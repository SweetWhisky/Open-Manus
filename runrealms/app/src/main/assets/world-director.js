'use strict';
(function(root){
  const REALMS=[
    {name:'NEON RAIN CITY',short:'NEON',sky:'#061326',road:'#08101d',a:'#23d7ff',b:'#ff3bd4',weather:'rain'},
    {name:'DESERT MEGACITY',short:'DUNE',sky:'#241106',road:'#24180f',a:'#ffb547',b:'#ff694d',weather:'dust'},
    {name:'FLOODED KINGDOM',short:'TIDE',sky:'#071b27',road:'#09222d',a:'#4fe4ff',b:'#52ffbf',weather:'mist'},
    {name:'ZERO-G STATION',short:'ORBIT',sky:'#080815',road:'#11101f',a:'#8b5cff',b:'#23d7ff',weather:'stars'},
    {name:'OVERGROWN LAB',short:'VERDANT',sky:'#07150f',road:'#0b1a14',a:'#67ff80',b:'#ffb547',weather:'spores'},
    {name:'FROZEN MEGASTRUCTURE',short:'FROST',sky:'#071521',road:'#0b1d2a',a:'#a9ecff',b:'#6ba7ff',weather:'snow'},
    {name:'VOLCANIC FOUNDRY',short:'FORGE',sky:'#240905',road:'#210d09',a:'#ff6b35',b:'#ffd166',weather:'embers'},
    {name:'CELESTIAL RUINS',short:'AETHER',sky:'#100b24',road:'#15102a',a:'#c59bff',b:'#5de4ff',weather:'comets'}
  ];
  const PATTERN_IDS=['single','double','jump_reward','slide_reward','switchback','drone_split','shard_wave','power_lane','staggered','precision','recovery','mixed'];
  function hashSeed(s){let h=2166136261>>>0;for(let i=0;i<s.length;i++){h^=s.charCodeAt(i);h=Math.imul(h,16777619)}return h>>>0}
  function makeRng(seed){let n=hashSeed(String(seed));return function(){n=(Math.imul(n,1664525)+1013904223)>>>0;return n/4294967296}}
  function shuffle(list,rng){for(let i=list.length-1;i>0;i--){const j=Math.floor(rng()*(i+1));const t=list[i];list[i]=list[j];list[j]=t}return list}
  function lane(rng){return Math.floor(rng()*3)}
  function otherLane(base,offset=1){return (base+offset)%3}
  function validateItems(items){
    if(!Array.isArray(items)||items.length===0||items.length>12)return false;
    const groups=new Map();
    for(const item of items){
      if(!item||!['block','bar','gate','drone','shard','shield','magnet','boost'].includes(item.type))return false;
      if(!Number.isInteger(item.lane)||item.lane<0||item.lane>2)return false;
      if(!Number.isFinite(item.z)||item.z<0.65||item.z>1.65)return false;
      if(['block','bar','gate','drone'].includes(item.type)){
        const bucket=Math.round(item.z*10);if(!groups.has(bucket))groups.set(bucket,new Set());groups.get(bucket).add(item.lane)
      }
    }
    for(const occupied of groups.values())if(occupied.size>=3)return false;
    return true;
  }
  function buildPattern(id,rng,difficulty){
    const z=1.2,l=lane(rng),safe=lane(rng),items=[];
    switch(id){
      case 'single':items.push({type:rng()<.45?'block':'gate',lane:l,z});break;
      case 'double':for(let i=0;i<3;i++)if(i!==safe)items.push({type:'block',lane:i,z});items.push({type:'shard',lane:safe,z:z+.08});break;
      case 'jump_reward':items.push({type:'block',lane:l,z},{type:'shard',lane:l,z:z+.05},{type:'shard',lane:l,z:z+.13});break;
      case 'slide_reward':items.push({type:'bar',lane:l,z},{type:'shard',lane:l,z:z+.05},{type:'shard',lane:l,z:z+.13});break;
      case 'switchback':items.push({type:'block',lane:l,z},{type:'block',lane:otherLane(l,1),z:z+.2},{type:'shard',lane:otherLane(l,2),z:z+.11});break;
      case 'drone_split':items.push({type:'drone',lane:l,z},{type:'block',lane:otherLane(l,2),z:z+.18},{type:'shard',lane:otherLane(l,1),z:z+.09});break;
      case 'shard_wave':for(let i=0;i<5;i++)items.push({type:'shard',lane:(l+i)%3,z:z+i*.09});break;
      case 'power_lane':for(let i=0;i<3;i++)if(i!==safe)items.push({type:'gate',lane:i,z});items.push({type:['shield','magnet','boost'][Math.floor(rng()*3)],lane:safe,z:z+.08});break;
      case 'staggered':items.push({type:'bar',lane:l,z},{type:'block',lane:otherLane(l,1),z:z+.22},{type:'shard',lane:otherLane(l,2),z:z+.12});break;
      case 'precision':items.push({type:'block',lane:0,z},{type:'bar',lane:2,z:z+.16},{type:'shard',lane:1,z:z+.08});break;
      case 'recovery':for(let i=0;i<4;i++)items.push({type:'shard',lane:l,z:z+i*.1});if(rng()<.35)items.push({type:'shield',lane:l,z:z+.45});break;
      default:items.push({type:rng()<.5?'gate':'drone',lane:l,z},{type:'block',lane:otherLane(l,1),z:z+.2},{type:'shard',lane:otherLane(l,2),z:z+.1});
    }
    if(difficulty>.78&&id!=='recovery'&&id!=='shard_wave'&&rng()<.25){const bonusLane=otherLane(l,2);items.push({type:'shard',lane:bonusLane,z:z+.34})}
    return {id,items};
  }
  function sanitizeBlueprint(raw){
    if(!raw||typeof raw!=='object')return null;
    const order=Array.isArray(raw.realmOrder)?raw.realmOrder.map(Number):[];
    if(order.length!==8||new Set(order).size!==8||order.some(v=>!Number.isInteger(v)||v<0||v>7))return null;
    const curve=Array.isArray(raw.intensityCurve)?raw.intensityCurve.map(Number):[];
    if(curve.length!==6||curve.some(v=>!Number.isFinite(v)||v<0.15||v>1))return null;
    const bias={};
    if(raw.patternBias&&typeof raw.patternBias==='object')for(const id of PATTERN_IDS){const v=Number(raw.patternBias[id]);if(Number.isFinite(v))bias[id]=Math.max(.25,Math.min(2,v))}
    return {realmOrder:order,intensityCurve:curve,patternBias:bias,theme:String(raw.theme||'Adaptive convergence').slice(0,80)};
  }
  function createPlan(seed,blueprint){
    const rng=makeRng(seed+'|PLAN'),safe=sanitizeBlueprint(blueprint);
    const realmOrder=safe?safe.realmOrder:shuffle([0,1,2,3,4,5,6,7],rng);
    const intensityCurve=safe?safe.intensityCurve:[.22,.34,.48,.62,.78,.92];
    return {seed:String(seed),realmOrder,intensityCurve,patternBias:safe?safe.patternBias:{},theme:safe?safe.theme:'Offline deterministic director'};
  }
  function selectPattern(plan,state){
    const difficulty=Math.max(0,Math.min(1,Number(state.difficulty)||0));
    const recent=Array.isArray(state.recent)?state.recent:[];
    const rng=state.rng;
    const unlocked=difficulty<.25?PATTERN_IDS.slice(0,5):difficulty<.55?PATTERN_IDS.slice(0,9):PATTERN_IDS;
    let total=0,weighted=[];
    for(const id of unlocked){let w=(plan.patternBias[id]||1);if(recent.includes(id))w*=.15;if(id==='recovery')w*=difficulty>.72?.55:1.15;if(id==='precision'&&difficulty<.45)w*=.2;total+=w;weighted.push([id,total])}
    let roll=rng()*total,id=weighted[weighted.length-1][0];for(const entry of weighted)if(roll<=entry[1]){id=entry[0];break}
    for(let attempt=0;attempt<5;attempt++){const pattern=buildPattern(id,rng,difficulty);if(validateItems(pattern.items))return pattern;id='single'}
    return {id:'single',items:[{type:'block',lane:lane(rng),z:1.2}]};
  }
  function realmForStage(plan,stage){return plan.realmOrder[Math.max(0,stage)%plan.realmOrder.length]}
  const api={REALMS,PATTERN_IDS,hashSeed,makeRng,createPlan,selectPattern,realmForStage,sanitizeBlueprint,validateItems};
  root.WorldDirector=api;if(typeof module!=='undefined'&&module.exports)module.exports=api;
})(typeof globalThis!=='undefined'?globalThis:this);
