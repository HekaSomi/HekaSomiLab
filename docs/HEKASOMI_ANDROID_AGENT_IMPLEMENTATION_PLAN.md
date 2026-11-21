# Heka Translate Android — participate, play and compare

Implementation handoff · November 20, 2026 · Companion to `HEKASOMI_WEB_AGENT_IMPLEMENTATION_PLAN.md` and HekaSomi Lab v0.3.

## Outcome

Keep Heka Translate's immediate value: people can understand one another through translated captions. Add the same approved personality and agent participation experience as HekaSomi.ai, with a mobile interface suited to joining rooms, inviting an agent, playing a turn and reviewing results.

The phone should not become a background agent farm. The server owns active game sessions and authorization; the external personal-agent provider or an explicitly selected inference route runs the agent. The camera/microphone pipeline remains independently useful.

## Starting point and limits of this review

The public website advertises camera-based subtitles beneath speakers, virtual meetups and a mobile app marked “coming soon.” Previous project context describes local ML Kit translation and selection of Whisper Tiny for language identification/shared native portability. Those descriptions do not establish the exact state of today's Android repository. No APK, Gradle project or Android device was supplied for inspection.

The delivered Windows Lab uses a bundled Whisper Base Q5_1 speech model; that is not an instruction to replace the Android app's model. Preserve the actual on-device architecture and verify model choice, audio buffering, package language, supported Android versions, lifecycle and latency in code.

Your web avatar-personality work is reported complete. Android should consume that same server-owned profile/version/grant system. Avoid a second, incompatible mobile personality database or a parallel account identity.

## Paste this into the Android repository's coding agent

```text
Read this plan, HEKASOMI_WEB_AGENT_IMPLEMENTATION_PLAN.md, the delivered Lab v0.3 docs/PARTICIPATION_CONTRACT.md and fixtures, and this repository's AGENTS.md.

Inspect the actual app first: Java/Kotlin boundaries, UI framework, navigation, auth, camera/face overlay, recording and speech models, ML Kit translation, networking, persistence, Gradle/toolchain and signing configuration. Record real paths and baseline checks. Do not rewrite the app or replace its speech pipeline as a prerequisite for agent participation.

Implement phases A0–A7 below using the existing architecture. Preserve offline translation and current meeting flows. Integrate the existing web personality APIs and the new shared game/assessment contracts. Backend routes described here are proposed until confirmed in the web repository. Use an explicit unavailable state for missing endpoints; use synthetic fixtures in tests only.

Add approved profile selection, optional post-approval tests, agent invitation/connection handoff, a human-accessible game screen, labeled AI participants, stop/takeover, results and consent controls. Separate actual human captions from generated agent messages. Do not put developer API keys or provider subscription tokens in the APK, shared preferences, URLs, logs or analytics.

Use official provider connection flows. The user may tell an existing Muse/dot/Grok Bot/Claude agent to play through the shared HekaSomi connector. Native app launch and sign-in do not prove a game has joined. Observe server status and preserve resumable intent through app switches. Do not assume any subscription grants arbitrary API calls.

Run relevant Gradle/unit/instrumentation/lifecycle/accessibility/network checks and produce an installable debug build when available. Document checks that need a physical device, provider account, backend endpoint or signing access. Prepare staged rollout/rollback; do not publish to Google Play or modify production services in this task.
```

## Navigation and core journeys

Adapt the current navigation rather than imposing a new framework. A practical mobile structure is **Translate**, **Rooms**, **My profile**, and **Activity**. A small “Learn by playing” entry belongs in Rooms; do not obscure the camera or live-caption controls with promotional cards.

| Area | Mobile behavior |
|---|---|
| Translate | Existing camera/conversation captions, language choice, model-download status and offline behavior. Personality styling remains separately controlled and must preserve meaning. |
| Rooms | Browse/join/create according to existing permissions; distinguish people-only rooms from rooms accepting AI players; show game state and participants. |
| My profile | Reuse server interview drafts, transcript correction, claims review, approval/version list and avatar attachment. Add agent-use permissions and the optional assessment invitation. |
| Activity | Own test comparisons, game history, pending invitations, active agent leases, and stop controls. Global summaries are aggregate, not other people's raw answers. |

### Journey 1: join and translate

The user opens a room and participates as themselves. Microphone/camera permissions are requested only for the action that needs them. Captions remain available even if no personality or provider account is connected. If an agent joins, render a persistent AI badge and separate message type; never place generated text under a human face as though that person spoke it.

### Journey 2: profile → test

After approving a new profile version, show: “Want to compare your choices with your personality model?” Offer **Take the test** and **Later**. Record dismissal per version so it does not repeatedly interrupt translation.

Use short, readable question screens with explicit choices, progress, save/resume and a final review before sealing. The human submits before any prediction is revealed. After sealing, display an asynchronous comparison status and allow the user to return to the app. Results show “8 of 12 choices matched,” percentage, coverage and each paired answer. Different providers are separate rows/cards, with condition and model/version visible on details.

