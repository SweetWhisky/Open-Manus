# RUN//REALMS 2.0 — Unity Remaster

A true Unity 3D remaster of RUN//REALMS, replacing the earlier WebView prototype with a native scene, physics triggers, procedural humanoid characters, streamed 3D environments, interactive sound, animated UI and server-ready world direction.

## Current playable scope

- Native Unity 6.3 LTS Android project
- Landscape endless-runner gameplay with three lanes
- Swipe left/right, jump, slide and tap-to-focus controls
- Procedural 3D player character with animated torso, head, arms and legs
- Procedural side characters/crowds with idle and dance motion
- Eight visual realms:
  - Neon Rain City
  - Desert Megacity
  - Flooded Kingdom
  - Zero-G Station
  - Overgrown Lab
  - Frozen Megastructure
  - Volcanic Foundry
  - Celestial Ruins
- Streamed road chunks and seamless realm transitions
- Generated textures, window patterns, emissive signs, structures, columns and weather particles
- Safe-lane obstacle patterns with jump and slide hazards
- Shards, shield, magnet and boost pickups
- Score, combo, near-miss and saved progression systems
- Animated home, HUD, pause, settings and result interfaces
- Procedurally synthesized music, ambience, UI clicks, pickups, impacts and whooshes
- Performance, Balanced and Ultra profiles targeting 60, 90 and 120 FPS
- Runtime quality fallback if sustained frame rate drops
- Deterministic offline world generation for repeatable seeds

## Architecture

### Unity client

`Assets/Scripts/Runtime`

- `RemasterBootstrap.cs` — scene creation, game state, scoring, camera, lighting and quality management
- `ProceduralRunner.cs` — humanoid construction, animation, touch controls, collision and power-up state
- `ProceduralWorld.cs` — streamed roads, buildings, crowds, particles, hazards and realm transitions
- `RuntimeAssets.cs` — procedural textures, materials and primitive pooling
- `InteractivePresentation.cs` — interactive Canvas UI and generated audio
- `WorldDirectorClient.cs` — HTTPS blueprint client with strict validation and offline fallback

### Build system

`Assets/Scripts/Editor/RemasterBuild.cs`

The editor build method creates the playable scene and launcher icon automatically, then configures:

- Android ARM64
- IL2CPP Release
- Vulkan first, OpenGL ES 3 fallback
- Linear color space
- LZ4HC compression
- High managed-code stripping
- Landscape orientation
- Package ID `com.runrealms.remaster`

### Server options

`Server/world-director`

- Secure Node.js HTTPS backend template
- OpenAI Responses API using `gpt-5.6` by default
- High reasoning effort
- Strict JSON Schema output
- Cache and per-IP rate limiting
- Independent validation before returning a blueprint
- API key remains server-side

`Server/ugs`

- Unity Cloud Code gateway example
- Server-authoritative access-policy template
- Designed for Unity Authentication, Cloud Save, Leaderboards and Remote Config integration

The model only chooses bounded realm order, pacing and a short theme. It cannot send executable code, arbitrary geometry, shaders, dialogue or assets to the game.

## Open the project

1. Install Unity Hub.
2. Install Unity `6000.3.18f1` with Android Build Support, Android SDK/NDK and OpenJDK.
3. Open the `runrealms2` directory as a Unity project.
4. Select **RUN REALMS 2 → Generate Preview Scene**.
5. Enter Play Mode.

The runtime bootstrap also creates the game automatically in an empty scene.

## Build Android locally

Select:

`RUN REALMS 2 → Build Android APK`

Default output:

`Build/Android/RUN-REALMS-2.0-Remaster.apk`

For command-line builds:

```bash
Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath ./runrealms2 \
  -executeMethod RunRealms2.Editor.RemasterBuild.BuildAndroid \
  -logFile -
```

Set `BUILD_PATH` to override the APK output location.

## GitHub Actions requirements

The workflow always checks the server code and packages the Unity source. Android compilation runs only when one of the following is configured in repository Actions secrets:

- `UNITY_LICENSE`, or
- `UNITY_EMAIL` and `UNITY_PASSWORD`

The build job runs Unity EditMode tests, builds the APK, generates SHA-256 and publishes the release after merge to `main`.

## Connect the advanced world director

1. Deploy `Server/world-director` to an HTTPS Node.js host.
2. Set `OPENAI_API_KEY` only in that server environment.
3. Set `OPENAI_MODEL=gpt-5.6` or another compatible model.
4. Put the deployed `/v2/blueprint` URL in `Assets/StreamingAssets/remaster-config.json`.
5. Rebuild the APK.

For a Unity Gaming Services setup, deploy `Server/ugs/GenerateWorld.js` with the UGS CLI and route authenticated calls through Cloud Code.

## Art direction status

This repository generates its own low-poly 3D people, textures, structures, effects and audio so the remaster is self-contained and legally clean. For a production visual target, replace the generated character and environment factories with licensed rigged characters, animation clips, PBR environment kits, VFX Graph effects, professionally mastered audio and remote Addressables bundles. The gameplay and server architecture are designed so those upgrades do not require rewriting the core run loop.

## Release status

This is a remaster engineering preview. It is not yet a production-signed Google Play release. Store publication additionally requires a private production keystore, Play Console access, final licensed art/audio, device testing, privacy declarations, screenshots and store listing materials.
