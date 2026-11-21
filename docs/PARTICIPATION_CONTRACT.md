# HekaSomi Participation contract v1

Implemented by Lab 0.3 in `Core/Participation.cs`, `Core/ParticipationCohorts.cs`, `Infrastructure/ParticipationStorage.cs`, `Infrastructure/AgentProviders.cs` and `Infrastructure/PlatformClient.cs`. The web and Android implementations must share these golden fixtures and scoring rules. This document specifies the new contract; it does not claim that production endpoints already exist.

## Encoding and identities

Use UTF-8 JSON, camelCase property names, enum strings in the exact case shown below, ISO-8601 timestamps with an offset, and finite JSON numbers. No comments or trailing commas. Unknown schema versions must fail closed. C# currently tolerates additional fields; do not repurpose fields or change semantics in v1. Servers should validate required fields explicitly rather than rely on DTO defaults.

| Enum | Values |
|---|---|
| `PersonaCondition` | `Neutral`, `StyleOnly`, `InterviewOnly`, `QuestionnaireOnly`, `Combined` |
| `ResponseState` | `Answered`, `Abstained`, `Invalid`, `Failed`, `TimedOut` |
| `AgentTransport` | `Responses`, `ChatCompletions`, `AnthropicMessages`, `Ollama`, `ExternalAgent` |

Use opaque unique IDs, never email addresses as owner IDs. The server derives owner identity from authentication. Lab IDs are generally 32-character UUID hex strings, but the wire contract allows nonempty strings. Never accept a client-supplied owner ID or claimed provider as evidence of identity.

## Test instrument

```ts
type TestItem = { id: string; question: string; options: string[] };
type ParticipationTest = {
  schema: "hekasomi.participation-test/1";
  id: string; version: string; language: string; title: string;
  items: TestItem[];
};
```

Require 2–100 questions, unique nonempty case-sensitive question IDs (maximum 100 characters), nonempty question text (maximum 4,000 characters), and 2–10 distinct nonempty option strings (maximum 1,000 characters each). Preserve order, whitespace and Unicode exactly. Options are the choice identifiers in v1; changing text or translation changes the test hash. Do not translate an instrument dynamically within a cohort. `language` identifies the instrument language, not the user's locale.

### Portable test hash

SHA-256 of concatenated **length-prefixed UTF-8 strings**, rendered as lowercase hex. Each length prefix is the byte count encoded in 4-byte big-endian form. No BOM, separators or Unicode normalization. Counts are decimal ASCII strings without leading zeroes.

Append fields in this exact order:

1. `hekasomi.test-hash/1`, schema, id, version, language, title, number of items.
2. For each item in supplied order: id, question, number of options, each option in supplied order.

`samples/participation/hash-fixture.json` contains a test and its C#-generated `expectedHash`. `scripts/verify-participation-hash.mjs` independently computes that value in JavaScript. A second unit-test golden value uses id `test`, version `1`, language `en`, title `Synthetic test`, two items `q1/First?/A,B` and `q2/Second?/A,B`: `1b14f4f45ba0c1383b0ed86f0265d790daf77d88a1413115b498cd220e2b1ba2`.

## Sealed human session

```ts
type AssessmentSession = {
  id: string; ownerId: string; constructId: string; constructVersion: number;
  constructHash: string; test: ParticipationTest;
  humanAnswers: Record<string, string>;
  sealedAt: string; heldOutAttested: true;
};
```

Every item has exactly one exact-option human answer; no extra answer IDs. The local store allows one sealed session per approved construct version and test hash. The platform should enforce the corresponding owner/version/test uniqueness transactionally. Seal before model generation or result reveal, and deny agents and generation workers access to `humanAnswers`.

`constructHash` binds the approved immutable profile snapshot. Lab computes it from the exact UTF-8 output of its own `JsonDefaults.Write(profile)`. A platform using another profile representation must maintain its own stable approved snapshot digest and pass that digest through; do not compare unrelated JSON serializations. Lab never imports a platform report as a locally runnable construct. `heldOutAttested` is an assertion; it is not proof the subject/agent never saw a question. Verified platform cohorts need server-side training/evaluation separation and an explicit memory condition.

## Connection, run and responses

```ts
type AgentConnection = {
  provider: string; model: string; transport: AgentTransport; baseUrl: string;
  credentialEnvironmentVariable: string; timeoutSeconds: number;
  maxOutputTokens: number; apiVersion: string;
};
type ParticipationRun = {
  id: string; sessionId: string; connection: AgentConnection;
  condition: PersonaCondition; compilerVersion: string; contextHash: string;
  createdAt: string; evidenceKind: string;
};
type ParticipationResponse = {
  runId: string; itemId: string; state: ResponseState; choice: string | null;
  actualModel: string | null; elapsedMs: number | null; errorCode: string | null;
};
type EvaluationObservation = {
  session: AssessmentSession; run: ParticipationRun; responses: ParticipationResponse[];
};
```

