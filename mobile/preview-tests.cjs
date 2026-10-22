const assert=require('node:assert/strict'),E=require('./offline-core.js');let checks=0;
const ok=(fn)=>{fn();checks++};
for(const mode of ['wake','clock','weekday'])for(const template of ['free','work','study','blank'])for(const date of ['2026-10-22','2026-10-24']){
 const profile={mode,template,wake:420,sleep:1380,exercise:true},s=E.previewSeed(date,profile),v=E.snapshot(s,date);
 ok(()=>{assert.equal(s.Template.length,0);assert.equal(s.AdaptivePriority,false);assert.ok(!v.planned.some(b=>/존2|웨이트|요거트/.test(b.name)));assert.equal(v.actual.length,0);assert.equal(v.sleep,138);assert.ok(v.planned.every((b,i,a)=>!i||a[i-1].start+a[i-1].count<=b.start));assert.equal(E.snapshot(E.decode(E.encode(s)),date).sleep,v.sleep)});
 const moved=E.apply(s,{date,op:'wake',lane:1,first:48,last:48}),changed=E.snapshot(moved,date);
 ok(()=>{assert.equal(changed.wakeConfirmed,true);assert.equal(changed.wake,48);assert.equal(changed.sleep,mode==='wake'?144:138);assert.equal(changed.actual[0].category,'기상')});
 if(template==='work'&&mode!=='wake'&&date==='2026-10-22')ok(()=>assert.equal(changed.planned.find(b=>b.templateId==='p-work-am').start,v.planned.find(b=>b.templateId==='p-work-am').start));
}
ok(()=>assert.throws(()=>E.previewProfile({wake:480,sleep:600}),/6~20시간/));
ok(()=>assert.throws(()=>E.previewProfile({mode:'invalid'})));
ok(()=>{const s=E.seed('2026-10-22');assert.equal(s.PreviewConfig,undefined);assert.ok(E.snapshot(s,'2026-10-22').planned.some(b=>b.name==='존2'))});
const date='2026-10-22';let s=E.previewSeed(date,{template:'study',wake:420,sleep:1380});
let before=E.snapshot(s,date),study=before.planned.find(b=>b.templateId==='p-study-am');
s=E.apply(s,{date,op:'copy',lane:1,first:study.start+12,last:study.start+20,sourceId:study.id});
let after=E.snapshot(s,date);
ok(()=>{assert.deepEqual(after.planned.filter(b=>b.start<=study.start),before.planned.filter(b=>b.start<=study.start));assert.equal(after.actual[0].start,study.start+12);assert.equal(after.sleep,before.sleep);assert.ok(after.adjustments.length);assert.ok(after.planned.every((b,i,a)=>!i||a[i-1].start+a[i-1].count<=b.start))});
const lunch=after.planned.find(b=>b.templateId==='p-lunch'),frozen=after.planned.filter(b=>b.start<=lunch.start);
s=E.apply(s,{date,op:'copy',lane:1,first:lunch.start+6,last:lunch.start+8,sourceId:lunch.id});after=E.snapshot(s,date);
ok(()=>assert.deepEqual(after.planned.filter(b=>frozen.some(x=>x.id===b.id)),frozen));
ok(()=>{const sat=E.apply(s,{date,op:'satisfaction',lane:1,first:lunch.start+6,last:lunch.start+8,satisfaction:5});assert.equal(E.snapshot(sat,date).actual.find(b=>b.start===lunch.start+6).satisfaction,5)});
ok(()=>{const before=E.snapshot(s,date),b=before.actual.find(x=>x.templateId==='p-study-am');const next=E.apply(s,{date,op:'score',lane:1,first:b.start+1,last:b.start+2,score:4});assert.deepEqual(E.snapshot(next,date).planned,before.planned)});
console.log(`RESULT ${checks}/${checks} Preview scenarios passed (isolated seed, onboarding validation, day modes, persistence, frozen plans, fixed bedtime).`);
