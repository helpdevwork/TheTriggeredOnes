# ClarityClaim — Validation & Test Cases v1.0

Companion to `00-Development-Plan.md`. Organized by layer (bottom-up, matching build order) plus cross-cutting integration/demo scenarios. Each case lists **Given / When / Then** and the layer it belongs in (`ClarityClaim.Tests` project, mirrored folder structure).

---

## 1. Domain Layer — sanity checks (no behavior, but contract checks)

| ID | Case | Expected |
|----|------|----|
| DOM-01 | `ClinicalRecord` default-constructs with non-null `Patient`, `SoapNote`, `Validation` and empty `Transcript`/`PatientSummary` | No NullReferenceException anywhere downstream that reads these before population |
| DOM-02 | `ValidationSeverity` enum ordering: `Critical=3 > Major=2 > Minor=1` | Sorting gaps by severity descending puts Critical first |
| DOM-03 | `ModelInstance` enum has no member for `phi-2` | Confirms Phi-2 cannot be selected via a valid enum value anywhere in code |

---

## 2. Infrastructure Layer

### 2.1 `LmStudioClient`

| ID | Case | Given | When | Then |
|----|------|-------|------|------|
| INF-01 | Happy-path completion | LM Studio running, valid model ID | `CompleteAsync(QWEN_120K, sys, user)` | Returns non-empty string, no exception |
| INF-02 | Embedding shape | Nomic model loaded | `EmbedAsync("test text")` | Returns `float[768]` |
| INF-03 | Timeout handling | LM Studio unreachable / port closed | `CompleteAsync(...)` | Throws `HttpRequestException`/`TaskCanceledException` within the configured 120s client timeout — caller (Service layer) must catch, not this class |
| INF-04 | No `phi-2` reachable via `Models` constants | Inspect `LmStudioClient.Models` | n/a | No constant maps to a `phi-2` model ID |
| INF-05 | Reused `HttpClient` | Multiple sequential calls | 50 rapid `CompleteAsync` calls | No socket exhaustion (validates BP-09 — named client via `IHttpClientFactory`, not `new HttpClient()`) |

### 2.2 `WhisperTranscriptionService`

| ID | Case | Given | When | Then |
|----|------|-------|------|------|
| INF-06 | Cold start init | Service not yet initialized | `TranscribeAsync(path)` called first | `InitialiseAsync()` runs implicitly, model loads from `ggml-medium.bin`, no crash |
| INF-07 | Clinical term accuracy | 45s demo audio clip with "radiculopathy", "metformin" | `TranscribeAsync` | Transcript text contains these terms spelled correctly (medium model, not tiny/base — validates HLD demo-safety note) |
| INF-08 | Segment timestamps monotonic | Any valid audio file | `TranscribeAsync` | `segments[i].End <= segments[i+1].Start` (or close) for all i, `Start < End` for every segment |
| INF-09 | Missing model file | `ggml-medium.bin` absent at configured path | `InitialiseAsync()` | Throws a clear, loggable exception (not a silent hang) — this is a Phase 0 pre-check gap that should fail loudly |

### 2.3 `FhirService`

| ID | Case | Given | When | Then |
|----|------|-------|------|------|
| INF-10 | Cache hit | `patient_{id}.json` exists in `Data/FHIR/` | `GetPatientContextAsync(id)` | Returns deserialized cached object; **no HTTP call made** (verify via mock `HttpClient` handler asserting zero invocations) |
| INF-11 | Cache miss, live success | No cache file, HAPI reachable, valid ID | `GetPatientContextAsync(id)` | Fetches all 4 resources, parses bundle, writes cache file, returns populated context |
| INF-12 | Cache miss, live failure (offline) | No cache file, network disconnected | `GetPatientContextAsync(id)` | Returns `FhirPatientContext` with only `PatientId` set, no exception propagates, warning logged |
| INF-13 | Malformed FHIR bundle | Condition resource missing `code.text` | `ParseFhirBundle(...)` | Skips/omits that entry rather than throwing; `Conditions` list simply shorter |

### 2.4 `PolicyVectorStoreService` / RAG

