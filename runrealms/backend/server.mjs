import http from 'node:http';
import crypto from 'node:crypto';

const PORT=Number(process.env.PORT||8787);
const MODEL=process.env.OPENAI_MODEL||'gpt-5-pro';
const API_KEY=process.env.OPENAI_API_KEY;
const WINDOW_MS=60_000,MAX_REQUESTS=12,limits=new Map();
const PATTERNS=['single','double','jump_reward','slide_reward','switchback','drone_split','shard_wave','power_lane','staggered','precision','recovery','mixed'];
const schema={
  type:'object',additionalProperties:false,required:['realmOrder','intensityCurve','patternBias','theme'],
  properties:{
    realmOrder:{type:'array',minItems:8,maxItems:8,items:{type:'integer',minimum:0,maximum:7}},
    intensityCurve:{type:'array',minItems:6,maxItems:6,items:{type:'number',minimum:.15,maximum:1}},
    patternBias:{type:'object',additionalProperties:false,required:PATTERNS,properties:Object.fromEntries(PATTERNS.map(id=>[id,{type:'number',minimum:.25,maximum:2}]))},
    theme:{type:'string',minLength:3,maxLength:80}
  }
};
function send(res,status,body){res.writeHead(status,{'content-type':'application/json; charset=utf-8','access-control-allow-origin':'*','access-control-allow-methods':'POST,OPTIONS','access-control-allow-headers':'content-type','cache-control':'no-store','x-content-type-options':'nosniff'});res.end(JSON.stringify(body))}
function limited(ip){const now=Date.now(),slot=limits.get(ip);if(!slot||now-slot.start>WINDOW_MS){limits.set(ip,{start:now,count:1});return false}slot.count++;return slot.count>MAX_REQUESTS}
function validSeed(seed){return typeof seed==='string'&&/^[A-Z0-9-]{1,32}$/.test(seed)}
function sanitize(raw){
  if(!raw||!Array.isArray(raw.realmOrder)||raw.realmOrder.length!==8||new Set(raw.realmOrder).size!==8)return null;
  if(raw.realmOrder.some(v=>!Number.isInteger(v)||v<0||v>7))return null;
  if(!Array.isArray(raw.intensityCurve)||raw.intensityCurve.length!==6||raw.intensityCurve.some(v=>!Number.isFinite(v)||v<.15||v>1))return null;
  if(!raw.patternBias||PATTERNS.some(id=>!Number.isFinite(raw.patternBias[id])||raw.patternBias[id]<.25||raw.patternBias[id]>2))return null;
  return {realmOrder:raw.realmOrder,intensityCurve:raw.intensityCurve,patternBias:Object.fromEntries(PATTERNS.map(id=>[id,raw.patternBias[id]])),theme:String(raw.theme).slice(0,80)};
}
async function readJson(req){let data='';for await(const chunk of req){data+=chunk;if(data.length>16_384)throw new Error('payload_too_large')}return JSON.parse(data||'{}')}
async function blueprint(seed,skill){
  const response=await fetch('https://api.openai.com/v1/responses',{
    method:'POST',headers:{authorization:`Bearer ${API_KEY}`,'content-type':'application/json','x-client-request-id':crypto.randomUUID()},
    body:JSON.stringify({
      model:MODEL,store:false,reasoning:{effort:'high'},max_output_tokens:1400,
      instructions:'You are the RUN//REALMS world director. Return only a safe bounded gameplay blueprint. Use every realm index exactly once. Keep the intensity curve nondecreasing. Prefer readable pacing, recovery windows, and variety. The client independently validates all values and converts them only into predefined gameplay primitives.',
      input:`Seed: ${seed}. Estimated player skill from 0 to 1: ${Math.max(0,Math.min(1,Number(skill)||.5)).toFixed(2)}. Create a deterministic-feeling eight-realm sequence and balanced pattern weights.`,
      text:{format:{type:'json_schema',name:'run_realms_blueprint',strict:true,schema}}
    })
  });
  if(!response.ok)throw new Error(`openai_${response.status}`);
  const result=await response.json();return sanitize(JSON.parse(result.output_text));
}
const server=http.createServer(async(req,res)=>{
  if(req.method==='OPTIONS')return send(res,204,{});
  if(req.method==='GET'&&req.url==='/health')return send(res,200,{ok:true,model:MODEL});
  if(req.method!=='POST'||req.url!=='/blueprint')return send(res,404,{error:'not_found'});
  const ip=req.socket.remoteAddress||'unknown';if(limited(ip))return send(res,429,{error:'rate_limited'});
  if(!API_KEY)return send(res,503,{error:'OPENAI_API_KEY_not_configured'});
  try{const body=await readJson(req),seed=String(body.seed||'').toUpperCase();if(!validSeed(seed))return send(res,400,{error:'invalid_seed'});const plan=await blueprint(seed,body.skill);if(!plan)return send(res,502,{error:'invalid_model_output'});return send(res,200,plan)}catch(error){console.error(error);return send(res,500,{error:'blueprint_failed'})}
});
server.listen(PORT,()=>console.log(`RUN//REALMS AI director listening on ${PORT} with ${MODEL}`));
