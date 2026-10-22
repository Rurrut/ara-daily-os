'use strict';
// Local stdio transport only. All policy and arithmetic stay in offline-core.js.
const readline=require('node:readline'),crypto=require('node:crypto'),fs=require('node:fs');
const E=require(process.argv[2]),N=E.nativeApi;
const parse=x=>typeof x==='string'?JSON.parse(x):x;
const state=q=>E.decode(parse(q.state)),single=q=>E.decode({Days:[parse(q.day)],CategorySettings:[]}).Days[0];
const origin=d=>N.baseMs(d.Date),minutes=(v,d)=>N.parseTime(v,d.Date);
const blocks=(value,d)=>parse(value).map(b=>({...b,Start:minutes(b.Start,d)}));
const stamp=(m,d)=>'/Date('+(origin(d)+m*60000)+'+0900)/';
const encodeBlocks=(list,d)=>list.map(b=>({...b,Start:stamp(b.Start,d)}));
function plan(value,d){const p=parse(value);p.Blocks=blocks(p.Blocks||[],d);if(p.VisualBlocks)p.VisualBlocks=blocks(p.VisualBlocks,d);delete p.DisplayBlocks;p.FixedIds??=[];if(p.FutureFloor!=null)p.FutureFloor=minutes(p.FutureFloor,d);return p}
function encodePlan(p,d){const r={...p,Blocks:encodeBlocks(p.Blocks,d),VisualBlocks:p.DisplayBlocks?encodeBlocks(p.DisplayBlocks,d):p.VisualBlocks?encodeBlocks(p.VisualBlocks,d):null};if(p.FutureFloor!=null)r.FutureFloor=stamp(p.FutureFloor,d);delete r.DisplayBlocks;return JSON.stringify(r)}
const changedDay=d=>JSON.stringify(E.encode({Days:[d]}).Days[0]);
function execute(q){
 if(q.op==='identity')return {hash:crypto.createHash('sha256').update(fs.readFileSync(process.argv[2])).digest('hex'),runtime:process.version};
 if(q.op==='previewSeed')return {state:JSON.stringify(E.encode(E.previewSeed('2026-10-22',{mode:'clock',template:'work',wake:420,sleep:1380})))};
 if(q.op==='validateDay'){N.validateDay(single(q));return {}}
 if(['report','stats','activity','reset'].includes(q.op)){const d=single(q);if(q.op==='report')return {value:E.adherence(d)};if(q.op==='stats'){const count=d.Records.reduce((a,b)=>a+b.Count,0);return {value:{total:d.Records.reduce((a,b)=>a+b.Score*b.Count,0),satisfaction:count?d.Records.reduce((a,b)=>a+E.sat(b)*b.Count,0)/count:0}}}if(q.op==='activity'){const b=N.activity(d,q.category);return {block:b?JSON.stringify(encodeBlocks([b],d)[0]):null}}N.reset(d);return {day:changedDay(d)}}
 if(q.op==='list'){const d={Date:q.date},list=blocks(q.list,d),start=minutes(q.start,d);let result;
  if(q.action==='score')result=N.setScore(list,start,q.count,q.value,q.satisfaction);
  else if(q.action==='range'||q.action==='clear'){result=N.setRange(list,q.action==='clear'?N.block({Id:'__clear__',Start:start,Count:q.count}):blocks([parse(q.block)],d)[0]);if(q.action==='clear')result=result.filter(b=>b.Id!=='__clear__')}
  else if(q.action==='merge')result=N.mergeBlocks(list);
  else throw Error('지원하지 않는 공통 블록 작업입니다');
  return {list:JSON.stringify(encodeBlocks(result,d))};
 }
 const s=state(q),empty=s.Days.length===0,d=E.day(s,q.date||s.Days[0]?.Date||'2000-01-01');let p,value;
 switch(q.op){
 case 'build':p=E.build(s,d);break;
 case 'generate':return {blocks:JSON.stringify(encodeBlocks(E.generate(s,d),d))};
 case 'snapshot':return {snapshot:JSON.stringify(E.snapshot(s,d.Date,q.now))};
 case 'reminders':return {feed:JSON.stringify({...E.reminders(s,q.now),generated:q.now})};
 case 'ensure':N.ensureGrid(s,d);break;
 case 'defaults':N.applyDefaults(s);break;
 case 'upgradeRoutine':N.upgradeRoutine(s,q.date);break;
 case 'upgradeAdaptive':N.upgradeAdaptive(s);break;
 case 'shortenYogurt':value=E.shortenYogurt(s,q.now);break;
 case 'validate':N.validateState(s);break;
 case 'target':value=E.target(s,d);break;
 case 'mealEnd':value=N.mealEnd(d,plan(q.plan,d),q.meal);break;
 case 'requiredFinish':value=E.requiredFinish(s,d,plan(q.plan,d));break;
 case 'frontBuffer':p=plan(q.plan,d);N.front(s,d,p,minutes(q.cutoff,d),q.phase);break;
 case 'rebalance':p=plan(q.plan,d);N.rebalance(s,d,p);break;
 case 'manual':p=plan(q.plan,d);p.Overflow=N.debt(s,d,p);p.Changes.push('직접 수정한 계획 유지 · 다음 식사·운동 기록에서 이후 일정 자동 조정');break;
 case 'historyEnsure':N.capture(d,N.desired(s,d),d.GridBase,true);break;
 case 'advance':{const before=E.decode(parse(q.before)),old=E.day(before,d.Date);E.advance(before,old,s,d);break}
 case 'adherenceUpdate':{const before=E.decode(parse(q.before)),old=E.day(before,d.Date);N.ahUpdate(before,old,s,d,blocks(q.previous,d));break}
 case 'satisfaction':{const stats=E.satisfactionStats(d,minutes(q.sleep,d),s,q.now);value={Average:stats.average,WeightedTotal:stats.weightedTotal,Minutes:stats.minutes,Finish:stats.finish};break}
 case 'detectMeal':value=N.detectMeal(d,plan(q.plan,d),blocks([parse(q.block)],d)[0]);break;
 case 'legacy':{const old=N.legacyBuild(s,d),r={...old,Target:stamp(old.Target,d),Slots:old.Slots.map(x=>({...x,Start:stamp(x.Start,d),End:stamp(x.End,d)}))};return {legacy:JSON.stringify(r)}}
 case 'apply':{const next=E.apply(s,parse(q.command));return {state:JSON.stringify(E.encode(next))}}
 default:throw Error('지원하지 않는 공통 엔진 요청입니다');
 }
 if(empty&&['defaults','upgradeRoutine','upgradeAdaptive','shortenYogurt','validate'].includes(q.op))s.Days=[];
 return {state:JSON.stringify(E.encode(s)),day:changedDay(d),plan:p?encodePlan(p,d):null,value};
}
const input=readline.createInterface({input:process.stdin,crlfDelay:Infinity,terminal:false});
input.on('line',line=>{let q;try{if(line.length>160*1024*1024)throw Error('공통 엔진 입력이 너무 큽니다');q=JSON.parse(line);const result=execute(q);process.stdout.write(JSON.stringify({id:q.id,ok:true,result})+'\n')}catch(e){process.stdout.write(JSON.stringify({id:q?.id,ok:false,error:e.message||'공통 엔진 계산 실패'})+'\n')}});
input.on('close',()=>process.exit(0));
