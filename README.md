# ClarityClaim

AI clinical documentation + NCD/LCD medical-necessity validation. Built per
`Documentation/ClarityClaim_LLD_v1.html` and `Documentation/Plan/00-Development-Plan.md`,
with a SQL Server data layer (`ClarityClaim-Database/`) and a healthcare-styled UI added on top.

## Solution layout

```
ClarityClaim.sln
ClarityClaim.Domain/            Layer 1 -- models, DTOs, interfaces, enums (zero deps)
ClarityClaim.Infrastructure/    Layer 2 -- Ollama client, Whisper, FHIR, lexical policy search, PDF indexing, SQL Server repos
ClarityClaim.Services/          Layer 3 -- SOAP generation, validation, patient summary, regulatory
ClarityClaim.API/               Layer 4 -- ASP.NET Core Web API (controllers, Program.cs)
  Data/Policies/                 6 CMS NCD/LCD policy PDFs (indexed into RAG at startup)
  Data/FHIR/                     Cached patient JSON (offline-first, legacy demo lookup)
  Data/Models/                   Whisper ggml-medium.bin goes here (not checked in)
  Data/Fallbacks/                Pre-generated demo-safe JSON (SOAP note, validation pre/post-fix)
ClarityClaim.Tests/              xUnit + Moq + FluentAssertions
clarity-claim-ui/                Layer 5 -- React + Vite + Tailwind v4, calls the API over HTTP
ClarityClaim-Database/           SQL Server scripts -- schema, views, stored procedures, seed data
```

Dependency direction: `API -> Services -> Infrastructure -> Domain`, `UI -> API` (HTTP only).

## Prerequisites already confirmed on this machine

- .NET 10 SDK (10.0.110)
- Node.js 22+ / npm
- SQL Server (`MSSQLSERVER` service, local default instance) -- already running
- An Ollama server reachable at `http://172.50.50.83:11434` serving
  `gpt-oss:20b`, `granite3.3:latest`, `llama3:latest`, `mistral:7b-instruct`
  (confirmed via its `/v1/models` endpoint) -- **this machine could not reach
  that address when last tested** (connection timed out); confirm VPN/network
  access to it before expecting live AI results. Without it, every AI call
  gracefully serves pre-generated fallback data.

## One-time setup

### 1. Database (required)

Run the 5 scripts in `ClarityClaim-Database/` in order, in SSMS, against your
local SQL Server instance (Windows Authentication):

```
01_CreateDatabase.sql -> 02_Schema.sql -> 03_Views.sql -> 04_StoredProcedures.sql -> 05_SeedData.sql
```

Full details in `ClarityClaim-Database/README.md`. This has already been run
once on this machine -- `ClarityClaimDb` exists with 5 seeded demo patients
(zero visits each). Re-run if you want a clean slate.

### 2. Whisper model weights (optional -- only for live voice transcription)

Typed/pasted transcripts work without this.

```
Invoke-WebRequest -Uri "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin" -OutFile "ClarityClaim.API\Data\Models\ggml-medium.bin"
```

### 3. Ollama server (optional -- only for live AI instead of fallback data)

Without this reachable, every AI call (SOAP generation, validation, patient
summary) gracefully serves pre-generated fallback data -- the app still runs
end-to-end, and the UI shows an amber "pre-generated result" badge when it does.

The API is wired to call `http://172.50.50.83:11434` (an Ollama server,
OpenAI-compatible `/v1/chat/completions` API), confirmed to be serving:

- `gpt-oss:20b` -- SOAP generation (largest available model, most complex single-pass task)
- `granite3.3:latest` + `llama3:latest` -- validation, run in parallel as two
  independent opinions and merged 0.6/0.4 (mirrors the original two-model
  ensemble design intent)
- `mistral:7b-instruct` -- patient summary (lighter task)
- `qwen2.5-coder:7b` is available on that server but unused -- it's coding-specialized

**No embedding model is available** on that server, so policy retrieval
(`ClarityClaim.Infrastructure/Search/PolicyLexicalSearchService.cs`) uses
TF-IDF lexical search over the chunked PDF text instead of vector similarity
-- it needs no model call at all, so it's not affected by Ollama being
unreachable.

1. Confirm reachability: `curl http://172.50.50.83:11434/v1/models` should
   return the model list. **This timed out when tested from the machine this
   was built on** -- check VPN/firewall/network access to that address before
   assuming it'll work; if the address or port differs in your environment,
   override it via `Llm:BaseUrl` in `appsettings.json` or
   `appsettings.Development.json`.
2. No further setup needed beyond reachability -- the API calls it directly,
   no local server to start.

Everything else (NuGet packages, npm packages, the 6 policy PDFs, fallback
JSON) is already in place.

### Resolved: the 6 CMS policy PDFs had no extractable text

