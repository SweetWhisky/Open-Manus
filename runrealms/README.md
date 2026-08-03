# RUN//REALMS v1.0.0

RUN//REALMS is an offline-first landscape endless runner with deterministic seeded worlds, adaptive pacing and an optional server-only AI blueprint service.

## Gameplay

- Three-lane swipe movement, jump, slide and focus slow-time
- Eight realms: Neon Rain City, Desert Megacity, Flooded Kingdom, Zero-G Station, Overgrown Lab, Frozen Megastructure, Volcanic Foundry and Celestial Ruins
- Deterministic seeded realm order and replayable routes
- Validated obstacle patterns that always retain a playable lane
- Adaptive intensity with recovery bias after mistakes
- Shield, magnet and boost power systems
- Shards, combos, near-miss scoring, daily missions, levels and local unlocks
- Same-seed replay and clipboard sharing

## Performance

- Fixed-step 60/120 Hz simulation selected from the frame-rate target
- 30, 60, 90 and 120 FPS rendering targets
- Automatic sustained-frame-time resolution scaling
- Performance, Balanced, High and Auto quality profiles
- Pooled hazards and pickups with a 96-object fixed pool
- Device-pixel-ratio caps, bounded particles and optional shadows
- Hardware-accelerated immersive Android WebView
- Uncompressed game assets for faster startup
- Code minification and Android resource shrinking

## Accessibility and privacy

- Reduced motion, high-contrast hazards and haptic controls
- Core gameplay works offline and requires no account
- No ads, payments, location, contacts, microphone, camera or behavioral tracking
- Optional mature visual theme is adult-only, consensual, non-explicit and disabled by default
- All progression data stays on the device

## Advanced AI director

The optional `backend/` service uses the OpenAI Responses API through a server-side environment key. Its output is constrained by strict JSON schema, validated again by the service and validated a third time by the game before being converted into predefined realm and pattern settings. The APK never contains an OpenAI API key and always retains its deterministic offline director.

## Validation and publishing

The GitHub Actions workflow:

1. Checks all JavaScript and backend syntax.
2. Runs deterministic and fairness tests across 500 generated patterns.
3. Runs Android release lint.
4. Builds the optimized test-signed APK.
5. Verifies the APK signature.
6. Generates a SHA-256 checksum.
7. Packages the web preview and source archive.
8. Publishes the assets to the `run-realms-v1.0.0` GitHub Release after merge to `main`.

This is a direct sideload QA release, not a Google Play production-signed package.
