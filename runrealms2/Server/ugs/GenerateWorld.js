const axios = require('axios-1.6');

// Replace this with the deployed HTTPS URL of Server/world-director.
// Keep all OpenAI credentials in that service, never in Unity or Cloud Code parameters.
const DIRECTOR_URL = 'https://replace-with-your-director.example/v2/blueprint';

module.exports = async ({ params, context, logger }) => {
  const seed = String(params.seed || '').trim().toUpperCase();
  const skill = Math.max(0, Math.min(1, Number(params.skill) || 0.5));
  if (!/^[A-Z0-9-]{1,32}$/.test(seed)) throw new Error('invalid_seed');

  try {
    const response = await axios.post(
      DIRECTOR_URL,
      { seed, skill, playerId: context.playerId },
      {
        timeout: 3500,
        headers: {
          'content-type': 'application/json',
          'x-client-build': 'run-realms-2.0'
        },
        validateStatus: status => status >= 200 && status < 300
      }
    );

    const result = response.data;
    if (!result || !Array.isArray(result.realmOrder) || result.realmOrder.length !== 8) throw new Error('invalid_director_response');
    return result;
  } catch (error) {
    logger.error('World director request failed', {
      playerId: context.playerId,
      seed,
      message: error.message
    });
    throw error;
  }
};
