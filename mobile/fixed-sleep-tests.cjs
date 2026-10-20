const assert=require('assert/strict'),E=require('./offline-core.js');
let n=0;const date='2026-10-19';
for(const wake of [0,480,900,1410])for(const delay of [0,30,50,60,90,120,180,300])for(const duration of [1,3,6]){
 const s=E.seed(date,wake),d=s.Days[0],initial=E.snapshot(s,date),fourth=d.GridBase.find(b=>b.MealId==='b15');
 d.Records.push({...fourth,Id:'actual-fourth',Start:fourth.Start+delay,Count:duration,Score:4,SatisfactionValue:3});
 const facts=JSON.stringify(d.Records),p=E.build(s,d),v=E.snapshot(s,date);
 assert.equal(v.sleep,initial.sleep);assert.equal(v.target,initial.target);assert.equal(v.lightsStart,initial.lightsStart);
 assert.equal(JSON.stringify(d.Records),facts);assert.equal(E.sleep(s,d,p),E.target(s,d));
 assert.ok(E.requiredFinish(s,d,p)>=fourth.Start+delay+duration*10+120);
 assert.ok(p.Overflow>=Math.max(0,fourth.Start+delay+duration*10+120-E.target(s,d)));
 const yogurt=p.Blocks.find(b=>b.MealId==='b16');if(yogurt)assert.ok(yogurt.Start+yogurt.Count*10<=E.target(s,d));
 const now=Date.parse(date+'T00:00:00+09:00')+wake*60000;
 const before=E.reminders(E.seed(date,wake),now).items.filter(x=>x.key.startsWith('sleep|')||x.key.startsWith('lights|'));
 const after=E.reminders(s,now).items.filter(x=>x.key.startsWith('sleep|')||x.key.startsWith('lights|'));
 assert.deepEqual(after,before);assert.equal(v.satisfactionAverage,E.satisfactionAverage(d,E.target(s,d)));n++;
}
console.log(`RESULT ${n}/${n} fixed bedtime scenarios passed (facts, yogurt cutoff, rest debt, lights, notifications, satisfaction).`);
