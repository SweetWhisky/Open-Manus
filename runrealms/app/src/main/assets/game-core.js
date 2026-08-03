'use strict';
const $=id=>document.getElementById(id),canvas=$('game'),ctx=canvas.getContext('2d',{alpha:false,desynchronized:true});
const UI={hud:$('hud'),home:$('home'),results:$('results'),pause:$('pauseMenu'),settings:$('settings'),distance:$('distance'),score:$('score'),shards:$('shards'),realm:$('realmName'),focus:$('focusBar'),combo:$('combo')};
const realms=[
 {name:'NEON RAIN CITY',short:'NEON',sky:'#061326',road:'#08101d',a:'#23d7ff',b:'#ff3bd4',weather:'rain'},
 {name:'DESERT MEGACITY',short:'DUNE',sky:'#241106',road:'#24180f',a:'#ffb547',b:'#ff694d',weather:'dust'},
 {name:'FLOODED KINGDOM',short:'TIDE',sky:'#071b27',road:'#09222d',a:'#4fe4ff',b:'#52ffbf',weather:'mist'},
 {name:'ZERO-G STATION',short:'ORBIT',sky:'#080815',road:'#11101f',a:'#8b5cff',b:'#23d7ff',weather:'stars'},
 {name:'OVERGROWN LAB',short:'VERDANT',sky:'#07150f',road:'#0b1a14',a:'#67ff80',b:'#ffb547',weather:'spores'}
];
const store={get(k,d){try{const v=localStorage.getItem(k);return v===null?d:JSON.parse(v)}catch(e){return d}},set(k,v){try{localStorage.setItem(k,JSON.stringify(v))}catch(e){}}};
let profile=store.get('rr_profile',{best:0,bank:0,runs:0,unlocked:[0],reduced:false,haptics:true,mature:false});
let W=0,H=0,DPR=1,last=0,acc=0,state='home',game=null,touch=null,toastTimer=0;
const pool=[];for(let i=0;i<32;i++)pool.push({active:false});
function hashSeed(s){let h=2166136261>>>0;for(let i=0;i<s.length;i++){h^=s.charCodeAt(i);h=Math.imul(h,16777619)}return h>>>0}
function rng(){game.rng=(Math.imul(game.rng,1664525)+1013904223)>>>0;return game.rng/4294967296}
function resize(){DPR=Math.min(devicePixelRatio||1,2);W=innerWidth;H=innerHeight;canvas.width=Math.round(W*DPR);canvas.height=Math.round(H*DPR);ctx.setTransform(DPR,0,0,DPR,0,0)}addEventListener('resize',resize,{passive:true});resize();
function vibrate(ms=18){if(profile.haptics&&navigator.vibrate)navigator.vibrate(ms)}
function showToast(msg){const t=$('toast');t.textContent=msg;t.classList.add('show');clearTimeout(toastTimer);toastTimer=setTimeout(()=>t.classList.remove('show'),1800)}
function dateSeed(){const d=new Date();return `DAILY-${d.getUTCFullYear()}-${d.getUTCMonth()+1}-${d.getUTCDate()}`}
function refreshHome(){ $('bestHome').textContent=profile.best.toLocaleString();$('bankHome').textContent=profile.bank;$('runsHome').textContent=profile.runs;const box=$('realmCards');box.innerHTML='';realms.forEach((r,i)=>{const d=document.createElement('div');d.className='realmcard'+(profile.unlocked.includes(i)?' unlocked':'');d.dataset.name=r.short;d.style.background=`linear-gradient(135deg,${r.sky},${r.a}99,${r.b}99)`;box.appendChild(d)});$('motionToggle').classList.toggle('on',profile.reduced);$('hapticToggle').classList.toggle('on',profile.haptics);$('matureToggle').classList.toggle('on',profile.mature)}
function setOverlay(name){[UI.home,UI.results,UI.pause,UI.settings].forEach(x=>x.classList.add('hidden'));if(name)$(name).classList.remove('hidden')}
function chooseRealm(seed){return hashSeed(seed)%realms.length}
function createGame(seed){const r=chooseRealm(seed);return {seed,rng:hashSeed(seed),startRealm:r,realm:r,realmStage:0,visited:[r],time:0,distance:0,score:0,shards:0,speed:8,lane:1,targetLane:1,jump:0,slide:0,focus:100,focusActive:false,combo:1,comboTimer:0,spawn:0,pattern:0,fail:false,invuln:1.2,nearMiss:0,lastPatterns:[],difficulty:.25};}
function startRun(seed){seed=(seed||'REALM-001').trim().toUpperCase().replace(/[^A-Z0-9-]/g,'').slice(0,14)||'REALM-001';$('seedInput').value=seed;game=createGame(seed);pool.forEach(o=>o.active=false);state='playing';setOverlay(null);UI.hud.style.display='block';last=performance.now();acc=0;vibrate(12);}
function pauseGame(){if(state!=='playing')return;state='paused';setOverlay('pauseMenu');}
function resumeGame(){if(state!=='paused')return;setOverlay(null);state='playing';last=performance.now();}
function endRun(reason='COLLISION'){if(!game||game.fail)return;game.fail=true;state='results';UI.hud.style.display='none';profile.runs++;profile.bank+=game.shards;profile.best=Math.max(profile.best,Math.floor(game.score));for(const realmIndex of game.visited){if(!profile.unlocked.includes(realmIndex))profile.unlocked.push(realmIndex)}profile.unlocked.sort((a,b)=>a-b);store.set('rr_profile',profile);$('resultTitle').textContent=reason==='QUIT'?'RUN ENDED':'REALM FRACTURED';$('resultScore').textContent=Math.floor(game.score).toLocaleString();$('resultDistance').textContent=Math.floor(game.distance);$('resultShards').textContent=game.shards;$('resultNote').textContent=`Seed ${game.seed} • ${realms[game.realm].name} • Difficulty ${Math.round(game.difficulty*100)}%`;setOverlay('results');refreshHome();vibrate([25,30,70]);}
function alloc(type,lane,z,variant=0){let o=pool.find(x=>!x.active);if(!o)return;o.active=true;o.type=type;o.lane=lane;o.z=z;o.variant=variant;o.hit=false;o.passed=false}
function spawnPattern(){let p=Math.floor(rng()*7);for(let guard=0;guard<8&&game.lastPatterns.includes(p);guard++)p=Math.floor(rng()*7);game.lastPatterns.push(p);if(game.lastPatterns.length>3)game.lastPatterns.shift();const z=1.18;
 switch(p){case 0:alloc('block',Math.floor(rng()*3),z);break;case 1:alloc('bar',Math.floor(rng()*3),z);alloc('shard',(Math.floor(rng()*3)+1)%3,z+.12);break;case 2:{const safe=Math.floor(rng()*3);for(let l=0;l<3;l++)if(l!==safe)alloc('block',l,z);alloc('shard',safe,z+.08);break}case 3:{const l=Math.floor(rng()*3);alloc('gate',l,z);alloc('bar',(l+1)%3,z+.17);break}case 4:{const l=Math.floor(rng()*3);for(let i=0;i<4;i++)alloc('shard',(l+i)%3,z+i*.09);break}case 5:{const l=Math.floor(rng()*3);alloc('drone',l,z);alloc('block',(l+2)%3,z+.18);break}default:{alloc('block',0,z);alloc('bar',2,z+.18);alloc('shard',1,z+.1)}}}
function activateFocus(){if(state!=='playing'||game.focus<28)return;game.focusActive=true;vibrate(10)}
function setLane(n){game.targetLane=Math.max(0,Math.min(2,n))}
function swipe(dx,dy,duration){if(state!=='playing')return;const ax=Math.abs(dx),ay=Math.abs(dy);if(Math.max(ax,ay)<22){activateFocus();return}if(ax>ay){setLane(game.targetLane+(dx>0?1:-1));vibrate(8)}else if(dy<0){if(game.jump<=0.05)game.jump=1;vibrate(8)}else{game.slide=1;vibrate(8)}}
