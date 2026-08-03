# RUN//REALMS AI Director

Optional backend service for generating bounded world blueprints. The Android game is fully playable without it.

## Security model

- `OPENAI_API_KEY` exists only in the server environment.
- The APK never contains or receives the key.
- The server requests strict JSON-schema output and validates it again before returning it.
- The model can only influence realm order, bounded intensity values, pattern weights and a short theme label.
- The game converts the response into predefined, independently validated gameplay primitives.
- Requests are size-limited and rate-limited.

## Run

```bash
cp .env.example .env
export OPENAI_API_KEY='your-server-side-project-key'
export OPENAI_MODEL='gpt-5-pro'
node server.mjs
```

Set the deployed HTTPS `/blueprint` URL in `app/src/main/assets/config.js`, then rebuild the APK. Keep cleartext traffic disabled.

## Endpoint

`POST /blueprint`

```json
{"seed":"REALM-001","skill":0.5}
```

The service also provides `GET /health`.
