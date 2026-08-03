import http from 'node:http';
import crypto from 'node:crypto';

const PORT = Number(process.env.PORT || 8787);
const OPENAI_API_KEY = process.env.OPENAI_API_KEY;
const OPENAI_MODEL = process.env.OPENAI_MODEL || 'gpt-5.6';
const ALLOWED_ORIGIN = process.env.ALLOWED_ORIGIN || '*';
const CACHE_TTL_MS = Number(process.env.CACHE_TTL_MS || 30 * 60 * 1000);
const RATE_WINDOW_MS = 60_000;
const RATE_LIMIT = Number(process.env.RATE_LIMIT || 20);
const cache = new Map();
const rateBuckets = new Map();

const blueprintSchema = {
  type: 'object',
  additionalProperties: false,
  required: ['seed', 'realmOrder', 'intensityCurve', 'theme'],
  properties: {
    seed: { type: 'string', minLength: 1, maxLength: 32 },
    realmOrder: {
      type: 'array', minItems: 8, maxItems: 8, uniqueItems: true,
      items: { type: 'integer', minimum: 0, maximum: 7 }
    },
    intensityCurve: {
      type: 'array', minItems: 6, maxItems: 6,
      items: { type: 'number', minimum: 0.1, maximum: 1 }
    },
    theme: { type: 'string', minLength: 3, maxLength: 80 }
  }
};

function headers(contentType = 'application/json; charset=utf-8') {
  return {
    'content-type': contentType,
    'access-control-allow-origin': ALLOWED_ORIGIN,
    'access-control-allow-methods': 'GET,POST,OPTIONS',
    'access-control-allow-headers': 'content-type,x-client-build',
    'cache-control': 'no-store',
    'x-content-type-options': 'nosniff',
    'referrer-policy': 'no-referrer'
  };
}

function send(res, status, body) {
  res.writeHead(status, headers());
  res.end(JSON.stringify(body));
}

function normalizeSeed(value) {
  const seed = String(value || '').trim().toUpperCase();
  return /^[A-Z0-9-]{1,32}$/.test(seed) ? seed : null;
}

function rateLimited(ip) {
  const now = Date.now();
  const bucket = rateBuckets.get(ip);
  if (!bucket || now - bucket.startedAt > RATE_WINDOW_MS) {
    rateBuckets.set(ip, { startedAt: now, count: 1 });
    return false;
  }
  bucket.count += 1;
  return bucket.count > RATE_LIMIT;
}

async function readJson(req) {
  let text = '';
  for await (const chunk of req) {
    text += chunk;
    if (text.length > 16_384) throw new Error('payload_too_large');
  }
  return JSON.parse(text || '{}');
}

function validBlueprint(value, requestedSeed) {
  if (!value || value.seed !== requestedSeed) return false;
  if (!Array.isArray(value.realmOrder) || value.realmOrder.length !== 8 || new Set(value.realmOrder).size !== 8) return false;
  if (value.realmOrder.some(item => !Number.isInteger(item) || item < 0 || item > 7)) return false;
  if (!Array.isArray(value.intensityCurve) || value.intensityCurve.length !== 6) return false;
  if (value.intensityCurve.some(item => !Number.isFinite(item) || item < 0.1 || item > 1)) return false;
  for (let index = 1; index < value.intensityCurve.length; index += 1) {
    if (value.intensityCurve[index] + 0.08 < value.intensityCurve[index - 1]) return false;
  }
  return typeof value.theme === 'string' && value.theme.length >= 3 && value.theme.length <= 80;
}

function outputText(response) {
  if (typeof response.output_text === 'string') return response.output_text;
  const pieces = [];
  for (const item of response.output || []) {
    for (const content of item.content || []) {
      if (content.type === 'output_text' && typeof content.text === 'string') pieces.push(content.text);
    }
  }
  return pieces.join('');
}

async function generateBlueprint(seed, skill) {
  const response = await fetch('https://api.openai.com/v1/responses', {
    method: 'POST',
    headers: {
      authorization: `Bearer ${OPENAI_API_KEY}`,
      'content-type': 'application/json',
      'x-client-request-id': crypto.randomUUID()
    },
    body: JSON.stringify({
      model: OPENAI_MODEL,
      store: false,
      reasoning: { effort: 'high' },
      max_output_tokens: 1000,
      instructions: [
        'You are the server-authoritative world director for RUN//REALMS 2.0, a Unity 3D endless runner.',
        'Return only the strict blueprint JSON.',
        'Realm indices: 0 Neon Rain City, 1 Desert Megacity, 2 Flooded Kingdom, 3 Zero-G Station, 4 Overgrown Lab, 5 Frozen Megastructure, 6 Volcanic Foundry, 7 Celestial Ruins.',
        'Use every realm exactly once before repetition.',
        'Make the six intensity values broadly increase, but include one small recovery valley when useful.',
        'Do not create geometry, code, dialogue, sexual content, executable instructions, or arbitrary assets.',
        'The Unity client independently validates this plan and converts it only into predefined safe gameplay primitives.'
      ].join(' '),
      input: `Seed ${seed}. Estimated player skill ${skill.toFixed(2)}. Choose a varied cinematic realm order and a fair pacing curve.`,
      text: {
        format: {
          type: 'json_schema',
          name: 'run_realms_2_blueprint',
          strict: true,
          schema: blueprintSchema
        }
      }
    })
  });
  if (!response.ok) throw new Error(`openai_${response.status}`);
  const result = await response.json();
  const parsed = JSON.parse(outputText(result));
  if (!validBlueprint(parsed, seed)) throw new Error('invalid_model_blueprint');
  return parsed;
}

const server = http.createServer(async (req, res) => {
  if (req.method === 'OPTIONS') return send(res, 204, {});
  if (req.method === 'GET' && req.url === '/health') {
    return send(res, 200, { ok: true, service: 'run-realms-2-world-director', model: OPENAI_MODEL, configured: Boolean(OPENAI_API_KEY) });
  }
  if (req.method !== 'POST' || req.url !== '/v2/blueprint') return send(res, 404, { error: 'not_found' });

  const ip = req.socket.remoteAddress || 'unknown';
  if (rateLimited(ip)) return send(res, 429, { error: 'rate_limited' });
  if (!OPENAI_API_KEY) return send(res, 503, { error: 'OPENAI_API_KEY_not_configured' });

  try {
    const body = await readJson(req);
    const seed = normalizeSeed(body.seed);
    if (!seed) return send(res, 400, { error: 'invalid_seed' });
    const skill = Math.max(0, Math.min(1, Number(body.skill) || 0.5));
    const cacheKey = `${seed}:${Math.round(skill * 10)}`;
    const cached = cache.get(cacheKey);
    if (cached && Date.now() - cached.createdAt < CACHE_TTL_MS) return send(res, 200, { ...cached.value, source: 'cache' });

    const blueprint = await generateBlueprint(seed, skill);
    cache.set(cacheKey, { value: blueprint, createdAt: Date.now() });
    return send(res, 200, blueprint);
  } catch (error) {
    console.error(JSON.stringify({ event: 'blueprint_failed', message: error.message }));
    return send(res, 500, { error: 'blueprint_failed' });
  }
});

server.listen(PORT, () => {
  console.log(`RUN//REALMS 2 world director listening on ${PORT} using ${OPENAI_MODEL}`);
});
