const assert=require('node:assert/strict'),E=require('./offline-core.js');
const date='2026-10-20';let passed=0;
function scenario(wake){let s=E.seed(date,wake);for(const c of [{sourceId:'plan-b1',first:wake/10+3,last:wake/10+5},{sourceId:'plan-b4',first:wake/10+15,last:wake/10+18},{sourceId:'plan-b6',first:wake/10+21,last:wake/10+23}])s=E.apply(s,{date,lane:1,op:'copy',...c});return s;}
for(const wake of [0,480,900])for(const op of ['copy','category','detail']){
 const before=scenario(wake),facts=E.clone(before.Days[0].Records),view=E.snapshot(before,date),at=wake/10+20;
 const c={date,lane:1,first:at,last:at,op,sourceId:'plan-prep-b6',category:'끼니 준비',name:'2끼니 준비'};
 const after=E.apply(before,c),next=E.snapshot(after,date),prep=after.Days[0].Records.find(b=>b.Start===at*10);
 assert.equal(prep.Category,'끼니 준비');assert.equal(prep.MealId,'');if(op==='copy')assert.equal(prep.PrepFor,'b6');
 for(const b of facts)assert.deepEqual(after.Days[0].Records.find(a=>a.Id===b.Id),b);
 assert.deepEqual(next.planned,view.planned);assert.equal(next.sleep,view.sleep);assert.equal(next.adherenceScore,view.adherenceScore);
 assert.deepEqual(E.snapshot(E.decode(E.encode(after)),date),next);passed++;
}
// The fix must not disable the existing guard for a recorded non-prep activity.
const blocked=E.seed(date),d=blocked.Days[0];d.Records.push({...d.GridBase.find(b=>b.MealId==='b6'),Id:'actual-second',Start:690});
d.Records.push({Id:'actual-work',Start:680,Count:1,Category:'작업',Name:'기존 활동',Score:0,SatisfactionValue:2,MealId:'',PrepFor:'',TemplateId:'',Priority:0,Minimum:1});
assert.throws(()=>E.build(blocked,d),/이른 끼니/);passed++;
console.log(`RESULT ${passed}/${passed} prep-record scenarios passed: copy/category/detail, frozen plans and actuals, roundtrip, real activity guard.`);
