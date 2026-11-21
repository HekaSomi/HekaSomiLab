# Build validation — version 0.3

Engineering checks observed November 20, 2026 on Linux x64 with .NET SDK 10.0.404. These checks establish implementation behavior; they do not establish personality validity, real provider eligibility, game fairness or production readiness.

| Check | Observed result |
|---|---|
| Locked NuGet restore | Passed; dependency lockfiles retained |
| Release solution build | Passed, 0 warnings and 0 errors; includes WPF XAML compilation |
| Windows x64 self-contained publish | Passed; executable, .NET/WPF runtime, SQLite, speech assets and samples included |
| Automated tests | **97 passed, 0 failed, 0 skipped** |
| Existing behavior | Original 66 tests retained, including approved-only revocation, speech assets, metrics and encrypted repository boundaries |
| New participation behavior | 31 additional test cases covering scoring, held-out answers, context boundaries, immutable sessions/results, revocation during inference, deletion, cancellation/resume, packet binding, report provenance/snapshots, cohorts and provider protocol contracts |
| New CLI fixtures and score | Exit 0; clearly synthetic 12-question fixture recomputed to 6/12 matches (50%) and 100% coverage |
| Independent JavaScript/C# instrument hash | Exact parity: `45769c20935779f40a2d9ebf3de032f70232b7c91dca9ad133fc091714ebebdc` |
| Existing comparison CLI | Exit 0; five cases across two arms |
| Existing strict evidence gate | Exit 2 as intended; human translation/behavioral evidence remains inconclusive |
| Native Windows UI, microphone, clipboard, audio conversion and DPAPI | **Not executed on this Linux host**; see WINDOWS_SMOKE_TEST.md |
| Live Meta/OpenAI/Grok/Claude/Gemini/Ollama calls | **Not performed in this update**; protocol adapters exercised using controlled HTTP handlers, not real accounts |
| Global average and online game data | Client, validation and aggregation rules implemented; **no real population data or deployed web endpoints supplied** |
| Muse/dots/Grok Bot/Claude subscription connectors | Web/Android implementation plans supplied; **not registered, approved or exercised live** |
| Hosted deployment, Android build, GitHub CI execution | **Not performed**; current web/Android repositories were not available |

Current logs and TRX are in `docs/build-evidence/v0.3`. Earlier logs in the parent directory and `v0.2-validation.md` are inherited historical evidence for the unchanged speech bundle, not freshly rerun claims. The test runner's informational VSTEST_ARTIFACTS_DIRECTORY fallback notice does not indicate a failed test; the TRX records all results.

The updated Windows workflow includes native speech, regression tests and synthetic participation scoring, but that workflow was not executed against a connected repository in this delivery. The portable ZIP is an unsigned development build. Complete the native acceptance checklist and real-account integration checks before release.

Both delivery archives include a SHA-256 inventory. Package validation checks ZIP CRCs, required assets/runtime files and inventory matches; this detects accidental corruption and does not replace code signing or an authenticated distribution channel.
