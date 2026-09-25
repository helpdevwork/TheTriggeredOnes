/*
    ClarityClaim-Database — 02_Schema.sql
    All tables, primary/foreign keys, and indexes for ClarityClaimDb.
    Run after 01_CreateDatabase.sql. Safe to re-run (drops and recreates, in FK-safe order).
*/

USE ClarityClaimDb;
GO

-- ── Drop in reverse dependency order (safe re-run) ─────────────────────────
IF OBJECT_ID('dbo.AuditLog', 'U') IS NOT NULL DROP TABLE dbo.AuditLog;
IF OBJECT_ID('dbo.ValidationHumanReviewFlags', 'U') IS NOT NULL DROP TABLE dbo.ValidationHumanReviewFlags;
IF OBJECT_ID('dbo.ValidationPassingCriteria', 'U') IS NOT NULL DROP TABLE dbo.ValidationPassingCriteria;
IF OBJECT_ID('dbo.ValidationPolicyReferences', 'U') IS NOT NULL DROP TABLE dbo.ValidationPolicyReferences;
IF OBJECT_ID('dbo.ValidationFixes', 'U') IS NOT NULL DROP TABLE dbo.ValidationFixes;
IF OBJECT_ID('dbo.ValidationGaps', 'U') IS NOT NULL DROP TABLE dbo.ValidationGaps;
IF OBJECT_ID('dbo.ValidationResults', 'U') IS NOT NULL DROP TABLE dbo.ValidationResults;
IF OBJECT_ID('dbo.SoapNoteProcedureCodes', 'U') IS NOT NULL DROP TABLE dbo.SoapNoteProcedureCodes;
IF OBJECT_ID('dbo.SoapNoteDiagnosisCodes', 'U') IS NOT NULL DROP TABLE dbo.SoapNoteDiagnosisCodes;
IF OBJECT_ID('dbo.SoapNotes', 'U') IS NOT NULL DROP TABLE dbo.SoapNotes;
IF OBJECT_ID('dbo.Transcriptions', 'U') IS NOT NULL DROP TABLE dbo.Transcriptions;
IF OBJECT_ID('dbo.Visits', 'U') IS NOT NULL DROP TABLE dbo.Visits;
IF OBJECT_ID('dbo.PatientInsurance', 'U') IS NOT NULL DROP TABLE dbo.PatientInsurance;
IF OBJECT_ID('dbo.PatientAllergies', 'U') IS NOT NULL DROP TABLE dbo.PatientAllergies;
IF OBJECT_ID('dbo.PatientMedications', 'U') IS NOT NULL DROP TABLE dbo.PatientMedications;
IF OBJECT_ID('dbo.PatientConditions', 'U') IS NOT NULL DROP TABLE dbo.PatientConditions;
IF OBJECT_ID('dbo.Patients', 'U') IS NOT NULL DROP TABLE dbo.Patients;
IF OBJECT_ID('dbo.Doctors', 'U') IS NOT NULL DROP TABLE dbo.Doctors;
GO

-- ── Doctors ─────────────────────────────────────────────────────────────
-- Captured once via the UI's doctor intake form ("assume a doctor is always
-- logged in" — this is the profile shown top-right, not a password-auth table).
CREATE TABLE dbo.Doctors
(
    DoctorId      INT IDENTITY(1,1)   NOT NULL PRIMARY KEY,
    FullName      NVARCHAR(200)       NOT NULL,
    Email         NVARCHAR(256)       NOT NULL,
    Specialty     NVARCHAR(200)       NULL,
    NpiNumber     NVARCHAR(20)        NULL,
    CreatedAt     DATETIME2           NOT NULL CONSTRAINT DF_Doctors_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2           NULL,
    CONSTRAINT UQ_Doctors_Email UNIQUE (Email)
);
GO

-- ── Patients ────────────────────────────────────────────────────────────
CREATE TABLE dbo.Patients
(
    PatientId          INT IDENTITY(1,1)  NOT NULL PRIMARY KEY,
    Mrn                NVARCHAR(50)       NOT NULL,           -- e.g. 'demo-001' -- mirrors the FHIR-cache patient id
    FullName           NVARCHAR(200)      NOT NULL,
    DateOfBirth        DATE               NOT NULL,
    Gender             NVARCHAR(20)       NULL,
    PreferredLanguage  NVARCHAR(50)       NULL,
    ContactNumber      NVARCHAR(30)       NULL,
    Email              NVARCHAR(256)      NULL,
    CreatedAt          DATETIME2          NOT NULL CONSTRAINT DF_Patients_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Patients_Mrn UNIQUE (Mrn)
);
GO
CREATE NONCLUSTERED INDEX IX_Patients_FullName ON dbo.Patients (FullName);
GO