The connection contains no credential value. Timeout is 5–300 seconds; maximum output is 128–8,192 tokens. Hosted URLs use HTTPS; HTTP is permitted only for loopback; no URL credentials, fragments or query strings. ExternalAgent has no inference endpoint. Lab's preset provider identifiers are `meta`, `openai`, `grok`, `claude`, `gemini`, `ollama`, `custom` and `any`.

The connection identity uses the same length-prefix SHA-256 algorithm with these fields: `hekasomi.connection-hash/1`, provider, model, transport enum string, baseUrl with trailing `/` characters removed, timeoutSeconds decimal, maxOutputTokens decimal, apiVersion. Credential variable name/value is excluded. URLs otherwise retain exact spelling. The resulting digest is `connectionIdentity` in cohort queries; it is not a serialized connection property. Normalize settings once before freezing a run. A server should map each trusted provider/configuration to an approved stable identity rather than accept arbitrary client endpoints into a verified cohort.

The compiler is `participation-claims-v1`. It selects approved evidence-linked claims, preferred style and/or aggregate IPIP raw domain scores according to condition. It excludes raw source interviews, recordings and item-level questionnaire answers. `contextHash` is SHA-256 of the exact UTF-8 context string. The platform should use this exact compiler if it advertises that version; a different compiler must get a different identifier and cohort. Changes to provider/model, settings, condition, compiler or memory availability create a distinct experimental condition.

`api-model` denotes Lab API inference; `external-manual` denotes manually supplied personal-agent answers; `synthetic-demo` denotes shipped fixtures. A future authenticated subscription-agent run needs its own evidence kind, such as `subscription-agent`, and cannot be combined with API-only runs. The platform must verify provenance itself. User-edited files remain unverified even if their `evidenceKind` claims otherwise.

An Answered result needs an exact supplied option. All other states require null choice. Item and run IDs must match, each item appears at most once, elapsedMs is null or finite/nonnegative. Omitted items are missing, not implicit abstentions. `actualModel` records a provider-resolved identifier when returned; API aliases may drift. For global cohorts, pin a resolved model/version or establish a trusted backend configuration epoch and invalidate comparability on drift. Lab does not independently certify a provider's model identity.

Responses are insert-only. Same session/connection/condition resumes an existing run and requests only missing items. Cancellation preserves completed items. Local revocation is rechecked before inference and in the transaction that saves a returned answer. A 401, 403 or 429 is recorded as a terminal failure for that item and stops that batch; later resume does not erase the failure.

## Scores

| Field | Definition |
|---|---|
| eligible | Total instrument questions |
| completed | Number of terminal response records, including failures/abstentions |
| answered | Valid `Answered` results |
| matches | Answered results equal to the sealed human option |
| matchPercent | 100 × matches / eligible; primary score |
| coveragePercent | 100 × answered / eligible |
| answeredMatchPercent | 100 × matches / answered; null if no answered items; secondary descriptive measure |
| missing | eligible − completed |

Use full precision internally and format one decimal in the UI. Do not round each participant before aggregation. Primary score includes failed, timed-out, invalid, abstained and missing items as nonmatches. A partial run remains visibly incomplete and cannot enter a completed-run cohort. Model-pair agreement uses only jointly answered items on the same sealed session; display that count. Neither metric is a percentage of a person's whole personality captured.

Policy version: `exact-all-eligible-owner-macro-v1`. Validate observations and recompute scores rather than trust serialized score claims.

## Manual personal-agent exchange

Export envelope: `{ "packetHash": "...", "packet": { ... } }`. Packet fields are `schema="hekasomi.agent-task/1"`, `runId`, `testHash`, `contextHash`, `instructions`, `profileContext` (a JSON string), and `items`. It contains no human answer key or provider secret. The supplied packetHash is the hash of Lab's exact serialized packet; **the external agent copies it verbatim and must not reserialize/recompute it**. This hash binds a response to the task; it is not a signature or proof of agent identity.

Answer file:

```json
{
  "schema": "hekasomi.agent-answers/1",
  "runId": "copy-from-packet",
  "packetHash": "copy-from-envelope",
  "answers": [
    {"itemId": "q1", "choice": "copy-one-exact-option"},
    {"itemId": "q2", "choice": null}
  ]
}
```

