# Participation in HekaSomi Lab 0.3

This workspace lets you test how closely a model using your approved profile predicts your choices. It also reads authorized game/test results from the future HekaSomi platform integration. The existing speech, translation, interview and research Evaluation workspaces remain available.

## Take a comparison test

1. In Personality, create or select your own profile, review its evidence and approve it. Approval offers an optional test. You can also open Participation manually and refresh profiles.
2. Select an approved profile. Use the included **Everyday choices** practice test, or import a versioned test JSON. The sample instrument is an original demonstration, not a validated psychological examination.
3. Answer every question. Confirm that these questions and answers were not used to build the profile, then seal the answers. The agent never receives that answer key. Do not test a personal agent that has already seen the answers and treat its score as a blind comparison.
4. Select the sealed session, a model provider and a model you actually have access to. Choose a condition: Neutral, StyleOnly, InterviewOnly, QuestionnaireOnly or Combined.
5. Preview the exact profile context and confirm sharing it with that provider. It contains selected approved claims/style and, where applicable, aggregate questionnaire scores. It excludes raw interview stories, audio and the test answer key.
6. Run the comparison. Cancel stops further requests and preserves completed results. Running the same settings again resumes missing questions rather than replacing prior predictions.

The dashboard displays **match percentage**, coverage, completion and each question's human/model choices. Here “correct” means the option matches your choice; a different preference is not a factual error. The primary score is matching choices divided by all test questions. Failed, missing, timed-out, invalid and abstained questions count as nonmatches; coverage makes this visible. Model-to-model agreement uses only questions both models answered and shows that denominator.

To compare models, keep the same sealed session and change the model/provider. For personality-condition comparisons, keep the model/settings fixed and compare Neutral with Combined or other conditions. Use the existing Evaluation workspace for its separate T0/T1 retest workflow; a retest-normalized score is not added to the new Participation score.

## Connect a model API

These are developer API integrations, separate from logging into a personal-agent subscription. Model names are deliberately not hard-coded: enter a currently available model ID for your account.

| Selection | Credential environment variable | Transport |
|---|---|---|
| Meta Muse models | `MODEL_API_KEY` | Responses |
| OpenAI | `OPENAI_API_KEY` | Responses |
| Grok | `XAI_API_KEY` | Responses |
| Claude | `ANTHROPIC_API_KEY` | Messages |
| Gemini | `GEMINI_API_KEY` | OpenAI-compatible chat completions |
| Ollama | None for ordinary localhost use | Ollama chat |
| Custom compatible API | `HEKASOMI_AGENT_KEY`, or your own variable name | Select supported protocol |

Set the variable through Windows' user environment-variable settings, then restart Lab. The field in Lab is the **variable name**, not the key. Do not paste a key into the model, URL or variable-name fields. Lab stores connection metadata, not the secret value. Treat environment variables as local credentials, not a secure vault. The default request deadline is 90 seconds and the maximum output is 2,048 tokens per question; both are adjustable. Provider-side charges and limits apply.

Hosted endpoints must use HTTPS; HTTP is accepted only for loopback. Redirect following is disabled in the app's shared HTTP client. API adapters have automated contract tests, but no paid provider call was made in this delivery. Model-specific access and parameter support must be checked with a real account. The ordinary OpenAI adapter does not implement the distinct Sign in with ChatGPT plan-usage flow.

## Use your existing personal agent today

1. Select **Any personal agent · manual** and enter a descriptive model/agent label.
2. Preview the approved context and confirm sharing. Choose **Export agent task**.
3. Give the JSON file to your agent. Ask it to follow the packet's instructions and return a JSON answer file. The packet includes the questions, profile context, run identity and a supplied `packetHash`; it omits your answers.
4. Import that answer file. Every question must appear exactly once, with an exact option string or `null` to abstain. Incorrect packet IDs/hashes, unknown choices and second imports are rejected.

These results are labeled `external-manual` and unverified. This route works as file/paste exchange where an agent can return text; it does not automatically connect a subscription or make the agent a game player. The web plan adds authenticated tools for Muse, dots, Grok Bot, Claude and other capable agents to participate directly. Provider approval/account capabilities remain required.

## Platform tests, games and global averages

Until the web implementation exposes the endpoints in `PARTICIPATION_CONTRACT.md`, use **Import platform report** with a valid file. The supplied `samples/participation/synthetic-platform-report.json` is explicitly synthetic and produces 50% match and 100% coverage. File imports do not contribute to, or establish, a global average.

Once staging is implemented, obtain a short-lived read-only Lab token for your authorized data, set `HEKASOMI_LAB_TOKEN`, restart Lab, confirm the platform HTTPS address, and fetch its report. Fetch replaces the previous online snapshot so platform deletions are reflected. Independent imported files remain until you remove local imports. The game-event table includes join/leave, accepted/rejected actions, timeouts, stop and test events without raw room transcripts.

Select a result and fetch the global average. The response must match its exact test, language, provider/model, settings identity, condition, context compiler, evidence route and scoring policy. Each consenting owner has equal weight; only the first completed attempt per owner/cohort is eligible. Cohorts below 10 owners are suppressed. Lab displays the server's timestamp and owner count. No real global population data is included in this release.

## Data and stopping

New sessions, responses and imports use the existing encrypted SQLite document store. Revoking a local profile blocks new requests and late-result commits. Deleting a local profile also deletes its local Participation sessions, runs and answers. Remove platform copies separately using **Remove imported reports**. Exported files remain readable independent files, and comparison exports contain your answers. An exported task already shared with a provider cannot be recalled by local revocation; use that provider's controls for retained data.

The platform's live game stop, OAuth connection, hosting and account deletion behavior are implementation work for the web/Android plans, not hidden services running inside Lab.

## Headless verification

From a source build:

```powershell
dotnet src/HekaSomi.Lab.Runner/bin/Release/net10.0/HekaSomi.Lab.Runner.dll participation-fixtures --output artifacts/participation-fixtures
dotnet src/HekaSomi.Lab.Runner/bin/Release/net10.0/HekaSomi.Lab.Runner.dll participation-score --report artifacts/participation-fixtures/synthetic-platform-report.json --output artifacts/participation-scores
```

The second command recomputes scores from the observations, labels them file-unverified, and never invents a global mean. Invalid inputs return exit 2. See the contract's hash fixture for a cross-language implementation.