CREATE TABLE dbo.PatientConditions
(
    Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PatientId     INT               NOT NULL,
    ConditionText NVARCHAR(300)     NOT NULL,
    CONSTRAINT FK_PatientConditions_Patients FOREIGN KEY (PatientId) REFERENCES dbo.Patients (PatientId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_PatientConditions_PatientId ON dbo.PatientConditions (PatientId);
GO

CREATE TABLE dbo.PatientMedications
(
    Id              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PatientId       INT               NOT NULL,
    MedicationText  NVARCHAR(300)     NOT NULL,
    CONSTRAINT FK_PatientMedications_Patients FOREIGN KEY (PatientId) REFERENCES dbo.Patients (PatientId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_PatientMedications_PatientId ON dbo.PatientMedications (PatientId);
GO

CREATE TABLE dbo.PatientAllergies
(
    Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PatientId    INT               NOT NULL,
    AllergyText  NVARCHAR(300)     NOT NULL,
    CONSTRAINT FK_PatientAllergies_Patients FOREIGN KEY (PatientId) REFERENCES dbo.Patients (PatientId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_PatientAllergies_PatientId ON dbo.PatientAllergies (PatientId);
GO

-- ── Insurance / Eligibility ─────────────────────────────────────────────
-- Modeled on the eligibility-check screens: payor/plan, network, deductible
-- and out-of-pocket remaining, and the eligibility date window.
CREATE TABLE dbo.PatientInsurance
(
    PatientInsuranceId              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PatientId                       INT               NOT NULL,
    PayorName                       NVARCHAR(200)     NULL,
    PlanName                        NVARCHAR(200)     NULL,
    GroupId                         NVARCHAR(50)      NULL,
    GroupName                       NVARCHAR(200)     NULL,
    MemberId                        NVARCHAR(50)      NULL,
    PPN                             NVARCHAR(100)     NULL,   -- e.g. 'Open Access Plus'
    CopayAmount                     DECIMAL(10,2)     NULL,
    RemainingDeductibleIndividual   DECIMAL(10,2)     NULL,
    RemainingDeductibleFamily       DECIMAL(10,2)     NULL,
    RemainingOutOfPocketIndividual  DECIMAL(10,2)     NULL,
    RemainingOutOfPocketFamily      DECIMAL(10,2)     NULL,
    EligibilityStartDate            DATE              NULL,
    EligibilityEndDate               DATE              NULL,
    IsPrimary                       BIT               NOT NULL CONSTRAINT DF_PatientInsurance_IsPrimary DEFAULT (1),
    LastEligibilityCheckAt          DATETIME2         NULL,
    CreatedAt                       DATETIME2         NOT NULL CONSTRAINT DF_PatientInsurance_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_PatientInsurance_Patients FOREIGN KEY (PatientId) REFERENCES dbo.Patients (PatientId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_PatientInsurance_PatientId ON dbo.PatientInsurance (PatientId);
GO

-- ── Visits (Encounters) ─────────────────────────────────────────────────
-- A patient can have many visits over time; each pipeline run (transcription
-- -> SOAP -> validation) belongs to exactly one visit.
CREATE TABLE dbo.Visits
(
    VisitId    INT IDENTITY(1,1)  NOT NULL PRIMARY KEY,
    PatientId  INT                NOT NULL,
    DoctorId   INT                NULL,
    VisitDate  DATETIME2          NOT NULL CONSTRAINT DF_Visits_VisitDate DEFAULT SYSUTCDATETIME(),
    Status     NVARCHAR(30)       NOT NULL CONSTRAINT DF_Visits_Status DEFAULT ('InProgress'),
    CreatedAt  DATETIME2          NOT NULL CONSTRAINT DF_Visits_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt  DATETIME2          NULL,
    CONSTRAINT FK_Visits_Patients FOREIGN KEY (PatientId) REFERENCES dbo.Patients (PatientId),
    CONSTRAINT FK_Visits_Doctors  FOREIGN KEY (DoctorId)  REFERENCES dbo.Doctors (DoctorId),
    CONSTRAINT CK_Visits_Status CHECK (Status IN ('InProgress', 'Completed'))
);
GO
CREATE INDEX IX_Visits_PatientId ON dbo.Visits (PatientId, VisitDate DESC);
GO

-- ── Transcriptions ──────────────────────────────────────────────────────
CREATE TABLE dbo.Transcriptions
(
    TranscriptionId  INT IDENTITY(1,1)  NOT NULL PRIMARY KEY,
    VisitId          INT                NOT NULL,
    TranscriptText   NVARCHAR(MAX)      NOT NULL,
    Source           NVARCHAR(20)       NOT NULL CONSTRAINT DF_Transcriptions_Source DEFAULT ('Manual'),  -- 'Audio' | 'Manual'
    CreatedAt        DATETIME2          NOT NULL CONSTRAINT DF_Transcriptions_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Transcriptions_Visits FOREIGN KEY (VisitId) REFERENCES dbo.Visits (VisitId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_Transcriptions_VisitId ON dbo.Transcriptions (VisitId);
GO

-- ── SOAP Notes (Subjective / Objective / Assessment / Plan + codes) ────────
CREATE TABLE dbo.SoapNotes
(
    SoapNoteId          INT IDENTITY(1,1)  NOT NULL PRIMARY KEY,
    VisitId             INT                NOT NULL,
    Subjective          NVARCHAR(MAX)      NULL,
    Objective            NVARCHAR(MAX)      NULL,
    Assessment          NVARCHAR(MAX)      NULL,
    [Plan]              NVARCHAR(MAX)      NULL,
    ClinicalReasoning   NVARCHAR(MAX)      NULL,
    FromFallback        BIT                NOT NULL CONSTRAINT DF_SoapNotes_FromFallback DEFAULT (0),
    CreatedAt           DATETIME2          NOT NULL CONSTRAINT DF_SoapNotes_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_SoapNotes_Visits FOREIGN KEY (VisitId) REFERENCES dbo.Visits (VisitId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_SoapNotes_VisitId ON dbo.SoapNotes (VisitId);
GO

CREATE TABLE dbo.SoapNoteDiagnosisCodes  -- ICD-10
(
    Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SoapNoteId    INT               NOT NULL,
    Code          NVARCHAR(20)      NOT NULL,
    Description   NVARCHAR(300)     NULL,
    Confidence    DECIMAL(4,3)      NULL,
    CONSTRAINT FK_SoapNoteDiagnosisCodes_SoapNotes FOREIGN KEY (SoapNoteId) REFERENCES dbo.SoapNotes (SoapNoteId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_SoapNoteDiagnosisCodes_SoapNoteId ON dbo.SoapNoteDiagnosisCodes (SoapNoteId);
GO

CREATE TABLE dbo.SoapNoteProcedureCodes  -- CPT
(
    Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SoapNoteId    INT               NOT NULL,
    Code          NVARCHAR(20)      NOT NULL,
    Description   NVARCHAR(300)     NULL,
    Confidence    DECIMAL(4,3)      NULL,
    CONSTRAINT FK_SoapNoteProcedureCodes_SoapNotes FOREIGN KEY (SoapNoteId) REFERENCES dbo.SoapNotes (SoapNoteId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_SoapNoteProcedureCodes_SoapNoteId ON dbo.SoapNoteProcedureCodes (SoapNoteId);
GO

-- ── Validation Results (medical necessity scorecard) ───────────────────
CREATE TABLE dbo.ValidationResults
(
    ValidationResultId    INT IDENTITY(1,1)  NOT NULL PRIMARY KEY,
    VisitId               INT                NOT NULL,
    SoapNoteId            INT                NULL,
    ApprovalProbability   INT                NOT NULL,
    FromFallback          BIT                NOT NULL CONSTRAINT DF_ValidationResults_FromFallback DEFAULT (0),
    CreatedAt             DATETIME2          NOT NULL CONSTRAINT DF_ValidationResults_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_ValidationResults_Visits FOREIGN KEY (VisitId) REFERENCES dbo.Visits (VisitId) ON DELETE CASCADE,
    CONSTRAINT FK_ValidationResults_SoapNotes FOREIGN KEY (SoapNoteId) REFERENCES dbo.SoapNotes (SoapNoteId),
    CONSTRAINT CK_ValidationResults_ApprovalProbability CHECK (ApprovalProbability BETWEEN 0 AND 100)
);
GO
CREATE INDEX IX_ValidationResults_VisitId ON dbo.ValidationResults (VisitId);
GO

CREATE TABLE dbo.ValidationGaps
(
    Id                    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ValidationResultId    INT               NOT NULL,
    GapIndex              INT               NOT NULL,
    PolicyRef             NVARCHAR(100)     NULL,
    Requirement           NVARCHAR(500)     NULL,
    CurrentDoc            NVARCHAR(500)     NULL,
    Severity              NVARCHAR(20)      NOT NULL,
    CONSTRAINT FK_ValidationGaps_ValidationResults FOREIGN KEY (ValidationResultId) REFERENCES dbo.ValidationResults (ValidationResultId) ON DELETE CASCADE,
    CONSTRAINT CK_ValidationGaps_Severity CHECK (Severity IN ('Critical', 'Major', 'Minor'))
);
GO
CREATE INDEX IX_ValidationGaps_ValidationResultId ON dbo.ValidationGaps (ValidationResultId);
GO

CREATE TABLE dbo.ValidationFixes
(
    Id                    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ValidationResultId    INT               NOT NULL,
    GapIndex              INT               NOT NULL,
    SuggestedLanguage     NVARCHAR(1000)    NULL,
    CONSTRAINT FK_ValidationFixes_ValidationResults FOREIGN KEY (ValidationResultId) REFERENCES dbo.ValidationResults (ValidationResultId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_ValidationFixes_ValidationResultId ON dbo.ValidationFixes (ValidationResultId);
GO

CREATE TABLE dbo.ValidationPolicyReferences
(
    Id                    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ValidationResultId    INT               NOT NULL,
    ReferenceText         NVARCHAR(300)     NOT NULL,
    CONSTRAINT FK_ValidationPolicyReferences_ValidationResults FOREIGN KEY (ValidationResultId) REFERENCES dbo.ValidationResults (ValidationResultId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_ValidationPolicyReferences_ValidationResultId ON dbo.ValidationPolicyReferences (ValidationResultId);
GO

CREATE TABLE dbo.ValidationPassingCriteria
(
    Id                    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ValidationResultId    INT               NOT NULL,
    CriteriaText          NVARCHAR(300)     NOT NULL,
    CONSTRAINT FK_ValidationPassingCriteria_ValidationResults FOREIGN KEY (ValidationResultId) REFERENCES dbo.ValidationResults (ValidationResultId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_ValidationPassingCriteria_ValidationResultId ON dbo.ValidationPassingCriteria (ValidationResultId);
GO

CREATE TABLE dbo.ValidationHumanReviewFlags
(
    Id                    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ValidationResultId    INT               NOT NULL,
    FlagText              NVARCHAR(300)     NOT NULL,
    CONSTRAINT FK_ValidationHumanReviewFlags_ValidationResults FOREIGN KEY (ValidationResultId) REFERENCES dbo.ValidationResults (ValidationResultId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_ValidationHumanReviewFlags_ValidationResultId ON dbo.ValidationHumanReviewFlags (ValidationResultId);
GO

-- ── Audit Log ───────────────────────────────────────────────────────────
-- One row per pipeline step (Transcription / SoapGeneration / Validation /
-- PatientSummary) so every run can be traced end-to-end per visit.
CREATE TABLE dbo.AuditLog
(
    AuditLogId    BIGINT IDENTITY(1,1)  NOT NULL PRIMARY KEY,
    VisitId       INT                   NULL,
    PatientId     INT                   NULL,
    DoctorId      INT                   NULL,
    StepName      NVARCHAR(50)          NOT NULL,   -- Transcription | SoapGeneration | Validation | PatientSummary
    Status        NVARCHAR(20)          NOT NULL,   -- Success | Fallback | Error
    DurationMs    INT                   NULL,
    DetailsJson   NVARCHAR(MAX)         NULL,
    CreatedAt     DATETIME2             NOT NULL CONSTRAINT DF_AuditLog_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AuditLog_Visits   FOREIGN KEY (VisitId)   REFERENCES dbo.Visits (VisitId) ON DELETE CASCADE,
    CONSTRAINT FK_AuditLog_Patients FOREIGN KEY (PatientId) REFERENCES dbo.Patients (PatientId),
    CONSTRAINT FK_AuditLog_Doctors  FOREIGN KEY (DoctorId)  REFERENCES dbo.Doctors (DoctorId),
    CONSTRAINT CK_AuditLog_Status CHECK (Status IN ('Success', 'Fallback', 'Error'))
);
GO
CREATE INDEX IX_AuditLog_VisitId ON dbo.AuditLog (VisitId, CreatedAt);
GO
