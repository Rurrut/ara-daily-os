const assert=require('node:assert/strict'),E=require('./offline-core.js');let n=0;const date='2026-10-21';
function first(wake,delay,count=3){const s=E.seed(date,wake),d=s.Days[0],b=d.GridBase.find(x=>x.MealId==='b1');d.Records.push({...b,Id:'actual-first',Start:b.Start+delay,Count:count,Score:3,SatisfactionValue:4});return s}
for(const wake of [0,480,900])for(const delay of [60,70,80,90,100,200,270]){
 const s=first(wake,delay),d=s.Days[0],facts=JSON.stringify(d.Records),p=E.build(s,d),y=p.Blocks.find(x=>x.MealId==='b16'),third=p.Blocks.find(x=>x.MealId==='b10');
 const gap=p.Changes.findIndex(x=>x.includes('요거트 종료→취침')),omission=p.Changes.findIndex(x=>x.includes('3끼니 제외'));
 assert.equal(JSON.stringify(d.Records),facts);assert.equal(E.sleep(s,d,p),E.target(s,d));
 if(delay===60){assert.equal(gap,-1);assert.ok(third)}
 if(delay===70||delay===80){assert.ok(third&&y);assert.equal(p.Overflow,0);assert.equal(E.target(s,d)-(y.Start+y.Count*10),delay===70?40:30);assert.equal(p.YogurtRestMinutes,delay===70?40:30)}
 if(omission>=0){assert.ok(gap>=0&&gap<omission);assert.equal(p.YogurtRestMinutes,30)}
 if(gap>=0){const wait=p.Changes.findIndex(x=>x.includes('존2·샤워→2끼니'));assert.ok(wait<gap)}
 for(let i=1;i<p.Blocks.length;i++)assert.ok(p.Blocks[i].Start>=p.Blocks[i-1].Start+p.Blocks[i-1].Count*10);
 const meals=p.Blocks.filter(x=>x.MealId);for(let i=1;i<meals.length;i++)assert.ok(meals[i].Start-meals[i-1].Start>=180);
 const gym=p.Blocks.find(x=>x.Category==='웨이트'),second=p.Blocks.find(x=>x.MealId==='b6');if(gym&&second)assert.ok(gym.Start-second.Start>=150);n++;
}
// A meal-two delay has no usable pre-meal waiting left: spend only the yogurt gap.
for(const delay of [10,20]){const s=E.seed(date),d=s.Days[0],second=d.GridBase.find(x=>x.MealId==='b6');d.Records.push({...second,Id:'actual-second',Start:second.Start+delay});const p=E.build(s,d);assert.ok(p.Blocks.find(x=>x.MealId==='b10'));assert.equal(p.YogurtRestMinutes,50-delay);assert.equal(p.Overflow,0);n++}
// The new pre-weight stage must not reinterpret or move an already eaten yogurt.
{const s=E.seed(date),d=s.Days[0],y=d.GridBase.find(x=>x.MealId==='b16');d.Records.push({...y,Id:'eaten',Start:y.Start+60});const facts=JSON.stringify(d.Records),p=E.build(s,d);assert.equal(JSON.stringify(d.Records),facts);assert.ok(!p.Changes.some(x=>x.includes('요거트 종료→취침')));n++}
console.log(`RESULT ${n}/${n} passed: yogurt gap before third omission, partial credit, thirty-minute floor, fixed sleep and protected facts.`);
