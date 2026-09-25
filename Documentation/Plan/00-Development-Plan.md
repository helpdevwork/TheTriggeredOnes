# ClarityClaim — Development Plan v1.0

**Scope:** PS 37 (AI Clinical Documentation) + PS 39 (NCD/LCD Medical Necessity Validation), with PS 31 (FHIR) as the data backbone and PS 35 (Regulatory Intelligence) as a background/internal capability.

**Source of truth order:** `ClarityClaim_LLD_v1.html` (authoritative —.NET 10 stack, layer contracts, model IDs) > actual files in `Documentation/CMS NCD and LCD PDFs/` (authoritative for policy data) > `ClarityClaim_HLD_v1.html` (architecture intent, superseded on stack/tech details) > `hackathon_strategy_v2.html` (narrative/strategy only).

---

## 1. Corrections Applied to the HLD

The HLD (`ClarityClaim_HLD_v1.html`) describes a **Python/FastAPI/ChromaDB/openai-whisper** stack. The Install Guide and LLD both confirm the real build target is **.NET 10 / ASP.NET Core Web API**, with **Whisper.net (CUDA)**, **Microsoft.Extensions.VectorData + Semantic Kernel InMemory connector** instead of ChromaDB, and **React + Vite + Tailwind** unchanged. This plan follows the LLD stack exclusively. The HLD's *architecture principles* (P1–P5: local-first, one data object, right-model-right-task, fail gracefully, build-for-demo) still apply conceptually and are carried forward.

### 1.1 Policy file / LCD ID corrections

The HLD and hackathon strategy doc both reference **LCD L35062** (lumbar MRI), **LCD L34067** (cognitive tests), and **LCD L35101** (sleep studies). None of these IDs match what was actually downloaded. The real files in `Documentation/CMS NCD and LCD PDFs/` are:

| # | Filename | Actual LCD/Article ID | Actual Title | Replaces (HLD) |
|---|----------|----|----|----|
| 1 | `L34220_lumbar_mri_lcd.pdf` | **L34220** | Lumbar MRI | L35062 |
| 2 | `L39266_cognitive_assessment_lcd.pdf` | **L39266** | Cognitive Assessment and Care Plan Service | L34067 |
| 3 | `L36839_sleep_studies_lcd.pdf` | **L36839** (nominal) | *see 1.2 below — content mismatch* | L35101 |
| 4 | `A57206_lumbar_mri_codes.pdf` | Article A57206 | Billing & Coding: Lumbar MRI | not in HLD |
| 5 | `A56903_sleep_studies_codes.pdf` | Article A56903 | Billing & Coding: Sleep Studies | not in HLD |
| 6 | `A59036_cognitive_assessment_codes.pdf` | Article A59036 | Billing & Coding: Cognitive Assessment | not in HLD |

