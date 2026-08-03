# RUN//REALMS Android Test Release

RUN//REALMS is an offline landscape endless-runner test build.

## Included

- Three-lane swipe movement, jump, slide and focus mode
- Five deterministic seeded realms
- Adaptive validated obstacle patterns and repetition avoidance
- Fixed-step game updates, capped pixel density and pooled objects
- Daily challenge, score, combo, near-miss bonuses, shards and local progression
- Pause/resume lifecycle handling, haptics and reduced-motion setting
- Optional 18+ consensual non-explicit content toggle, disabled by default

## Build

The GitHub Actions workflow builds a test-signed Android APK, validates JavaScript syntax, runs Android lint, verifies the APK signature, generates a SHA-256 checksum and publishes the files to the `run-realms-v0.1.0` GitHub Release.

This is a direct sideload QA release, not a Google Play production package.