All items must appear exactly once, and any null is an abstention. The entire import is transactional; a duplicate or invalid answer rejects the entire import. A second import cannot overwrite results.

## Platform report endpoint

`GET /api/lab/v1/report` with `Authorization: Bearer <read-only-Lab-token>` returns:

```ts
type PlatformReport = {
  schema: "hekasomi.platform-report/1";
  id: string; exportedAt: string;
  evaluations: EvaluationObservation[];
  events: GameEvent[];
};
type GameEvent = {
  id: string; sessionId: string;
  kind: "joined" | "left" | "actionAccepted" | "actionRejected" |
        "turnTimedOut" | "stopped" | "testOffered" | "testCompleted";
  actorKind: "human" | "agent"; occurredAt: string;
  durationMs: number | null; provider: string | null;
};
```

This is a **complete current authorized snapshot**, at most 500 evaluations, 10,000 events and 8 MiB of JSON. Limit the report's declared window on the server; do not silently truncate a “complete” history. If the selected bounded window cannot fit, return an explicit error and require a smaller server-side export scope. A later contract can add paging and tombstones, but Lab v1 does not process pages. Each changed snapshot has a new report ID and a monotonically nondecreasing exportedAt; byte-identical/repeated IDs are idempotent, while changed contents under the same ID conflict. Run and event IDs are unique within a report.

Lab recomputes scores and replaces its prior `platform-https` snapshot transactionally. This reflects server-side deletions. Files import as `file-unverified`, are retained separately, and never establish a global mean. Duplicate imported run IDs are displayed once per origin. Game events contain no room transcript, source interview, email or token. The desktop renders these as descriptive events, not audited proof of game performance.

Require HTTPS and a read-only token scoped to the viewer's authorized export and aggregate access. The server must enforce owner isolation and explicit research/export roles. Avoid returning another owner's identifiable answers to ordinary users. Apply consent and deletion filters before export. No token in URL, log or report. Lab loads its token from `HEKASOMI_LAB_TOKEN`; there is no account login/refresh flow in desktop v0.3.

## Global cohort endpoint

`GET /api/lab/v1/cohort` with the same auth accepts URL-encoded filters `testHash`, `provider`, `model`, `connectionIdentity`, `condition`, `compilerVersion`, `language`, `evidenceKind`, `policyVersion`.

```ts
type GlobalCohort = {
  schema: "hekasomi.global-cohort/1";
  testHash: string; provider: string; model: string; connectionIdentity: string;
  condition: PersonaCondition; compilerVersion: string; language: string;
  evidenceKind: string; policyVersion: "exact-all-eligible-owner-macro-v1";
  unit: "owner"; distinctOwners: number; minimumOwners: number;
  meanMatchPercent: number | null; meanCoveragePercent: number | null;
  suppressed: boolean; asOf: string;
};
```

Lab rejects mismatched filters, invalid percentages, future timestamps beyond 5 minutes, or published aggregates below `minimumOwners` (which must be at least 10). If suppressed, both means must be null. If published, means are finite values from 0–100 and meanMatchPercent cannot exceed meanCoveragePercent.

The server derives contributions from trusted authorized records, with analytics consent and no revoked/deleted subjects. Group by the exact cohort. Among completed attempts, select the run with the earliest `createdAt` for each owner, breaking ties by ordinal run ID. Average those owners' unrounded percentages equally. `ParticipationCohorts.Build` provides this calculation for a .NET backend or a porting reference. The web backend must additionally enforce authentic owners, instrument eligibility, timing, memory condition, provider/version provenance, and anti-abuse controls; this pure function is not an authorization layer.

Compute cohort means over the eligible platform population, not the current viewer's limited report. Return no individual contributor identifiers. Label the population and its opt-in selection limits. Rate-limit/filter aggregate queries and review differencing risks before public release. The desktop displays “Platform-reported global average,” owner count, coverage and as-of time; it does not cache or fabricate a default mean.

## Required cross-platform checks

- Identical instrument/config hashes in C#, web and Android, including Unicode and option order.
- Matching score for missing, failed, abstained, invalid and all-correct runs; no division by zero.
- Duplicate/stale/unauthorized submissions rejected; no answer-key read from agent/generation scopes.
- First completed attempt per owner; no weighting by number of models/runs; revoked/deleted/opted-out exclusion; suppression below minimum.
- Online snapshot deletion and duplicate-report semantics, bounded JSON, cancellation and no secret-bearing redirects.
- Clearly synthetic fixture remains 50% match/100% coverage and is never included in a public aggregate.