**Action:** every prompt, demo script line, `LCD_MAP` dictionary, and UI copy that references `L35062`, `L34067`, or `L35101` must be updated to `L34220`, `L39266`, `L36839` respectively. The primary demo scenario (low back pain → lumbar MRI) keeps its narrative but cites **L34220**, not L35062. Coverage rule confirmed from the actual L34220 text: **4 weeks** of conservative management (not "6 weeks" as the HLD/strategy doc's demo script claims) — the demo script and the `ValidationGap.Requirement` fallback text must say 4 weeks.

### 1.2 `L36839_sleep_studies_lcd.pdf` — content confirmed correct (previous note retracted)

An earlier draft of this plan flagged `L36839_sleep_studies_lcd.pdf` as mislabeled, believing it contained Cognitive Assessment (L39266) content instead of sleep-study content. **This has been corrected: the file's content is relevant to polysomnography/sleep studies as its filename and LCD ID indicate.** No file replacement or exclusion is needed. `L36839` is indexed normally alongside the other 5 policy files, and the `PdfIndexingService.LCD_MAP` entry `"LCD L36839 — Polysomnography & Sleep Studies"` is accurate as originally specified in the LLD.

### 1.3 Everything else in the HLD that still holds

- 3-stage pipeline shape (Documentation → Validation → Patient Summary), Architecture Principles P1–P5, the `ClinicalRecord` single-object flow, the 3 UI screens, the 6 API routes, the Qwen(0.6)/Gemma(0.4) score merge formula, and the demo runbook structure are all unchanged and adopted as-is from the LLD, which already encodes them in .NET terms.

---

## 2. Target Repository Layout

Everything stays under `D:\HA_POC\The Triggered Ones\`. UI, API, and services are separate projects/folders (segregated), following the LLD's 5-layer dependency direction: `API → Services → Infrastructure → Domain`, and `UI → API` over HTTP only.

```
D:\HA_POC\The Triggered Ones\
├── ClarityClaim.sln
├── ClarityClaim.Domain\            # Layer 1 — models, DTOs, interfaces, enums. Zero NuGet deps.
├── ClarityClaim.Infrastructure\    # Layer 2 — LmStudioClient, Whisper, FHIR, VectorStore, PdfIndexing
├── ClarityClaim.Services\          # Layer 3 — SoapGenerationService, ValidationService, PatientSummaryService
├── ClarityClaim.API\               # Layer 4 — Controllers, Program.cs, appsettings.json
│   └── Data\
│       ├── Policies\               # the 6 PDFs (copied/linked from Documentation\CMS NCD and LCD PDFs\)
│       ├── FHIR\                   # cached patient/condition/medication/allergy JSON
│       ├── Models\                 # ggml-medium.bin (Whisper weights)
│       └── Fallbacks\              # pre-generated SOAP/validation/summary JSON for demo safety
├── ClarityClaim.Tests\             # Layer 3.5 — xUnit + Moq + FluentAssertions, mirrors src layers
├── clarity-claim-ui\               # Layer 5 — React + Vite + Tailwind, calls API via HTTP only
└── Documentation\
    ├── ClarityClaim_HLD_v1.html
    ├── ClarityClaim_LLD_v1.html
    ├── ClarityClaim_InstallGuide.html
    ├── hackathon_strategy_v2.html
    ├── CMS NCD and LCD PDFs\
    └── Plan\
        ├── 00-Development-Plan.md          (this file)
        └── 01-Validation-Test-Cases.md
```

No project reverses the dependency arrow. `ClarityClaim.Domain` has no NuGet packages at all — this is enforced by code review, not tooling, so call it out explicitly in PR checks.

---

## 3. Build Phases

Phased for correctness and demo-readiness, not clock-boxed to 48h (the original hackathon framing) — but the phase *order* from the LLD/HLD build timelines is preserved since it minimizes rework (data layer → infra → services → API → UI → integration → polish).

### Phase 0 — Environment & Data Prep (blocking, do first)
- Confirm `.NET 10 SDK`, Node 22 LTS, Git, IDE per Install Guide (already mostly done — only NuGet packages remain, which you're installing yourself).
- Copy/verify the 6 policy PDFs into `ClarityClaim.API/Data/Policies/` (all 6, including `L36839`, index normally per §1.2).
- Cache HAPI FHIR responses for a confirmed working patient ID → `ClarityClaim.API/Data/FHIR/*.json`.
- Download Whisper `ggml-medium.bin` → `ClarityClaim.API/Data/Models/`.
- Source/record the 45s demo encounter audio clip (low back pain + radiculopathy, consistent with L34220).
- Confirm LM Studio serves all 5 required model IDs at `localhost:1234/v1/models`: `qwen/qwen3.6-35b-a3b`, `qwen/qwen3.6-35b-a3b:2`, `google/gemma-4-26b-a4b-qat`, `llama-3.2-1b-instruct`, `text-embedding-nomic-embed-text-v1.5`. Confirm `phi-2` is **not** wired into any code path.

### Phase 1 — Domain Layer
- Implement all records/interfaces/enums exactly as specified in LLD §Domain (`ClinicalRecord`, `SoapNote`, `ValidationResult`, `ValidationGap`, `ValidationFix`, `FhirPatientContext`, `PolicyChunk`, request/response DTOs, `ILanguageModelClient`, `ITranscriptionService`, `IFhirService`, `IVectorStoreService`, `IPdfIndexingService`, enums).
- No logic, no external packages. This is the contract every other layer codes against.

### Phase 2 — Infrastructure Layer
- `LmStudioClient` (chat completion + embeddings, model ID constants including the corrected LCD context but *not* affecting model IDs — those are already correct in LLD).
- `WhisperTranscriptionService` (Whisper.net + CUDA runtime, medium model, warm-up on startup).
- `FhirService` (cache-first, live-fallback, bundle parsing).
- `PolicyVectorStoreService` (`Microsoft.Extensions.VectorData` + Semantic Kernel InMemory, 1400-token chunks / 150-token overlap, 768-dim Nomic vectors).
- `PdfIndexingService` — **update `LCD_MAP` to the corrected IDs from §1.1** before wiring it into startup indexing. All 6 files, including `L36839`, index normally (§1.2).
- Each infra class implements its Domain interface only; nothing here is called directly by Controllers.

### Phase 3 — Services Layer
- `SoapGenerationService` (Qwen Instance 1 / 120K) — system prompt enforces ICD-10 specificity (M51.16 over M54.5), fallback JSON on failure.
- `ValidationService` (RAG retrieve → Qwen Instance 2 / 240K + Gemma4 in parallel via `Task.WhenAll` → 0.6/0.4 merge → disagreement flag at >20-point delta) — **update the gap requirement text/prompts to cite L34220 and the correct 4-week rule**, not 6 weeks.
- `PatientSummaryService` (Llama 1B, temp 0.3, plain-language, multilingual).
- All async, all typed-JSON-in/typed-JSON-out per BP-01/BP-02.

### Phase 4 — API Layer
- `ClinicalController` with the 6 routes: `GET /api/patient/{id}`, `POST /api/transcribe`, `POST /api/generate-soap`, `POST /api/validate`, `POST /api/patient-summary`, `GET /api/regulatory`.
- `Program.cs`: named `HttpClient`s (`lmstudio`, `fhir`) via `IHttpClientFactory`, singleton infra, scoped services, CORS locked to `http://localhost:5173`, PDF indexing run once at startup, listens on `http://localhost:8000`.
- `GET /api/regulatory` mock payload updated to reference **L39266** and **L36839** with correct titles.

### Phase 5 — UI Layer
- Scaffold `clarity-claim-ui` (Vite + React + Tailwind + Axios) per Install Guide.
- Build `ApprovalGauge` first (highest demo priority per LLD/HLD), then Screen 1 → Screen 2 → Screen 3, `useClinicalRecord` shared state, `services/api.js` matching the 6 routes exactly.
- Wrap each screen in a React Error Boundary with static fallback screenshots in `/public/fallbacks/`.

### Phase 6 — Integration, Fallbacks, Polish
- Pre-generate and save fallback JSON (SOAP note, validation @ pre-fix score, validation @ post-fix score, patient summary) to `ClarityClaim.API/Data/Fallbacks/` using the **corrected L34220 / 4-week** scenario.
- Wire the `_useFallback` toggle and >45s / exception → fallback path end-to-end.
- Run the complete Screen 1 → 2 → 3 flow repeatedly; fix every failure point.
- Verify offline operation (disconnect network) — FHIR cache, PDF index, Whisper, and LM Studio (local) must all still work.

---

## 4. Open Items Requiring a Decision Before Build Starts

1. **Demo scenario coverage rule** — confirm all prompts/UI copy use **4 weeks** (per actual L34220 text), replacing every "6 weeks" reference inherited from the HLD/strategy doc.
2. **HAPI FHIR patient ID** — must be re-verified as live/valid before caching (per LLD's own caveat); pick and lock one ID for the whole build.
3. **Fallback score values** — decide the exact pre-fix/post-fix approval percentages to hardcode into fallbacks once real Qwen/Gemma outputs are observed during Phase 6 (HLD used 74%→93% illustratively; keep or replace with real observed numbers).