Diagnosed directly with PdfPig: all 6 PDFs in `Documentation/CMS NCD and LCD
PDFs/` are scanned/image-based with zero characters of real text per page
(e.g. `L34220_lumbar_mri_lcd.pdf` is 24 pages, every page returned 0 characters).
This was always true but was previously masked because indexing failed at the
embedding-call step (LM Studio unreachable) before the "indexed N chars" log
line was ever reached.

Fixed with a resolution chain in `PolicyTextResolver`, run once per policy at
startup, in order:
1. **Disk cache** -- `Data/Policies/{id}.txt`, written by whichever of the
   next two steps succeeds. Already populated on this machine (14.4K-294K
   real characters per document -- see below).
2. **Live CMS fetch** (`CmsPolicyTextService`) -- these exact LCD/Article IDs
   are published as real HTML on CMS's Medicare Coverage Database
   (`cms.gov/medicare-coverage-database/view/{lcd|article}.aspx?...`), the
   same official source the scanned PDFs were printed from. Confirmed working:
   all 6 fetched successfully (A56903: 6.9K chars, A57206: 294K chars, A59036:
   24.7K chars, L34220: 14.4K chars, L36839: 22K chars, L39266: 9.9K chars).
3. **OCR fallback** (`PolicyPdfOcrService`, Tesseract + PDFtoImage/PDFium) --
   only runs if CMS is unreachable at startup. Requires
   `Data/Models/tessdata/eng.traineddata` (already downloaded on this machine,
   ~4MB); if missing, this step is silently skipped.
4. **Raw PdfPig text** -- last-resort fallback so indexing never fails outright,
   even though it'll be near-empty for these particular scanned PDFs.

Since CMS was reachable when this was set up, all 6 `.txt` cache files are
already populated with real policy text and checked into `Data/Policies/` --
OCR won't actually run unless that cache is deleted and CMS is also
unreachable at the next startup. The CMS fetch is polite about it: one request
per policy per cache lifetime, never on the request path, with a descriptive
User-Agent.

## Running locally

**Backend** (from the repo root):
```
dotnet run --project ClarityClaim.API
```
Serves `http://localhost:8000`. Reads `ConnectionStrings:ClarityClaimDb` from
`appsettings.json` (defaults to `Server=localhost` with Windows Authentication --
override in `appsettings.Development.json`, which is git-ignored, if your
instance name differs).

**Frontend** (in a second terminal):
```
cd clarity-claim-ui
npm run dev
```
Serves `http://localhost:5173`. Open that URL in a browser.

**Tests**:
```
dotnet test
```

## Using the app

1. **First run only -- doctor intake**: enter a name and email (specialty/NPI
   optional). Full name and specialty accept letters and spaces only; email is
   regex-validated; NPI number, if given, must pass the real 10-digit Luhn
   check-digit algorithm CMS uses (validated both client-side for instant
   feedback and server-side in `DoctorUpsertRequest`/`NpiCheckDigitAttribute`).
   This is saved to `dbo.Doctors` and cached in the browser; the profile then
   shows top-right on every screen. Not password auth -- "switch profile"
   clears it and shows the form again.

2. **Landing page -- Patient Dashboard**: a grid of the 5 seeded patients
   (demographics, primary insurance, eligibility window, last visit). Use the
   search bar in the header (matches name, MRN, payor, plan, contact, email).
   Click the arrow on the right of a row to expand full demographics,
   insurance/eligibility, cost-share, and clinical snapshot (conditions,
   medications, allergies) inline, then **Start Encounter** -- this creates a
   new row in `dbo.Visits` and opens the doctor's 2-step wizard for that patient.

3. **Step 1 -- Encounter Capture**: click "Record Encounter" to capture audio
   via your microphone (needs the Whisper model downloaded; works fully
   offline from Ollama since transcription is local). The recording is
   re-encoded to a 16kHz mono WAV in the browser before upload -- Whisper.net
   requires that exact format, and the browser's native recording format
   (webm/Opus) isn't readable by it directly. Or type/paste a transcript, e.g.:
   > "Patient reports 6 weeks of low back pain radiating down the left leg,
   > consistent with radiculopathy. Positive straight leg raise. No conservative
   > treatment tried yet."

   Click **Generate Clinical Note**.

