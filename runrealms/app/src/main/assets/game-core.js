'use strict';
const $=id=>document.getElementById(id),canvas=$('game'),ctx=canvas.getContext('2d',{alpha:false,desynchronized:true});
const CFG=window.RUN_REALMS_CONFIG||{aiBlueprintEndpoint:'',aiTimeoutMs:1800,build:'1.0.0'},Director=window.WorldDirector,realms=Director.REALMS;
const UI={hud:$('hud'),home:$('home'),results:$('results'),pause:$('pauseMenu'),settings:$('settings'),distance:$('distance'),score:$('score'),shards:$('shards'),realm:$('realmName'),focus:$('focusBar'),combo:$('combo'),power:$('powerState')};
const store={get(k,d){try{const v=localStorage.getItem(k);return v===null?d:JSON.parse(v)}catch(e){return d}},set(k,v){try{localStorage.setItem(k,JSON.stringify(v))}catch(e){}}};
const defaultProfile={best:0,bank:0,runs:0,level:1,xp:0,streak:0,lastDay:'',unlocked:[0],mission:null,settings:{graphics:'auto',fps:'auto',reduced:false,haptics:true,contrast:false,mature:false}};
const loaded=store.get('rr_profile_v1',store.get('rr_profile',{}));
let profile={...defaultProfile,...loaded,settings:{...defaultProfile.settings,...(loaded.settings||{}),reduced:loaded.reduced??loaded.settings?.reduced??false,haptics:loaded.haptics??loaded.settings?.haptics??true,mature:loaded.mature??loaded.settings?.mature??false}};
if(!Array.isArray(profile.unlocked))profile.unlocked=[0];
let W=0,H=0,DPR=1,last=0,acc=0,state='home',game=null,touch=null,toastTimer=0;
const pool=[];for(let i=0;i<96;i++)pool.push({active:false});
const perf={mode:'auto',targetFps:60,maxDpr:1.5,renderScale:1,particles:24,shadows:true,avgMs:16.7,samples:0,totalMs:0,lastTune:0};
function choosePerformance(){
  const cores=navigator.hardwareConcurrency||4,memory=navigator.deviceMemory||4,mode=profile.settings.graphics;
  let tier=mode==='auto'?(cores>=8&&memory>=6?'high':cores>=6&&memory>=4?'balanced':'performance'):mode;
  if(tier==='high'){perf.maxDpr=2;perf.particles=42;perf.shadows=true}else if(tier==='balanced'){perf.maxDpr=1.6;perf.particles=26;perf.shadows=true}else{perf.maxDpr=1.2;perf.particles=12;perf.shadows=false}
  const requested=profile.settings.fps==='auto'?(tier==='high'?120:60):Number(profile.settings.fps);perf.targetFps=[30,60,90,120].includes(requested)?requested:60;perf.mode=tier;perf.renderScale=1;resize();
}
function resize(){DPR=Math.max(.75,Math.min(devicePixelRatio||1,perf.maxDpr)*perf.renderScale);W=innerWidth;H=innerHeight;canvas.width=Math.max(1,Math.round(W*DPR));canvas.height=Math.max(1,Math.round(H*DPR));ctx.setTransform(DPR,0,0,DPR,0,0)}
addEventListener('resize',resize,{passive:true});
function tunePerformance(frameMs,now){perf.totalMs+=frameMs;perf.samples++;if(perf.samples<90)return;perf.avgMs=perf.totalMs/perf.samples;perf.samples=0;perf.totalMs=0;if(now-perf.lastTune<3000||profile.settings.graphics!=='auto')return;perf.lastTune=now;const budget=1000/perf.targetFps;if(perf.avgMs>budget*1.22&&perf.renderScale>.72){perf.renderScale=Math.max(.72,perf.renderScale-.1);resize()}else if(perf.avgMs<budget*.72&&perf.renderScale<1){perf.renderScale=Math.min(1,perf.renderScale+.05);resize()}}
function vibrate(ms=18){if(profile.settings.haptics&&navigator.vibrate)navigator.vibrate(ms)}
function showToast(msg){const t=$('toast');t.textContent=msg;t.classList.add('show');clearTimeout(toastTimer);toastTimer=setTimeout(()=>t.classList.remove('show'),1800)}
function dateKey(){const d=new Date();return `${d.getUTCFullYear()}-${d.getUTCMonth()+1}-${d.getUTCDate()}`}
function dateSeed(){return `DAILY-${dateKey()}`}
function createMission(){const r=Director.makeRng(dateSeed()+'|MISSION'),types=[['distance',900+Math.floor(r()*7)*100,'Run distance'],['shards',18+Math.floor(r()*18),'Collect shards'],['near',6+Math.floor(r()*10),'Near misses'],['combo',5+Math.floor(r()*5),'Reach combo']];const m=types[Math.floor(r()*types.length)];return {day:dateKey(),type:m[0],target:m[1],label:m[2],progress:0,rewarded:false}}
function ensureMission(){if(!profile.mission||profile.mission.day!==dateKey())profile.mission=createMission()}
function missionValue(){if(!game)return 0;if(profile.mission.type==='distance')return Math.floor(game.distance);if(profile.mission.type==='shards')return game.shards;if(profile.mission.type==='near')return game.nearMiss;return Math.floor(game.maxCombo)}
function saveProfile(){store.set('rr_profile_v1',profile)}
function refreshHome(){
  ensureMission();$('bestHome').textContent=profile.best.toLocaleString();$('bankHome').textContent=profile.bank;$('runsHome').textContent=profile.runs;$('levelHome').textContent=profile.level;
  const box=$('realmCards');box.innerHTML='';realms.forEach((r,i)=>{const d=document.createElement('div');d.className='realmcard'+(profile.unlocked.includes(i)?' unlocked':'');d.dataset.name=r.short;d.style.background=`linear-gradient(135deg,${r.sky},${r.a}99,${r.b}99)`;box.appendChild(d)});
  const m=profile.mission,pct=Math.min(100,Math.round((m.progress/m.target)*100));$('missionText').textContent=`${m.label}: ${Math.min(m.progress,m.target)}/${m.target}`;$('missionBar').style.width=pct+'%';$('missionReward').textContent=m.rewarded?'COMPLETE':'75 SHARDS';
  $('motionToggle').classList.toggle('on',profile.settings.reduced);$('hapticToggle').classList.toggle('on',profile.settings.haptics);$('contrastToggle').classList.toggle('on',profile.settings.contrast);$('matureToggle').classList.toggle('on',profile.settings.mature);$('graphicsSelect').value=profile.settings.graphics;$('fpsSelect').value=String(profile.settings.fps);
  $('directorStatus').textContent=CFG.aiBlueprintEndpoint?'AI BLUEPRINT CACHE + OFFLINE FALLBACK':'OFFLINE DETERMINISTIC DIRECTOR';$('performanceStatus').textContent=`${perf.mode.toUpperCase()} • ${perf.targetFps} FPS TARGET • ${Math.round(DPR*100)}% SCALE`;
}
function setOverlay(name){[UI.home,UI.results,UI.pause,UI.settings].forEach(x=>x.classList.add('hidden'));if(name)$(name).classList.remove('hidden')}
function seedKey(seed){return `rr_plan_${seed}`}
function cachedBlueprint(seed){return Director.sanitizeBlueprint(store.get(`rr_ai_${seed}`,null))}
function resolvePlan(seed){let plan=store.get(seedKey(seed),null);if(plan&&Array.isArray(plan.realmOrder))return plan;plan=Director.createPlan(seed,cachedBlueprint(seed));store.set(seedKey(seed),plan);return plan}
async function prefetchBlueprint(seed){
  if(!CFG.aiBlueprintEndpoint||cachedBlueprint(seed)||store.get(seedKey(seed),null))return;
  const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),CFG.aiTimeoutMs||1800);
  try{const response=await fetch(CFG.aiBlueprintEndpoint,{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({seed,skill:Math.min(1,profile.level/30)}),signal:controller.signal});if(!response.ok)return;const safe=Director.sanitizeBlueprint(await response.json());if(safe){store.set(`rr_ai_${seed}`,safe);showToast('AI blueprint cached for '+seed)}}catch(e){}finally{clearTimeout(timer)}
}
function createGame(seed){const plan=resolvePlan(seed),rng=Director.makeRng(seed+'|RUN'),realm=Director.realmForStage(plan,0);return {seed,plan,rng,realm,realmStage:0,visited:[realm],time:0,distance:0,score:0,shards:0,speed:8,lane:1,targetLane:1,jump:0,slide:0,focus:100,focusActive:false,combo:1,maxCombo:1,comboTimer:0,spawn:0,fail:false,invuln:1.2,nearMiss:0,recentPatterns:[],difficulty:.2,shield:0,magnet:0,boost:0,mistakes:0}}
function startRun(seed){seed=(seed||'REALM-001').trim().toUpperCase().replace(/[^A-Z0-9-]/g,'').slice(0,32)||'REALM-001';$('seedInput').value=seed;game=createGame(seed);pool.forEach(o=>o.active=false);state='playing';setOverlay(null);UI.hud.style.display='block';last=performance.now();acc=0;vibrate(12);prefetchBlueprint('R-'+Math.random().toString(36).slice(2,10).toUpperCase())}
function pauseGame(){if(state!=='playing')return;state='paused';setOverlay('pauseMenu')}
function resumeGame(){if(state!=='paused')return;setOverlay(null);state='playing';last=performance.now()}
function updateMission(){ensureMission();profile.mission.progress=Math.max(profile.mission.progress,missionValue());let reward=0;if(profile.mission.progress>=profile.mission.target&&!profile.mission.rewarded){profile.mission.rewarded=true;profile.bank+=75;reward=75}return reward}
function endRun(reason='COLLISION'){
  if(!game||game.fail)return;game.fail=true;state='results';UI.hud.style.display='none';profile.runs++;profile.bank+=game.shards;profile.best=Math.max(profile.best,Math.floor(game.score));profile.xp+=Math.max(10,Math.floor(game.distance/20)+game.shards*2);profile.level=1+Math.floor(Math.sqrt(profile.xp/80));for(const realmIndex of game.visited)if(!profile.unlocked.includes(realmIndex))profile.unlocked.push(realmIndex);profile.unlocked.sort((a,b)=>a-b);
  const day=dateKey();profile.streak=profile.lastDay===day?profile.streak:profile.streak+1;profile.lastDay=day;const reward=updateMission();saveProfile();
  $('resultTitle').textContent=reason==='QUIT'?'RUN ENDED':'REALM FRACTURED';$('resultScore').textContent=Math.floor(game.score).toLocaleString();$('resultDistance').textContent=Math.floor(game.distance);$('resultShards').textContent=game.shards;$('resultNear').textContent=game.nearMiss;$('resultCombo').textContent='x'+game.maxCombo.toFixed(1);$('resultNote').textContent=`Seed ${game.seed} • ${game.visited.length} realms • ${Math.round(game.difficulty*100)}% intensity${reward?' • Daily reward +75':''}`;setOverlay('results');refreshHome();vibrate([25,30,70])
}
function alloc(type,lane,z){const o=pool.find(x=>!x.active);if(!o)return;o.active=true;o.type=type;o.lane=lane;o.z=z;o.hit=false;o.passed=false}
function spawnPattern(){const pattern=Director.selectPattern(game.plan,{difficulty:game.difficulty,recent:game.recentPatterns,rng:game.rng});game.recentPatterns.push(pattern.id);if(game.recentPatterns.length>4)game.recentPatterns.shift();for(const item of pattern.items)alloc(item.type,item.lane,item.z)}
function activateFocus(){if(state!=='playing'||game.focus<24)return;game.focusActive=true;vibrate(10)}
function setLane(n){game.targetLane=Math.max(0,Math.min(2,n))}
function swipe(dx,dy){if(state!=='playing')return;const ax=Math.abs(dx),ay=Math.abs(dy);if(Math.max(ax,ay)<22){activateFocus();return}if(ax>ay){setLane(game.targetLane+(dx>0?1:-1));vibrate(8)}else if(dy<0){if(game.jump<=.05)game.jump=1;vibrate(8)}else{game.slide=1;vibrate(8)}}
choosePerformance();ensureMission();resize();
