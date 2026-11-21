# HekaSomi Translation Lab

A Windows desktop application for comparing speech translation pipelines and building owner-reviewed personality constructs. Written in **C# / .NET 10, WPF and MVVM**.

**Version 0.3 — learn through participation.** Compare an approved personality's predictions with your own sealed answers, run several model providers or exchange a task with your personal agent, and inspect platform game events and cohort results. The Windows package still includes Whisper, its multilingual model and the .NET runtime. See [participation setup](docs/PARTICIPATION_GUIDE.md), [shared contracts](docs/PARTICIPATION_CONTRACT.md) and [changes](docs/CHANGELOG.md).

## Start on Windows

1. Extract the **entire** Windows ZIP into a new folder. Open **HekaSomi.Lab.exe**.
2. On **Start**, click **Try sample**. This runs real speech recognition with the included model.
3. Select a microphone, click **Record my voice**, then **Stop recording** and **Transcribe audio**. Or click **Choose audio file**.
4. Review and correct the words, then use **Copy text** or **Save text file**.

No separate Whisper installation, model download, account, API key, SDK or Visual C++ runtime installer is needed for speech testing. The primary target is **Windows 11 x64**. The model runs on CPU. This is an unsigned development build; native Windows UI, microphone and DPAPI acceptance checks are still pending.

**Translate speech into English** enables Whisper's built-in audio-to-English mode. Other target languages, personality style and AI suggestions use the optional language-model connection below. Manual personality capture and questionnaire scoring work without Ollama.

Microphone quick tests stop after 2 minutes. Imported audio may be up to 10 minutes and 250 MB. WAV/MP3 and formats supported by Windows decoders (such as M4A/AAC/FLAC) are converted to 16 kHz mono PCM copies. Original files remain untouched. Quick-test audio is removed when cleared or on normal app close; abandoned app WAVs are cleared on the next launch.

For the engineering demo, open **Compare → Load demo → Run A / B comparison**. Its replay outputs are fixed fixtures with planted errors and no inference timings. The Start sample uses actual inference. Neither demonstrates personality validity or general model accuracy.

## Updating from version 0.1 or 0.2

Close the old app and extract this update into a new folder. Existing profiles, runs and reviews remain in the same Windows-user data folder. No database copy is needed. Keep the old app folder until the new version passes your Windows smoke test. Revocation now rejects drafts and already-revoked versions by checking the stored status.

## What works

| Workspace | Implemented behavior |
|---|---|
| Start | Included speech recognition and English audio translation; sample, record or import; microphone level and timer; conversion; copy/save text. |
| Translation bench | Import/export JSON datasets; add text or supported audio samples; waveform, playback and seek; alternating A/B order; cancellation; per-case errors; WER, CER and measured offline durations; p95 summaries; audio/dataset fingerprints; JSON, CSV and HTML export. |
| Personality studio | Seven interview modules with follow-ups; typed answers or microphone recording; local Whisper transcription; evidence-linked manual or model-suggested claims; explicit owner review; 50-item IPIP scoring; draft, approval, revision, revocation and deletion. |
| Evaluation | Seal new scenario questions and T0 answers; run neutral/style/interview/questionnaire/combined conditions; freeze predictions; record one later T1 answer; show match rates and owner retest agreement separately. |
| Participation | Optional test invitation after approval; 12-question practice test or custom instrument; sealed owner answers; Meta/OpenAI/Grok/Claude/Gemini/Ollama and compatible API adapters; external agent task exchange; per-question matches, coverage, model agreement; platform report and game-event imports; matching global-cohort client. |
| Run history | Encrypted saved runs; inspect source and translation; enter adequacy/style/critical-error reviews; export reviews separately with a report. |
| Setup | Included speech checks; optional Ollama connection; baseline/candidate models; advanced custom paths; explicit remote HTTPS opt-in. |
| Automation | Headless comparison CLI, meaningful failure exit codes, Windows build/publish script and GitHub Actions workflow. |

**Not implemented in this desktop package:** hosting games/meetings, live streaming captions or camera tracking, diarization, automatic COMET/BLEU scoring, live caption timing, GPU/CPU telemetry, voice cloning/TTS/avatar animation, a validated population personality measure, a multi-rater study portal, signed MSIX/auto-update, or the web platform's hosted endpoints and provider connector registrations. Global results require the web implementation; no fabricated average is bundled. The web and Android plans in `docs` describe that integration. API adapters have automated protocol tests; real provider accounts and native Windows acceptance remain to be checked.

## Optional translation and personality models

Included Whisper handles speech recognition and audio-to-English translation. To translate into other languages, apply personality style, generate provisional claims or predict held-out responses:

