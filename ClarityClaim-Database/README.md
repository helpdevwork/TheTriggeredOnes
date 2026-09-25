# ClarityClaim-Database

SQL Server scripts for ClarityClaim's member/visit data store. Written to run
top-to-bottom in **SSMS** against a local SQL Server instance (Windows
Authentication — matches the `LAPTOP-M53Q5L8P` server from your screenshot).

## Execution order

Run these in SSMS, in this exact order, against your SQL Server instance:

| # | File | What it does |
|---|------|---|
| 1 | `01_CreateDatabase.sql` | Creates the `ClarityClaimDb` database (skips if it already exists) |
| 2 | `02_Schema.sql` | Creates all tables, keys, and indexes (drops + recreates — safe to re-run) |
| 3 | `03_Views.sql` | Creates `vw_PatientDashboard`, the single-read view behind the landing-page grid |
| 4 | `04_StoredProcedures.sql` | Creates every stored procedure the API calls — **all data access goes through these**, no ad-hoc SQL from the app |
| 5 | `05_SeedData.sql` | Seeds 5 demo patients (demographics + insurance/eligibility), each with **zero visits** — a doctor creates the first visit from the dashboard |
| 6 | `06_ReviewTeam.sql` | Adds the review-team workflow: `ReviewTeamMembers` table, reviewer assignment on `Visits`, and 4 seeded reviewers |

Each script is idempotent (safe to re-run) except `02_Schema.sql`, which
intentionally drops and recreates tables — re-running it wipes any data you've
since added through the app. Re-run `05_SeedData.sql` afterward to restore the
5 demo patients (`06_ReviewTeam.sql` re-adds its own seed data safely either way).

## Schema overview

```
Doctors                    -- captured via the UI's one-time intake form
Patients                   -- demographics (Mrn is the natural key, e.g. 'demo-001')
  PatientConditions
  PatientMedications
  PatientAllergies
  PatientInsurance         -- payor/plan, network, deductible/OOP, eligibility window
Visits                     -- one row per encounter; a patient has many
  Transcriptions           -- one row per transcript saved for a visit
  SoapNotes                -- Subjective / Objective / Assessment / Plan
    SoapNoteDiagnosisCodes   -- ICD-10, one row per code
    SoapNoteProcedureCodes   -- CPT, one row per code
  ValidationResults         -- approval probability + fallback flag
    ValidationGaps
    ValidationFixes
    ValidationPolicyReferences
    ValidationPassingCriteria
    ValidationHumanReviewFlags
AuditLog                   -- one row per pipeline step (Transcription / SoapGeneration /
                               Validation / PatientSummary), keyed by VisitId
```

`vw_PatientDashboard` joins `Patients` + primary `PatientInsurance` + the most
recent `Visits` row into the single result set the landing-page grid reads.

## Stored procedures (called by the API — no raw SQL from application code)

- `usp_Doctor_Upsert`, `usp_Doctor_GetById`
- `usp_Patient_List`, `usp_Patient_Search`, `usp_Patient_GetDetail`
- `usp_Visit_Create`, `usp_Visit_Complete`, `usp_Visit_GetHistory`
- `usp_Transcription_Insert`
- `usp_SoapNote_Insert` (also writes the ICD-10/CPT child rows, via an `OPENJSON` array parameter)
- `usp_ValidationResult_Insert` (also writes gaps/fixes/references/criteria/flags, same `OPENJSON` pattern)
- `usp_AuditLog_Insert`, `usp_AuditLog_ListByVisit`

Child collections (codes, gaps, fixes, etc.) are passed as a single JSON array
parameter and expanded server-side with `OPENJSON`, so persisting one SOAP
note or one validation result — with all its child rows — is a single round
trip from the API.

## Connecting the API

The API reads the connection string from `ConnectionStrings:ClarityClaimDb` in
`ClarityClaim.API/appsettings.json`. The checked-in default targets local SQL
Server with Windows Authentication:

```json
"ConnectionStrings": {
  "ClarityClaimDb": "Server=localhost;Database=ClarityClaimDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True"
}
```

If your instance name differs (per your screenshot, it's `LAPTOP-M53Q5L8P`),
override it locally in `ClarityClaim.API/appsettings.Development.json`
(git-ignored — never commit machine-specific or credentialed connection
strings) or via the `ConnectionStrings__ClarityClaimDb` environment variable.
Windows Authentication means there's no password to leak in the string itself,
but keep the server/instance name out of source control anyway since it's
identifying infrastructure.

No PHI is ever logged: the API logs SQL exceptions at the message/type level
only, never parameter values, and all access is via parameterized stored
procedure calls (no string-built SQL, so no injection surface).
