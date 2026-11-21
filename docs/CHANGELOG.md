# Version 0.3

- Add Participation: optional post-approval test invitation, versioned instruments, sealed answers, frozen model runs and per-question comparisons.
- Add Responses, Chat Completions, Claude Messages and Ollama adapters; explicit provider presets; bounded responses, cancellation, request deadlines and sanitized failure records. API credentials stay in environment variables.
- Add personal-agent task export and atomic answer import with packet binding. Human answers and raw interview records are excluded from task context. Manual results retain unverified provenance.
- Add exact-choice agreement, coverage, missing/failed result accounting, model-to-model agreement and a first-attempt/equal-owner global aggregation implementation for trusted server data.
- Add HTTPS platform report/cohort clients, current-snapshot replacement, local import deletion and game-event dashboards. Hosted endpoints and live subscription connectors are specified in the web plan, not deployed by this package.
- Preserve approval/revocation rules and recheck them before generation and when committing results; cascade local participation deletion with profile deletion.
- Add CLI fixtures/scoring, shared contract/hash documentation, regression tests, Windows acceptance steps and web/Android implementation handoffs.

# Version 0.2

- `RevokeAsync` checks the stored construct status and permits only Approved → Revoked. Regression coverage rejects drafts and repeat revocation.
- Bundle a Windows x64 Whisper CLI and multilingual Base Q5_1 model, with SHA-256 integrity checks and licenses. The native CLI uses a static C++ runtime and standard Windows components.
- Add Start: real sample recognition, microphone selection/refresh, recording level and timer, automatic two-minute stop, file import, language selection, English audio translation, copy/save text, cancellation and error details.
- Convert supported imported audio to 16 kHz mono PCM temporary copies; retain originals. Limit imports to 250 MB and ten minutes.
- Make optional Ollama setup explicit; included speech and personality interview transcription work without it. Keep custom engine configuration in Advanced.
- Guard unsaved personality edits and pending answer text; add clear IPIP response choices and profile version/status labels.
- Improve comparison labels, new-dataset action, full result inspection/copy, export-folder shortcuts and history review state.
- Add native Windows speech smoke verification to CI, rebuild provenance and updated acceptance instructions.

The original research, encryption, consent, approval and evaluation boundaries remain in place. Voice cloning, streaming captions, signed installation and product integration are still future work.