Do not send the capture questionnaire back to a model and score its reproduction as a held-out personality test. The bundled Lab pilot is a practice set; formal evaluations require an independent instrument and consistent language/version. Translating question text on the fly changes the instrument and must not retain its old hash.

### Journey 3: invite an existing personal agent

The user selects **Use with my agent**, chooses an approved profile and eligible room, reviews the small context projection, and selects duration/round limits. The server creates a pending invitation reference. Display **Copy invitation prompt** and the provider's supported open/share action.

Use Android's standard share sheet for a text prompt where supported. Check whether a destination exists before launching it. Always provide copy/open-web fallback. A share sheet sends a prompt to an app; it is not a universal API for sending a message on the user's behalf, and it does not prove the agent has acted.

The verified Grok Bot prompt scheme can prefill a message using `grokbot://app/v1/prompt?text=...`, optionally with a known Bot ID. Follow the documented 1,000-character limit and URL-encode the text. It is unsent until the user sends it. Do not invent equivalent schemes for Muse, dots or Claude; use their documented route or the copy/share fallback.

When the user returns, fetch invitation status from HekaSomi. States: pending authorization, waiting for agent, joined, playing, stopped, expired, unavailable. Show the agent's actual server-authorized participant, not a success animation triggered by returning from another app.

### Journey 4: agent initiated play

A user can ask their agent to find and join a HekaSomi game. The agent uses the shared connector under the user's HekaSomi authorization. Android receives the resulting invitation/session update. The user can join as a human, inspect the agent's selected persona version, and stop it.

If push notifications are not available or permission is declined, foreground refresh still works. The app does not need to keep a microphone active while waiting for an agent.

## Authentication and permissions

Reuse current HekaSomi auth. Use a system browser/custom tab for provider authorization rather than a credential-collecting WebView. Distinguish three operations: signing into HekaSomi, authorizing a provider agent to call HekaSomi tools, and authorizing a separately supported inference/billing route.

OAuth callback state, PKCE verifier, nonce and issuer/audience must be validated by the component responsible for the flow. App links should be verified against the HekaSomi domain with the actual Android application ID and signing certificate. Use exact callback allowlists; do not accept an arbitrary redirect from a received intent. An invite link is a short-lived reference resolved after authentication, never an unrestricted profile token.

Store refresh/session secrets using platform-backed secure storage appropriate to the existing app. Keep tokens out of logs, crash reports, backups and analytics. Do not place broad `lab:read` researcher tokens in mobile builds. Mobile users can retrieve their own results through owner-scoped APIs.

Keep separate, understandable controls for:

- Microphone/camera access.
- Saving reviewed interview evidence.
- Sharing a selected profile projection with a chosen agent/provider.
- Agent participation in a particular room/game.
- Contributing de-identified assessment results to an aggregate.

Withdrawing analytics permission must remove future eligible contributions and trigger server-side aggregate recomputation. Stopping a game does not silently delete the user's profile; deleting a profile must stop its active grants.

Official ChatGPT plan usage, if approved for HekaSomi's hosted/mobile arrangement, is a distinct integration. Do not copy the ordinary API-key request format into it: the current published preview requires streaming and has different supported fields. Keep it unavailable until registration, eligibility and account/region rules have been verified. The normal MCP-agent route remains independent.

## Android implementation boundaries

| Component | Responsibility |
|---|---|
| Existing translation/capture pipeline | Camera frames, audio, local ASR/language identification and translation; preserve current resource management. |
| Persona repository | Authenticated draft/version APIs, local draft cache where permitted, review and approval state. Server is authoritative for active versions. |
| Agent connection repository | Capability list, invitation/authorization state, grants, active leases and stop actions. No provider inference secret embedded in client. |
| Game repository | State snapshots/deltas, action receipts, expected state version, turn/deadline and reconnect recovery. |
| Assessment repository | Test definition/hash, draft answers, seal operation, run progress and owner results. No model-facing answer-key route. |
| UI state holders | Lifecycle-aware screen state, offline/pending/error UI, cancellation and navigation continuity. Use the app's existing MVVM/presenter pattern. |
| Event recorder | Minimal first-party UX/operational events with consent and deduplicated IDs; server produces authoritative game/evaluation events. |

Use an explicit `ParticipantKind` such as Human, PersonaAgent and BaselineAgent, mapped to existing server types. A game action needs `sessionId`, `actorId`, `turnId`, `expectedStateVersion`, a legal typed action and an idempotency key. Authorization comes from the authenticated session/grant, not the actor ID supplied by the phone.

The backend connector acts for external agents. Android should not implement an inbound MCP server or run a hidden forever-polling loop on the phone.

## Lifecycle, offline operation and performance

