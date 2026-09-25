# ClarityClaim — Installation Guide (New Machine Setup)

For a teammate setting up ClarityClaim on a new machine (e.g. an office
laptop) from scratch. Follow the sections in order — each one depends on the
previous. Versions listed are what this project was actually built and
tested against on the original development machine.

---

## 1. Prerequisites Checklist

| # | Software | Version tested | Required for |
|---|----------|----|---|
| 1 | [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0.110 | Building/running the API |
| 2 | [Node.js](https://nodejs.org/) (LTS) | v22.x LTS or later (tested on v26.5.0) | Building/running the React UI |
| 3 | [Git](https://git-scm.com/downloads) | 2.55.x or later | Cloning/managing the repo |
| 4 | [SQL Server](https://www.microsoft.com/sql-server/sql-server-downloads) — Developer Edition | 2019 RTM or later | Hosting `ClarityClaimDb` |
| 5 | [SQL Server Management Studio (SSMS)](https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms) | 18.x or later (20.x recommended) | Running the database scripts |
| 6 | [Visual Studio Code](https://code.visualstudio.com/) | Latest stable | Editing the code (Visual Studio 2022 also works) |

Also needed, but these are **data files**, not installable software — see
§5:
- Whisper speech-to-text model (`ggml-medium.bin`, ~1.5GB)
- Tesseract OCR language data (`eng.traineddata`, ~4MB)

---

## 2. Install the software

### 2.1 .NET 10 SDK
1. Download the **SDK** (not just the runtime) installer for Windows x64 from
   https://dotnet.microsoft.com/download/dotnet/10.0
2. Run the installer, accept defaults.
3. Verify in a new terminal:
   ```
   dotnet --version
   ```
   Should print `10.0.x`.

### 2.2 Node.js
1. Download the **LTS** installer from https://nodejs.org/
2. Run the installer, accept defaults (this also installs `npm`).
3. Verify:
   ```
   node --version
   npm --version
   ```

### 2.3 Git
1. Download from https://git-scm.com/downloads and run the installer.
2. Defaults are fine; if asked, choose "Git from the command line and also
   from 3rd-party software."
3. Verify:
   ```
   git --version
   ```

### 2.4 SQL Server (Developer Edition) + SSMS
1. Download **SQL Server Developer Edition** (free for dev/test, not
   production) from the link in §1. Choose the **Basic** installation type
   for a local default instance.
2. During setup, leave **Windows Authentication** as the auth mode — the
   app's default connection string relies on it (no SQL login/password to
   manage). Note the instance name if you don't use the default one.
3. Download and install **SSMS** separately (it's not bundled with the SQL
   Server installer anymore).
4. Verify: open SSMS, connect to `localhost` (or `.\<InstanceName>` if you
   named it) with Windows Authentication. If it connects, you're set.

### 2.5 IDE
Visual Studio Code is enough for this project (it's a plain solution + Vite
app, no IDE-specific project files required). Install the **C# Dev Kit**
extension for a better .NET editing experience. Visual Studio 2022
(Community or higher) also works if that's already your team's standard.

---

## 3. Get the code

Clone the repository (or copy the project folder if it isn't in a git
remote yet) to your machine, e.g.:
```
git clone <repo-url> "ClarityClaim"
cd ClarityClaim
```
The folder layout you should see:
```
ClarityClaim.sln              (or ClarityClaim.slnx)
ClarityClaim.Domain/
ClarityClaim.Infrastructure/
ClarityClaim.Services/
ClarityClaim.API/
ClarityClaim.Tests/
clarity-claim-ui/
ClarityClaim-Database/
Documentation/
Installations Guide Folder/   (this file)
README.md
```

---

## 4. Set up the database

Run these scripts **in this exact order**, in SSMS, connected to your local
SQL Server instance:

```
ClarityClaim-Database\01_CreateDatabase.sql
ClarityClaim-Database\02_Schema.sql
ClarityClaim-Database\03_Views.sql
ClarityClaim-Database\04_StoredProcedures.sql
ClarityClaim-Database\05_SeedData.sql
ClarityClaim-Database\06_ReviewTeam.sql
```

Open each file in SSMS (`File > Open > File...`), make sure you're
connected to the right server, and click **Execute** (or press F5). Full
details, schema overview, and what each script does are in
`ClarityClaim-Database\README.md`.

When done, running `05_SeedData.sql` a second time (or the verification
query at the bottom of it) should show 5 seeded patients with 0 visits each.

---

## 5. Download the required data files

These are large binary files intentionally **not** checked into git (see
`.gitignore`). Download them once per machine.

### 5.1 Whisper speech-to-text model (required for live voice transcription)
```powershell
Invoke-WebRequest -Uri "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin" -OutFile "ClarityClaim.API\Data\Models\ggml-medium.bin"
```
~1.5GB — takes a few minutes depending on connection speed. Without this
file, the app still runs fine; recording just falls back to a "type the
transcript manually" message instead of live transcription.

### 5.2 Tesseract OCR language data (fallback only — usually not needed)
```powershell
New-Item -ItemType Directory -Force -Path "ClarityClaim.API\Data\Models\tessdata" | Out-Null
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata" -OutFile "ClarityClaim.API\Data\Models\tessdata\eng.traineddata"
```
~4MB. This is only used if the CMS policy-text live fetch (see §6) fails at
first run — otherwise it's never invoked. Safe to skip and add later if
needed.

### 5.3 CMS policy PDFs
Already checked into the repo at `Documentation\CMS NCD and LCD PDFs\`.
Copy all 6 PDFs into `ClarityClaim.API\Data\Policies\` (same filenames):
```powershell
Copy-Item "Documentation\CMS NCD and LCD PDFs\*.pdf" "ClarityClaim.API\Data\Policies\"
```
On first run, the app resolves each policy's real text automatically (cache
→ live CMS fetch → OCR fallback → raw PDF as last resort — see the main
`README.md` for details) and caches the result as `.txt` files alongside the
PDFs. If those `.txt` files are already present in the repo, this step is
effectively instant.

---

## 6. Network / connectivity requirements

- **Local SQL Server**: no external network needed — it's on `localhost`.
- **Internal Ollama server** (LLM inference — SOAP generation, validation,
  patient summaries): the app calls `http://172.50.50.83:11434`. This is a
  **private, internal network address** — confirm your machine is on the
  same office network/VPN as that server before expecting live AI results.
  Check with:
  ```
  curl http://172.50.50.83:11434/v1/models
  ```
  If that times out, live AI calls will fail and the app automatically
  serves pre-generated fallback data instead (the UI shows an amber
  "pre-generated result" badge when it does) — the app still works
  end-to-end either way.
- **cms.gov** (one-time, only if the `.txt` policy cache files aren't
  already present): needed to fetch real policy text. If unreachable, the
  app falls back to OCR, then to raw (near-empty) PDF text.
- **huggingface.co / github.com**: only needed once, to download the data
  files in §5.
- **registry.npmjs.org / api.nuget.org**: needed the first time you run
  `npm install` and `dotnet build` — these fetch every project dependency
  (React, ASP.NET Core, Dapper, PdfPig, etc.) automatically. None of those
  libraries are separately installed software; see
  `SoftwareInstallationRequest.md` for why they aren't on the IT request
  list. If the office network proxies/blocks these registries, package
  restore will fail even with the SDKs installed — check with IT.

---

## 7. Build and run

From the repository root:

**Backend:**
```
dotnet build
dotnet run --project ClarityClaim.API
```
Serves `http://localhost:8000`.

**Frontend** (separate terminal):
```
cd clarity-claim-ui
npm install
npm run dev
```
Serves `http://localhost:5173`. Open that URL in a browser.

**Tests** (optional sanity check):
```
dotnet test
```
Should report all tests passing.

---

## 8. Verify the install

1. Open `http://localhost:5173`. You should see a "tell us who's using
   ClarityClaim today" intake form.
2. Fill it in and continue — you should land on a **Patient Dashboard**
   showing 5 seeded patients.
3. Expand a patient row, click **Start Encounter**, type a short transcript,
   and click **Generate Clinical Note** — you should get a SOAP note back
   (either live or a fallback, both are "working" outcomes).
4. Click **Send for Review Team**, pick a reviewer — a new tab should open
   showing that reviewer's view with a **Run Validation** button.

If all four steps work, the install is complete.

---

## 9. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| API won't start / SQL errors on startup | SQL Server not running, or scripts not run yet | Check the `MSSQLSERVER` service is running (`services.msc`); re-run §4 |
| "Pre-generated result" badge always shows | Can't reach the Ollama server | Check VPN/network per §6; app still works, just not with live AI |
| "Record Encounter" always fails | Whisper model not downloaded | Run the command in §5.1; typed transcripts work regardless |
| `dotnet build` can't find packages | No internet on first build (NuGet restore) | Connect to internet once; packages are cached locally after |
| Port 8000 or 5173 already in use | A previous run is still active | Find and stop the existing `ClarityClaim.API.exe` / `node` process, then retry |
| CORS error in browser console | Frontend not running on `localhost:5173` | The API only allows that exact origin (`Program.cs`) — don't change the frontend's port |
