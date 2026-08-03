'use strict';
const assert=require('node:assert/strict');
const director=require('../app/src/main/assets/world-director.js');

const a=director.createPlan('TEST-SEED');
const b=director.createPlan('TEST-SEED');
assert.deepEqual(a,b,'same seed must create the same plan');
assert.equal(new Set(a.realmOrder).size,8,'plan must visit all eight realms before repeating');

const rngA=director.makeRng('PATTERN-SEED');
const rngB=director.makeRng('PATTERN-SEED');
const sequenceA=[];const sequenceB=[];
for(let i=0;i<500;i++){
  const difficulty=i/499;
  const pa=director.selectPattern(a,{difficulty,recent:sequenceA.slice(-3),rng:rngA});
  const pb=director.selectPattern(b,{difficulty,recent:sequenceB.slice(-3),rng:rngB});
  assert.equal(director.validateItems(pa.items),true,'generated pattern must remain beatable');
  assert.deepEqual(pa,pb,'same state and seed must reproduce identical patterns');
  sequenceA.push(pa.id);sequenceB.push(pb.id);
}

assert.equal(director.sanitizeBlueprint({realmOrder:[0,1,2,3,4,5,6,7],intensityCurve:[.2,.3,.4,.5,.7,.9],patternBias:{single:1.5},theme:'test'}).theme,'test');
assert.equal(director.sanitizeBlueprint({realmOrder:[0,0,1,2,3,4,5,6],intensityCurve:[.2,.3,.4,.5,.7,.9]}),null,'duplicate realms must be rejected');
console.log('RUN//REALMS world director tests passed');