- **Process death:** Persist only permitted draft/pending intent state; reload authoritative invitations, grants and game snapshots on restart. Do not automatically replay an expired move.
- **Reconnect:** Send the last received version and resolve uncertain action receipts before retrying. Gaps request a snapshot. Duplicate events/actions are harmless.
- **Background:** Let the server and provider continue only within the explicit lease. Use approved notification/background mechanisms for user-visible updates. Do not use a permanent foreground service merely to keep an agent alive.
- **Offline translation:** Existing downloaded translation resources continue working as supported. Show online requirements only on agent/room features.
- **Offline assessment drafts:** Permit local drafts if secure storage is already available; seal only against the current server profile and test version. Resolve conflicts explicitly.
- **Stop/takeover:** Attempt immediate server stop, display acknowledged state only after receipt, and show pending status offline. Server lease expiry supplies the bounded fallback; the phone cannot guarantee remote stop while disconnected.
- **Thermal/battery:** Avoid simultaneous camera processing, local speech inference, game rendering and unnecessary polling. Observe frame/caption latency, dropped buffers, memory and thermal behavior on the target phone class. Keep heavy inference off the UI thread.
- **Audio:** Handle audio focus, calls, Bluetooth changes and permission revocation. Agent TTS, if later added, must not feed back into human ASR as though it were a new human utterance.

For ML Kit, preserve model download management and release translator resources when no longer needed. For Whisper, verify the installed model/backend, native ABI and cancellation strategy. Do not change those based on the Windows package's defaults.

## Results and Lab interoperability

The server uses the same scoring policy as Lab: exact matches over all eligible questions, separate coverage, missing/failed/abstained outcomes retained, and one eligible completed attempt per owner in the matched global cohort. Small cohorts show no score. Public game rounds and manual agent responses are not pooled with verified private assessments.

Android displays owner results from its ordinary authenticated API. The Windows Lab independently reads `/api/lab/v1/report` and `/api/lab/v1/cohort`. Mobile should not upload local UI-calculated percentages as authoritative results. The server recomputes metrics from committed answers/outcomes and records test/model/compiler/route identity.

Do not save ordinary caption text, room audio or camera frames as research telemetry by default. Latency/activity events need only IDs, event type, provider route and timing metadata. Any content-based study has its own consent and export controls.

## Phases and acceptance checks

| Phase | Work | Completion evidence |
|---|---|---|
| A0 | Discover current architecture/toolchain and run baseline | Repo map; build result; current local-translation behavior documented on a supported device/emulator. |
| A1 | Shared DTOs, API client, profile/version integration | Golden contract/hash tests against Lab and web; owner isolation; expired auth handled. |
| A2 | My profile actions and post-approval assessment invitation | Existing persona flows reused; prompt once per version; later/resume works. |
| A3 | Invitation, verified app links, provider handoff and status | Cross-app return/process death tested; absent provider fallback; pending versus joined accurate. |
| A4 | Human game UI, AI roster labels, turn submission and takeover | Two-client game; duplicates/stale state rejected; timeout/reconnect/stop behavior visible. |
| A5 | Private assessment and results | Seal before reveal; model requests contain no answers; match and coverage parity; no fake global data. |
| A6 | Accessibility, localization, performance and privacy | TalkBack, large fonts, contrast, right-to-left layout, long captions, offline and device-resource tests. |
| A7 | Release package, staging configuration and rollback | Debug APK when buildable, reproducible build instructions, signed-release prerequisites and test evidence. No Play Store publication in this handoff. |

Required failures to exercise: expired invitation, unauthorized room, revoked persona, app killed during OAuth, wrong callback state, no matching app, network loss after action commit, duplicate notification, provider quota exhaustion, agent response after takeover, microphone permission removed, local translation model missing, phone rotating during test, sealed answers submitted twice, and changed instrument language/version.

Suggested flags: `agent_rooms`, `agent_invites`, `personality_assessments`, `global_personality_results`, `provider_plan_usage`. Server policy always wins over a client flag. Rollback hides new entry points and stops new agent grants while retaining basic translation and meeting access.

## Education in the actual experience

Use short explanations at moments where users can test them:

- Before a comparison: “Your answers stay hidden while the model predicts.”
- After a mismatch: “A familiar style does not guarantee the same decision.”
- When switching models: “The same profile can behave differently on another model.”
- When using a personal agent: “This agent may also use memories from earlier conversations.”
- After a translation: “Meaning and personality style are assessed separately.”

Measure whether people understand these distinctions through optional learning questions and usability studies. Do not optimize only for time spent playing or a rising similarity score.

## Primary references

- [Android App Links](https://developer.android.com/training/app-links) — domain-associated links and navigation.
- [Android Credential Manager](https://developer.android.com/identity/credential-manager) — integrate with the actual existing authentication strategy.
- [ML Kit translation on Android](https://developers.google.com/ml-kit/language/translation/android) — local translation resources and lifecycle.
- [Grok Bot deep links](https://docs.x.ai/grok-bot/deep-links) — documented prefill behavior.
- [Muse connector platform](https://platform.muse.ai/docs), [Claude remote connectors](https://support.claude.com/en/articles/11176164-use-connectors-to-extend-claude-s-capabilities), [OpenAI plugins](https://developers.openai.com/plugins).
- [OpenAI plan-usage scope](https://developers.openai.com/siwc/token-sharing-open-source) and [preview limitations](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations).

For each provider, run a real-account acceptance test before displaying a “connected and ready” status. The current review establishes documented paths; it does not establish that your production accounts or applications are approved.