1. Open **Setup → Get Ollama for Windows**, or install [Ollama](https://ollama.com/) yourself.
2. Install a language model in Ollama, then click **Connect / refresh models** in HekaSomi.
3. Choose baseline and candidate models, then **Save setup**. The default server is `http://localhost:11434`.
4. On **Compare**, choose **Translate text · language model** or **Translate audio · speech + language model**.

Choose the same model in both arms to isolate personality style. Text mode uses source text, never the reference transcript, and reports no ASR score. Audio comparisons normalize recordings before inference and fingerprint the normalized audio actually used. The desktop shares the speech model between arms; separate CLI configurations can use different Whisper models.

Start speech testing works even if Ollama is unavailable. The app does not install/start Ollama automatically, and no large language model is bundled. Remote servers require HTTPS and explicit opt-in; authentication gateways are not supported.

For a custom speech engine or model, open **Setup → Advanced**. **Restore included speech** returns to the bundled defaults. [NATIVE_SPEECH_BUILD.md](docs/NATIVE_SPEECH_BUILD.md) records provenance, checksums, licenses and the native build.

## Build a personality construct

1. Open **Personality → New construct**, give it a name and confirm the owner's consent.
2. Work through relevant interview modules. Select a microphone and the spoken language (or automatic detection). Transcription uses included speech without Ollama. Enter an answer or select a microphone, record, stop, transcribe and correct the transcript. Click **Save interview answer**. A pending recording must be saved or discarded before leaving.
3. Select an evidence row and **Add claim from selected answer**, or use **Suggest with local model**. Model suggestions remain unapproved. Edit the statement, context and source; check Approved only when the owner agrees.
4. Optionally complete the public-domain IPIP items using the displayed 1–5 anchors. Click **Calculate raw scores**. All ten answers are required to score each domain; missing responses are never imputed. Scoring is raw and descriptive, not a diagnosis or population percentile.
5. Confirm the owner's review and **Approve version**. Approval requires at least one supported approved claim or a complete questionnaire domain. Partial survey-only constructs therefore cannot be approved unless a domain is complete.
6. On the bench, refresh constructs, choose the approved version, enable candidate personality style, and use a live engine. Compare against the same model without style before changing other variables.

Approved and revoked versions cannot be edited in place. **New revision** creates a separate draft; **Revoke version** prevents future use. Approval of a new revision does not automatically revoke an older one. Review the displayed version and explicitly revoke any older version that should no longer be used.

Translation receives only preferred style and approved Communication claims. It does not receive the life-story transcript, values claims or questionnaire responses. These instructions guide the model; they do not prove that meaning will be preserved. Review negation, numbers, names, uncertainty and commitments independently.

The research predictor can use the approved interview and questionnaire according to the chosen ablation condition. It is an experimental prediction tool, not a public-facing autonomous avatar.

## Evaluate without leaking the answers

On **Evaluation**, refresh and select an approved construct, then load its owner's cases. Enter a **new** scenario and distinct options, enter the owner's exact T0 option, confirm it was held out, and seal the case.

The prediction service accepts only the question, options, compiled construct context and model name. It has no answer-key parameter or repository dependency. T0 is stored separately and cannot be rewritten. The UI attestation cannot detect material someone has manually copied into an interview; separation of capture and evaluation still needs a study protocol.

**Evaluate five conditions** stores each result as it completes. Cancellation retains finished predictions. Re-running fills missing conditions rather than sampling replacements. A construct version cannot mix model names; use the original model name or a new revision. Model tags may change server-side: pin the actual model build outside the application for controlled studies.

Collect T1 later at the planned interval without showing the owner their prior answer. Only one T1 entry is allowed. The app reports T0 match, T1 match and human retest agreement separately. Results are for one owner and the tested cases; there are no population confidence intervals, significance claims or proof of fidelity. A new revision may already have been influenced by previous case results; use new held-out cases for confirmatory evaluation.

## Build from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The solution pins SDK `10.0.404` with patch roll-forward. Use an IDE that supports .NET 10, or the command line. On Windows, from the repository root:

```powershell
.\scripts\Build.ps1
```

This verifies included speech files, restores locked dependencies, builds the solution, runs tests and publishes the portable app to `artifacts/windows-x64`. To run the source build:

```powershell
dotnet run --project src/HekaSomi.Lab.Desktop/HekaSomi.Lab.Desktop.csproj
```

The desktop uses `EnableWindowsTargeting` for cross-compilation. Linux can compile it but cannot run WPF. Portable Core, Infrastructure, Runner and tests can execute on Linux.

```powershell
dotnet build HekaSomi.Lab.sln -c Release -m:1
dotnet test tests/HekaSomi.Lab.Tests -c Release -m:1
dotnet run --project src/HekaSomi.Lab.Runner -- compare --manifest samples/demo.json --baseline-config samples/baseline.json --candidate-config samples/candidate.json --output artifacts/demo-run
```

Add `--strict` to require all release evidence. Exit codes: `0` descriptive automated checks passed; `1` a comparison failed or was canceled; `2` configuration/evidence unavailable. With the demo, `--strict` returns **2**, because human translation and behavioral validation are still inconclusive. A green engineering check is not a production release approval.

For live CLI runs, copy `samples/*-live.example.json`, fill in installed model names/paths, and select those files. `--construct` accepts a complete approved construct JSON; the desktop's **Export style JSON** intentionally exports only a smaller runtime style document and is not that full construct format. The desktop can use approved stored constructs directly.

## Architecture and engineering decisions

| Project | Responsibility |
|---|---|
| `HekaSomi.Lab.Core` | Typed domain records, text metrics, run orchestration, construct compilation, questionnaires, validation calculations and WAV inspection. No UI or network dependency. |
| `HekaSomi.Lab.Infrastructure` | Model HTTP adapters, platform report client, cancellable Whisper process, encrypted SQLite document repository, dataset files and reports. |
| `HekaSomi.Lab.Desktop` | WPF XAML, CommunityToolkit.Mvvm observable view models/async commands, NAudio recording/playback, Windows DPAPI, file dialogs and composition root. |
| `HekaSomi.Lab.Runner` | Batch comparison and report export using the same core engine. |
| `HekaSomi.Lab.Tests` | xUnit unit/integration tests for metrics, privacy boundaries, immutability, encryption, cancellation, file handling and adapter contracts. |

Dependencies are wired at the application composition root. View models contain workflow state; XAML handles presentation; code-behind is limited to window lifecycle/navigation guards. The version-1 local repository uses parameterized `Microsoft.Data.Sqlite` commands and encrypted JSON documents, rather than adding EF Core before the data model stabilizes. `PRAGMA user_version=1` records the schema; future migrations need explicit version handling.

Scores use NFKC normalization, lowercase, punctuation-to-space and whitespace tokenization. WER is aggregated by total reference words and may exceed 100% after insertions; an empty reference is unscored. CER operates on Unicode scalar values, excludes whitespace and is useful when word segmentation is unsuitable. Punctuation removal changes how contractions/hyphenation score. WER is not a multilingual tokenizer or translation-quality score.

Each run records configuration, environment, construct version/hash, dataset fingerprint and audio content hashes. Absolute audio paths are excluded from the fingerprint. Offline stage and total durations are measured only for live calls; they are not streaming/caption latency. p95 is linear interpolation over completed request durations, without warm-up exclusion or a confidence interval. Small datasets do not estimate production tail latency reliably.

## Data lifecycle

The app stores data at `%LOCALAPPDATA%\HekaSomi\TranslationLab`:

- `lab.db`: encrypted JSON payloads inside SQLite; document kind, ID, status and update timestamp remain visible metadata.
- `media`: DPAPI-encrypted recordings, tied to the current Windows user.
- `temporary`: plaintext WAV only while recording or playing/transcribing encrypted audio; leftover app WAVs are cleared on next startup. Whisper also creates temporary transcript files and removes them on normal completion/cancellation; a process or system crash can leave temporary files.
- `connections.json`: model endpoint/name/path preferences, without credentials.

Deleting a construct removes all its versions, linked runs and human reviews, predictions, local participation sessions/responses, unshared owner validation cases/answers, and owned recording files. Platform imports are independent copies: remove them with Participation's Remove local imports action. A new HTTPS snapshot replaces the prior online snapshot, including records removed on the platform. Imported WAVs, independently exported reports and external backups are not deleted. DPAPI does not protect against malware running as the same Windows user. There is no portable encrypted-backup/recovery feature in this version.

CSV exports guard spreadsheet-formula prefixes; HTML escapes user text. Exports are readable files containing research material. No telemetry, automatic upload, cloud synchronization, or external deployment runs in the background.

## Verification and next milestone

See [BUILD_VALIDATION.md](docs/BUILD_VALIDATION.md) for actual build/test evidence and [WINDOWS_SMOKE_TEST.md](docs/WINDOWS_SMOKE_TEST.md) for the remaining native checks. The full research protocol, acceptance criteria and deployment sequence remain in [DEVELOPMENT_PLAN.md](docs/DEVELOPMENT_PLAN.md).

The next acceptance milestone is a Windows smoke test with your actual microphone and local models, followed by a small consented pilot with independently held-out cases and bilingual raters. Research review and measured evidence determine whether the personality feature is useful; a running application alone does not establish that.

## Research and attribution

The plan preserves the distinction between Park et al.'s [2024 original preprint](https://arxiv.org/abs/2411.10109v1) and the [2026 revision](https://arxiv.org/abs/2411.10109v3). Their interview-grounding and held-out evaluation motivate the design; this shorter capture workflow does not reproduce their study.

The 50 English [IPIP Big Five factor markers](https://ipip.ori.org/newBigFive5broadKey.htm) use the published scoring keys and [public-domain permission](https://ipip.ori.org/newPermission.htm). Item order is interleaved; item IDs/keys are preserved by the application. Seven interview prompts are original, exploratory prompts requiring psychological review before a confirmatory study.

See [THIRD_PARTY_NOTICES.md](docs/THIRD_PARTY_NOTICES.md), `docs/licenses`, and `assets/whisper/licenses` for notices. Whisper Base Q5_1 is bundled with its license; no large language model is included.