4. **Step 2 -- SOAP Note (doctor's last step)**: review/edit Subjective,
   Objective, Assessment, Plan and the ICD-10/CPT codes, then click **Send for
   Review Team** -- not "Run Validation." Medical-necessity validation is a
   reviewer/coder function in US practice, not something the ordering
   physician runs on their own note, so that step was moved off the doctor's
   screen entirely. Picking a reviewer from the table that opens:
   - persists your final edits (`PUT /api/soap-notes/{id}`),
   - assigns the visit to that reviewer (`Status` becomes `PendingReview`),
   - opens **a new browser tab** for that reviewer, with their name shown
     top-right (their identity comes from being picked, not a separate login).

5. **Reviewer's tab**: shows the exact SOAP note the doctor sent, read-only,
   with a **Run Validation** button in its place. Clicking it runs the same
   validation pipeline and shows the Validation Scorecard: approval-probability
   gauge, a **Documentation Gaps** section that relabels itself **Claim
   Rejection Reasons** when the score is under 60 (high denial risk) and gaps
   exist, passing criteria, an English-only plain-language patient summary
   (button removed -- no language picker needed), the regulatory-updates panel,
   and an **Audit Trail** card showing every pipeline step (Transcription, SOAP
   Generation, Validation) with status and timing, read back from `dbo.AuditLog`.
   Click **Fix This -> Insert into note** to re-run validation with the fix
   applied -- watch the gauge animate up (74% -> 93% in the fallback scenario).

Use the header's sun/moon button to switch between light and dark mode (teal/
green palette, persisted per browser).

## What's stored in ClarityClaimDb, and how

Every pipeline call that includes a `visitId` persists its output and writes
an audit-log row -- transcription text, the full SOAP note plus its ICD-10/CPT
child rows, the validation result plus its gaps/fixes/policy references/
passing criteria/human-review flags, all via the stored procedures in
`ClarityClaim-Database/04_StoredProcedures.sql` (parameterized calls only, no
ad-hoc SQL). Persistence failures are caught and logged as warnings -- they
never break the API response, consistent with the fail-gracefully principle
used everywhere else in this build. Full schema and connection details are in
`ClarityClaim-Database/README.md`.

## Notes on corrections applied vs. the original HLD/strategy docs

Per `Documentation/Plan/00-Development-Plan.md`: the LCD IDs are `L34220`
(lumbar MRI), `L39266` (cognitive assessment), `L36839` (sleep studies) --
not the stale `L35062`/`L34067`/`L35101` values in the original HLD draft --
and the lumbar-MRI conservative-treatment rule is **4 weeks**, not 6. Both
corrections are reflected throughout the code, prompts, and fallback JSON.

## Known simplifications for this build

- The transcript endpoint is a plain `POST` returning the full transcript, not
  a Server-Sent-Events stream -- the LLD's own reference controller code uses
  the same plain-POST shape even though one best-practice note (BP-10) describes
  live SSE streaming; true chunked live captioning was out of scope here.
- Whisper.net runs on CPU (`Whisper.net.Runtime`), not the CUDA runtime, since
  GPU availability wasn't confirmed on this machine -- swap in
  `Whisper.net.Runtime.Cuda.Windows` for GPU acceleration if available.
- Test coverage is a representative subset of `Documentation/Plan/01-Validation-Test-Cases.md`
  (domain contracts, model-ID exclusion, SOAP/validation service behavior,
  fallback correctness, NPI checksum) rather than the full ~90-case matrix. No
  new tests were added for the SQL repositories (they're exercised via live
  curl testing against the real database instead).
- The doctor "login" is a profile form, not authentication -- there's no
  password check, matching "assume a doctor is always logged in."
- Policy retrieval is lexical (TF-IDF), not embedding-based, because no
  embedding model is available on the configured Ollama server -- see
  `ClarityClaim.Infrastructure/Search/PolicyLexicalSearchService.cs`.
- Ollama models' default context window (`num_ctx`, often 2048 tokens unless
  configured otherwise) may be smaller than the validation prompt (retrieved
  policy chunks + SOAP text + instructions). If live validation responses look
  truncated or malformed, increase `num_ctx` for the relevant models on the
  Ollama server (e.g. via a custom Modelfile) -- this wasn't verifiable from
  here since the server was unreachable.
- The reviewer's SOAP note view is read-only by design (a real review/coding
  team wouldn't edit the physician's documentation) -- the doctor is the only
  one who edits, and does so before sending, not after.
- Reviewer "identity" is whoever the doctor picked from the table, not a
  separate login -- there's no reviewer-side auth, matching the doctor's own
  "always logged in" profile-not-password model.

## Fixed: transcription logged multiple times per encounter

The audit trail was showing 2-3 `Transcription` rows per encounter instead of
one. Cause: `EncounterCapture`'s "Generate Clinical Note" handler
unconditionally called `saveManualTranscript`, even when the transcript had
already come from a **recording** (which the backend already persists via
`POST /api/transcribe?visitId=...`) -- so every recorded encounter got a
second, redundant "Manual" row, and clicking the button more than once (e.g.
retrying after an error) added yet another. Fixed by tracking whether the
current transcript text has already been persisted (`transcriptPersisted`
state in `EncounterCapture.jsx`) and only calling `saveManualTranscript` once,
and only for transcripts that were actually typed rather than recorded.