| ID | Case | Given | When | Then |
|----|------|-------|------|------|
| INF-14 | Chunking respects token ceiling | Full text of `L34220_lumbar_mri_lcd.pdf` (~24 pages) | `IndexDocumentAsync` chunking step | No chunk exceeds ~1400 tokens (approx via 4 chars/token check); overlap ≈150 tokens between consecutive chunks |
| INF-15 | Retrieval relevance — lumbar MRI | Index built from L34220 + A57206 | `RetrieveAsync("M51.16 lumbar MRI medical necessity conservative treatment", topK=3)` | Top-3 results' `Source`/`LcdId` = L34220 or A57206; retrieved text contains "conservative" and a duration reference |
| INF-16 | Retrieval relevance — cognitive assessment | Index built from L39266 + A59036 | `RetrieveAsync("CPT 99483 cognitive assessment care plan", topK=3)` | Top-3 results reference L39266/A59036; text mentions "independent historian" or "care plan" |
| INF-17 | Retrieval relevance — sleep studies | Index built from L36839 + A56903 | `RetrieveAsync("polysomnography sleep study documentation", topK=3)` | Top-3 results' `Source`/`LcdId` = L36839 or A56903; retrieved text is genuine sleep-study/polysomnography content (confirms §1.2 — file content matches its filename/LCD ID, no mislabeling) |
| INF-18 | 2048-token embedding ceiling not exceeded | Any chunk from step INF-14 | `EmbedAsync(chunk)` | Embedding call succeeds; no silent truncation (cross-check chunk length stays under Nomic's 2048-token hard limit with margin) |

### 2.5 `PdfIndexingService`

| ID | Case | Given | When | Then |
|----|------|-------|------|------|
| INF-19 | All 6 PDFs indexed at startup | `Data/Policies/` populated | `IndexAllPoliciesAsync(path)` | One `IndexDocumentAsync` call per PDF; log line per file with char count > 0 |
| INF-20 | `LCD_MAP` matches corrected IDs | Inspect `PdfIndexingService.LCD_MAP` | n/a | Keys are `L34220`, `L39266`, `L36839`, `A56903`, `A57206`, `A59036` — **not** `L35062`/`L34067`/`L35101` |
| INF-21 | Unknown filename fallback | A 7th PDF dropped in with an unrecognized prefix | `IndexAllPoliciesAsync` | Falls back to using the filename itself as `Source`/`LcdId` rather than throwing |

---

## 3. Services Layer

### 3.1 `SoapGenerationService`

| ID | Case | Given | When | Then |
|----|------|-------|------|------|
| SVC-01 | Happy path | Valid transcript + patient context, LM Studio up | `GenerateAsync(req)` | Returns `SoapResponse` with `FromFallback=false`, all 4 SOAP fields non-empty, ≥1 ICD-10 code |
| SVC-02 | ICD specificity rule | Transcript explicitly mentions "radiculopathy" | `GenerateAsync(req)` | Resulting `icd10_codes` contains `M51.16` and does **not** contain `M54.5` (validates the anti-unspecified-code system prompt rule) |
| SVC-03 | Malformed LLM JSON | Mock `ILanguageModelClient` returns text wrapped in ```` ```json ```` fences | `GenerateAsync(req)` | Fence-stripping succeeds, parses correctly (or: if truly malformed, falls back gracefully — see SVC-04) |
| SVC-04 | LLM throws / invalid JSON | Mock client throws or returns non-JSON garbage | `GenerateAsync(req)` | Returns `SoapResponse` with `FromFallback=true`, loaded from `Data/Fallbacks/`, **no exception escapes the method** |
| SVC-05 | Confidence values in range | Any successful generation | Inspect `icd10_codes[].Confidence`, `cpt_codes[].Confidence` | All values in `[0.0, 1.0]` |

### 3.2 `ValidationService`

| ID | Case | Given | When | Then |
|----|------|-------|------|------|
| SVC-06 | Happy path, parallel execution | Valid SOAP note + ICD codes, both models respond | `ValidateAsync(req)` | Qwen and Gemma calls run concurrently (assert via mock timing/call-order, not sequential); merged score = `round(qwen*0.6 + gemma*0.4)` |
| SVC-07 | Correct policy citation | ICD code `M51.16` submitted | `ValidateAsync(req)` | `PolicyReferences` includes an entry sourced from **L34220** (not L35062) |
| SVC-08 | Requirement text uses correct duration | Gap generated for missing conservative treatment | Inspect `ValidationGap.Requirement` (live or fallback) | Text says **"4 weeks"**, not "6 weeks" |
| SVC-09 | Disagreement flag triggers | Mock Qwen score=90, mock Gemma score=60 (delta=30) | `ValidateAsync(req)` | `HumanReviewFlags` contains the disagreement message |
| SVC-10 | Disagreement flag does not trigger | Mock Qwen score=80, mock Gemma score=70 (delta=10) | `ValidateAsync(req)` | `HumanReviewFlags` is empty |
| SVC-11 | One model fails, other succeeds | Gemma call throws, Qwen succeeds | `ValidateAsync(req)` | Whole call still fails to fallback (per LLD's `try/catch` around the full method) — confirms `Task.WhenAll` failure propagates and fallback is served, not a partial/crashed result |
| SVC-12 | Full failure → fallback | Both LM calls throw | `ValidateAsync(req)` | Returns fallback `ValidationResult`, `FromFallback=true` |
| SVC-13 | The demo "wow" delta | Fallback pre-fix vs post-fix JSON | Compare `Data/Fallbacks/validation-prefix.json` and `validation-postfix.json` | Post-fix `ApprovalProbability` > pre-fix by a visually meaningful margin (≥15 points) and gap count decreases |

### 3.3 `PatientSummaryService`

| ID | Case | Given | When | Then |
|----|------|-------|------|------|
| SVC-14 | Happy path | Valid SOAP note, `language="en"` | `SummariseAsync(req)` | Returns non-empty plain text, no JSON artifacts, no markdown headers |
| SVC-15 | Multilingual pass-through | `language="es"` | `SummariseAsync(req)` | `Language` field on response echoes `"es"`; prompt correctly instructs the model in Spanish |
| SVC-16 | Temperature setting | Inspect the call args passed to `ILanguageModelClient` | `SummariseAsync(req)` | `temperature=0.3f` (not 0.1f — this is the one call intentionally warmer per BP-05) |

---

## 4. API Layer

| ID | Case | Endpoint | Given | When | Then |
|----|------|----|-------|------|------|
| API-01 | Get cached patient | `GET /api/patient/{id}` | Cache file exists | Call with valid `id` | 200 OK, `FhirPatientContext` JSON |
| API-02 | Transcribe audio | `POST /api/transcribe` | Multipart form with `.wav` file | Call | 200 OK, `TranscriptResponse`; temp file cleaned up afterward (assert file no longer exists at temp path) |
| API-03 | Generate SOAP | `POST /api/generate-soap` | Valid `GenerateSoapRequest` body | Call | 200 OK, `SoapResponse` |
| API-04 | Validate | `POST /api/validate` | Valid `ValidateDocumentRequest` body | Call | 200 OK, `ValidationResponse` |
| API-05 | Patient summary | `POST /api/patient-summary` | Valid `PatientSummaryRequest` body | Call | 200 OK, `PatientSummaryResponse` |
| API-06 | Regulatory updates | `GET /api/regulatory` | n/a | Call | 200 OK, array of `RegulatoryUpdate`; entries reference **L39266** and **L36839** with corrected titles, not L34067/L35101 |
| API-07 | CORS enforcement | Request from `http://evil.example.com` origin | Any endpoint, browser-simulated CORS preflight | Blocked — no `Access-Control-Allow-Origin` for that origin (validates BP-08, `AllowAnyOrigin()` must never appear in `Program.cs`) |
| API-08 | CORS allow-list | Request from `http://localhost:5173` | Any endpoint | Allowed — response includes correct CORS headers |
| API-09 | Malformed request body | `POST /api/generate-soap` with invalid JSON | Call | 400 Bad Request (model binding failure), not 500 |
| API-10 | Startup indexing runs once | Application starts | n/a | `IPdfIndexingService.IndexAllPoliciesAsync` called exactly once during `Program.cs` startup, before `app.Run()` |

---

## 5. UI Layer

| ID | Case | Component | Given | When | Then |
|----|------|----|-------|------|------|
| UI-01 | Patient sidebar loads | `PatientSidebar` | App mounts, Screen 1 | On load | Displays name, conditions, medications, allergies from `GET /api/patient/{id}` |
| UI-02 | Generate button disabled state | `EncounterCapture` | `record.transcript === ''` | Render | "Generate Clinical Note" button is `disabled` |
| UI-03 | Generate button enabled after transcript | `EncounterCapture` | Transcript populated via recorder/SSE | Render | Button becomes enabled |
| UI-04 | Screen transition on generate | `EncounterCapture` → `SOAPNoteEditor` | Click "Generate Clinical Note", API resolves | After await | `onNext()` fires, Screen 2 renders with populated SOAP fields |
| UI-05 | SOAP field edits persist | `SOAPNoteEditor` | User edits "Assessment" textarea | Blur / before validate click | `updateRecord({soapNote: note})` called with edited value before `api.validate` fires |
| UI-06 | ICD chips render confidence | `ICD10Chips` | `codes=[{code:"M51.16", confidence:0.94}]` | Render | Chip shows code, description, and a confidence indicator |
| UI-07 | Approval gauge animates | `ApprovalGauge` | `score=93` passed as prop after a prior render at `score=74` | Effect fires | Displayed value animates from 74 toward 93 over ~1200ms (not an instant jump) |
| UI-08 | Gauge color thresholds | `ApprovalGauge` | `score=45` / `score=65` / `score=85` | Render each | Red (<60), amber (60–79), green (≥80) respectively |
| UI-09 | Gap checklist "Fix This" flow | `GapChecklist` | A gap with an associated `ValidationFix` | Click "Fix This" | Suggested language displays inline, no extra API call fired (per LLD note — fix is already in the validate response) |
| UI-10 | Error boundary fallback | Any screen | Component throws (simulated) | Render | Error boundary catches, renders the static fallback image from `/public/fallbacks/`, app does not white-screen |
| UI-11 | Regulatory panel content | `RegulatoryPanel` | `GET /api/regulatory` response | Render | Displays L39266 and L36839 entries with corrected titles |

---

## 6. Cross-Cutting / Integration Scenarios

| ID | Case | Scope | Given | When | Then |
|----|------|----|-------|------|------|
| INT-01 | Full happy-path demo flow | E2E | Backend + frontend running, LM Studio up, network connected | Load patient → record/play audio → transcribe → generate SOAP → validate → fix gap → re-validate → patient summary → regulatory panel | Every step succeeds with live (non-fallback) data end-to-end; final approval score visibly higher than initial |
| INT-02 | **Offline demo flow** ("no cloud" moment) | E2E | Same as INT-01, but network/ethernet disconnected first | Repeat the full flow | Still succeeds — FHIR serves from cache, PDFs already indexed in-memory, Whisper and LM Studio are local-only. **No step depends on internet** |
| INT-03 | Fallback-triggered flow | E2E | LM Studio stopped mid-session (simulate outage) | Attempt generate-soap / validate | Every affected endpoint returns `FromFallback=true` with valid, pre-generated content; UI shows no error state to the user |
| INT-04 | RAG citation consistency | Integration | Full pipeline run for the lumbar MRI scenario | Complete validate call | `PolicyReferences` in the response cite **L34220**, and the gap `Requirement` text says **4 weeks** — both consistent with the actual PDF content, not the HLD's stale "L35062 / 6 weeks" values |
| INT-05 | Model exclusion check | Static/integration | Full DI container built at startup | Resolve all registered services | No registered component references a `phi-2` model ID anywhere in the resolved object graph |
| INT-06 | Score merge arithmetic, real values | Integration | Live Qwen + Gemma calls on the demo scenario | `ValidateAsync` | Manually recompute `round(qwenScore*0.6 + gemmaScore*0.4)` from logged raw scores and confirm it equals `ApprovalProbability` |
| INT-07 | Repeat-run stability | Integration | Same demo audio/transcript run 3 times back-to-back | Full flow x3 | ICD-10 code selection (M51.16 vs M54.5) and approval score band (e.g., 65–85%) are stable across runs — flags prompt non-determinism if temperature=0.1 isn't actually being honored |
| INT-08 | Timing budget | Integration/perf | Full validate call, Qwen+Gemma in parallel | Measure wall-clock time | Total validation time ≈ `max(Qwen time, Gemma time)`, not the sum — confirms `Task.WhenAll` parallelism is real, not accidentally sequential |

---

## 7. Data-Correction Acceptance Criteria (must pass before "done")

These are the concrete, testable outcomes of the corrections in `00-Development-Plan.md` §1:

- [ ] No source file (prompts, fallback JSON, `LCD_MAP`, UI copy, demo script) contains the strings `L35062`, `L34067`, or `L35101`.
- [ ] Every reference to the lumbar-MRI conservative-treatment duration says **4 weeks**, not 6.
- [ ] `L36839_sleep_studies_lcd.pdf` is indexed normally alongside the other 5 policy files (content confirmed correct per §1.2); INF-17 passes with genuine sleep-study citations.
- [ ] `Data/Policies/` contains all files confirmed present on disk: `L34220`, `L36839`, `L39266`, `A56903`, `A57206`, `A59036` (A57206 is already present — LLD's "missing file" note is stale and should not block the build).
