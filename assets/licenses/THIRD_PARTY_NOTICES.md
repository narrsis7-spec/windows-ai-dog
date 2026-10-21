# Third-party notices

## Dog sound recordings (CC0 1.0)

Each source page was checked for CC0 1.0 on 2026-10-09. The authors do not endorse this app.
License: https://creativecommons.org/publicdomain/zero/1.0/

- 20141026 Chihuahua Barking Licking 01 — kijjaz — https://freesound.org/people/kijjaz/sounds/252909/ (`assets/sounds/bark.wav`)
- Dog licking.wav — slv1 — https://freesound.org/people/slv1/sounds/461564/ (`assets/sounds/lick.wav`)

The WAV files are trimmed, faded and resampled excerpts; exact edits and hashes are in `sounds.csv`.

## Hermes (MIT)

Provider catalog and authentication classification ideas in
`src/WindowsAiDog.Windows/Chat/Providers/` are an independent C# adaptation of ideas from
NousResearch/hermes-agent (commit 7612d1e9e3eb40954ff610eeb1eeee8e27a9eb01,
`hermes_cli/provider_catalog.py`). Copyright (c) 2025 Nous Research. The MIT license text is in
`hermes-MIT.txt`. No Hermes code, runtime, endpoints or credentials are bundled.

## Artwork

All dog artwork is AI-generated for this project; no third-party or stock imagery is included.

- `assets/sprites-v9/` (current runtime art): video clips generated with Google Flow
  (Nano Banana / Omni) from a generated base image, then keyed and packed into sprite sheets with
  `tools/flow-matte.py`, `tools/flow-sheet.py` and `tools/import-flow-sprites.py`.
  Made with Google Flow; the generated outputs include invisible SynthID watermarks.
- `assets/sprites/`, `assets/sprites-v6` to `assets/sprites-v8` (older fallback sets used by the
  tests): generated with OpenAI image generation.
- `assets/dog.ico` is a resized frame of the older idle art.

Use of generated output is also subject to the terms of the service that produced it.
