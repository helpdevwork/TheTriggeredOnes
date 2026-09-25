/*
    ClarityClaim-Database — 04_StoredProcedures.sql
    Run after 03_Views.sql. All application data access goes through these
    procedures (no ad-hoc SQL from the API) — parameterized, SQL-injection safe.
    Child collections (codes, gaps, fixes, etc.) are passed as a single JSON
    array parameter and expanded server-side with OPENJSON, to keep each
    pipeline-step persist to one round trip.
*/

USE ClarityClaimDb;
GO

-- ═══════════════════════════ DOCTORS ═══════════════════════════════════

IF OBJECT_ID('dbo.usp_Doctor_Upsert', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Doctor_Upsert;
GO
CREATE PROCEDURE dbo.usp_Doctor_Upsert
    @Email      NVARCHAR(256),
    @FullName   NVARCHAR(200),
    @Specialty  NVARCHAR(200) = NULL,
    @NpiNumber  NVARCHAR(20)  = NULL,
    @DoctorId   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT @DoctorId = DoctorId FROM dbo.Doctors WHERE Email = @Email;

    IF @DoctorId IS NULL
    BEGIN
        INSERT INTO dbo.Doctors (FullName, Email, Specialty, NpiNumber)
        VALUES (@FullName, @Email, @Specialty, @NpiNumber);
        SET @DoctorId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.Doctors
        SET FullName = @FullName, Specialty = @Specialty, NpiNumber = @NpiNumber, UpdatedAt = SYSUTCDATETIME()
        WHERE DoctorId = @DoctorId;
    END
END
GO

IF OBJECT_ID('dbo.usp_Doctor_GetById', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Doctor_GetById;
GO
CREATE PROCEDURE dbo.usp_Doctor_GetById
    @DoctorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DoctorId, FullName, Email, Specialty, NpiNumber, CreatedAt, UpdatedAt
    FROM dbo.Doctors
    WHERE DoctorId = @DoctorId;
END
GO

-- ═══════════════════════════ PATIENTS / DASHBOARD ══════════════════════

IF OBJECT_ID('dbo.usp_Patient_List', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Patient_List;
GO
CREATE PROCEDURE dbo.usp_Patient_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.vw_PatientDashboard ORDER BY FullName;
END
GO

IF OBJECT_ID('dbo.usp_Patient_Search', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Patient_Search;
GO
CREATE PROCEDURE dbo.usp_Patient_Search
    @Keyword NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @kw NVARCHAR(202) = N'%' + @Keyword + N'%';

    SELECT * FROM dbo.vw_PatientDashboard
    WHERE FullName LIKE @kw
       OR Mrn LIKE @kw
       OR PayorName LIKE @kw
       OR PlanName LIKE @kw
       OR ContactNumber LIKE @kw
       OR Email LIKE @kw
    ORDER BY FullName;
END
GO

IF OBJECT_ID('dbo.usp_Patient_GetDetail', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Patient_GetDetail;
GO
-- Returns 5 result sets, read in order by the API: patient+insurance+last visit,
-- conditions, medications, allergies, visit history.
CREATE PROCEDURE dbo.usp_Patient_GetDetail
    @PatientId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM dbo.vw_PatientDashboard WHERE PatientId = @PatientId;

    SELECT ConditionText FROM dbo.PatientConditions WHERE PatientId = @PatientId;

    SELECT MedicationText FROM dbo.PatientMedications WHERE PatientId = @PatientId;

    SELECT AllergyText FROM dbo.PatientAllergies WHERE PatientId = @PatientId;

    SELECT v.VisitId, v.VisitDate, v.Status, d.FullName AS DoctorName
    FROM dbo.Visits v
    LEFT JOIN dbo.Doctors d ON d.DoctorId = v.DoctorId
    WHERE v.PatientId = @PatientId
    ORDER BY v.VisitDate DESC;
END
GO

-- ═══════════════════════════ VISITS ════════════════════════════════════

IF OBJECT_ID('dbo.usp_Visit_Create', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Visit_Create;
GO
CREATE PROCEDURE dbo.usp_Visit_Create
    @PatientId  INT,
    @DoctorId   INT = NULL,
    @VisitId    INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Visits (PatientId, DoctorId, Status)
    VALUES (@PatientId, @DoctorId, 'InProgress');
    SET @VisitId = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('dbo.usp_Visit_Complete', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Visit_Complete;
GO
CREATE PROCEDURE dbo.usp_Visit_Complete
    @VisitId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Visits SET Status = 'Completed', UpdatedAt = SYSUTCDATETIME() WHERE VisitId = @VisitId;
END
GO

IF OBJECT_ID('dbo.usp_Visit_GetHistory', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Visit_GetHistory;
GO
-- Full pipeline history for one visit: transcript, SOAP note + codes,
-- validation result + gaps/fixes/references/criteria/flags, audit trail.
CREATE PROCEDURE dbo.usp_Visit_GetHistory
    @VisitId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1 TranscriptText FROM dbo.Transcriptions WHERE VisitId = @VisitId ORDER BY CreatedAt DESC;

    DECLARE @SoapNoteId INT;
    SELECT TOP 1 @SoapNoteId = SoapNoteId FROM dbo.SoapNotes WHERE VisitId = @VisitId ORDER BY CreatedAt DESC;
    SELECT Subjective, Objective, Assessment, [Plan], ClinicalReasoning FROM dbo.SoapNotes WHERE SoapNoteId = @SoapNoteId;
    SELECT Code, Description, CAST(Confidence AS FLOAT) AS Confidence FROM dbo.SoapNoteDiagnosisCodes WHERE SoapNoteId = @SoapNoteId;
    SELECT Code, Description, CAST(Confidence AS FLOAT) AS Confidence FROM dbo.SoapNoteProcedureCodes WHERE SoapNoteId = @SoapNoteId;

    DECLARE @ValidationResultId INT;
    SELECT TOP 1 @ValidationResultId = ValidationResultId FROM dbo.ValidationResults WHERE VisitId = @VisitId ORDER BY CreatedAt DESC;
    SELECT ApprovalProbability FROM dbo.ValidationResults WHERE ValidationResultId = @ValidationResultId;
    SELECT GapIndex, PolicyRef, Requirement, CurrentDoc, Severity FROM dbo.ValidationGaps WHERE ValidationResultId = @ValidationResultId ORDER BY GapIndex;
    SELECT GapIndex, SuggestedLanguage FROM dbo.ValidationFixes WHERE ValidationResultId = @ValidationResultId ORDER BY GapIndex;
    SELECT ReferenceText FROM dbo.ValidationPolicyReferences WHERE ValidationResultId = @ValidationResultId;
    SELECT CriteriaText FROM dbo.ValidationPassingCriteria WHERE ValidationResultId = @ValidationResultId;
    SELECT FlagText FROM dbo.ValidationHumanReviewFlags WHERE ValidationResultId = @ValidationResultId;

    SELECT AuditLogId, StepName, Status, DurationMs, DetailsJson, CreatedAt
    FROM dbo.AuditLog WHERE VisitId = @VisitId ORDER BY CreatedAt;
END
GO

-- ═══════════════════════════ TRANSCRIPTION ═════════════════════════════

IF OBJECT_ID('dbo.usp_Transcription_Insert', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Transcription_Insert;
GO
CREATE PROCEDURE dbo.usp_Transcription_Insert
    @VisitId          INT,
    @TranscriptText    NVARCHAR(MAX),
    @Source            NVARCHAR(20) = 'Manual',
    @TranscriptionId   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Transcriptions (VisitId, TranscriptText, Source)
    VALUES (@VisitId, @TranscriptText, @Source);
    SET @TranscriptionId = SCOPE_IDENTITY();
END
GO

-- ═══════════════════════════ SOAP NOTES ════════════════════════════════

IF OBJECT_ID('dbo.usp_SoapNote_Insert', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_SoapNote_Insert;
GO
-- @Icd10CodesJson / @CptCodesJson shape: [{"code":"M51.16","description":"...","confidence":0.93}, ...]
CREATE PROCEDURE dbo.usp_SoapNote_Insert
    @VisitId             INT,
    @Subjective          NVARCHAR(MAX) = NULL,
    @Objective           NVARCHAR(MAX) = NULL,
    @Assessment          NVARCHAR(MAX) = NULL,
    @Plan                NVARCHAR(MAX) = NULL,
    @ClinicalReasoning   NVARCHAR(MAX) = NULL,
    @FromFallback        BIT = 0,
    @Icd10CodesJson      NVARCHAR(MAX) = N'[]',
    @CptCodesJson        NVARCHAR(MAX) = N'[]',
    @SoapNoteId          INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    INSERT INTO dbo.SoapNotes (VisitId, Subjective, Objective, Assessment, [Plan], ClinicalReasoning, FromFallback)
    VALUES (@VisitId, @Subjective, @Objective, @Assessment, @Plan, @ClinicalReasoning, @FromFallback);
    SET @SoapNoteId = SCOPE_IDENTITY();

    INSERT INTO dbo.SoapNoteDiagnosisCodes (SoapNoteId, Code, Description, Confidence)
    SELECT @SoapNoteId, j.code, j.description, j.confidence
    FROM OPENJSON(@Icd10CodesJson)
    WITH (code NVARCHAR(20) '$.code', description NVARCHAR(300) '$.description', confidence DECIMAL(4,3) '$.confidence') j;

    INSERT INTO dbo.SoapNoteProcedureCodes (SoapNoteId, Code, Description, Confidence)
    SELECT @SoapNoteId, j.code, j.description, j.confidence
    FROM OPENJSON(@CptCodesJson)
    WITH (code NVARCHAR(20) '$.code', description NVARCHAR(300) '$.description', confidence DECIMAL(4,3) '$.confidence') j;

    COMMIT TRANSACTION;
END
GO

-- ═══════════════════════════ VALIDATION RESULTS ════════════════════════

IF OBJECT_ID('dbo.usp_ValidationResult_Insert', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_ValidationResult_Insert;
GO
-- @GapsJson: [{"gapIndex":0,"policyRef":"LCD L34220","requirement":"...","currentDoc":"...","severity":"Critical"}]
-- @FixesJson: [{"gapIndex":0,"suggestedLanguage":"..."}]
-- @PolicyReferencesJson / @PassingCriteriaJson / @HumanReviewFlagsJson: ["text1","text2"]
CREATE PROCEDURE dbo.usp_ValidationResult_Insert
    @VisitId                 INT,
    @SoapNoteId               INT = NULL,
    @ApprovalProbability      INT,
    @FromFallback             BIT = 0,
    @GapsJson                 NVARCHAR(MAX) = N'[]',
    @FixesJson                NVARCHAR(MAX) = N'[]',
    @PolicyReferencesJson     NVARCHAR(MAX) = N'[]',
    @PassingCriteriaJson      NVARCHAR(MAX) = N'[]',
    @HumanReviewFlagsJson     NVARCHAR(MAX) = N'[]',
    @ValidationResultId       INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    INSERT INTO dbo.ValidationResults (VisitId, SoapNoteId, ApprovalProbability, FromFallback)
    VALUES (@VisitId, @SoapNoteId, @ApprovalProbability, @FromFallback);
    SET @ValidationResultId = SCOPE_IDENTITY();

    INSERT INTO dbo.ValidationGaps (ValidationResultId, GapIndex, PolicyRef, Requirement, CurrentDoc, Severity)
    SELECT @ValidationResultId, j.gapIndex, j.policyRef, j.requirement, j.currentDoc, j.severity
    FROM OPENJSON(@GapsJson)
    WITH (gapIndex INT '$.gapIndex', policyRef NVARCHAR(100) '$.policyRef',
          requirement NVARCHAR(500) '$.requirement', currentDoc NVARCHAR(500) '$.currentDoc',
          severity NVARCHAR(20) '$.severity') j;

    INSERT INTO dbo.ValidationFixes (ValidationResultId, GapIndex, SuggestedLanguage)
    SELECT @ValidationResultId, j.gapIndex, j.suggestedLanguage
    FROM OPENJSON(@FixesJson)
    WITH (gapIndex INT '$.gapIndex', suggestedLanguage NVARCHAR(1000) '$.suggestedLanguage') j;

    INSERT INTO dbo.ValidationPolicyReferences (ValidationResultId, ReferenceText)
    SELECT @ValidationResultId, value FROM OPENJSON(@PolicyReferencesJson);

    INSERT INTO dbo.ValidationPassingCriteria (ValidationResultId, CriteriaText)
    SELECT @ValidationResultId, value FROM OPENJSON(@PassingCriteriaJson);

    INSERT INTO dbo.ValidationHumanReviewFlags (ValidationResultId, FlagText)
    SELECT @ValidationResultId, value FROM OPENJSON(@HumanReviewFlagsJson);

    COMMIT TRANSACTION;
END
GO

-- ═══════════════════════════ AUDIT LOG ═════════════════════════════════

IF OBJECT_ID('dbo.usp_AuditLog_Insert', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_AuditLog_Insert;
GO
CREATE PROCEDURE dbo.usp_AuditLog_Insert
    @VisitId       INT = NULL,
    @PatientId     INT = NULL,
    @DoctorId      INT = NULL,
    @StepName      NVARCHAR(50),
    @Status        NVARCHAR(20),
    @DurationMs    INT = NULL,
    @DetailsJson   NVARCHAR(MAX) = NULL,
    @AuditLogId    BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AuditLog (VisitId, PatientId, DoctorId, StepName, Status, DurationMs, DetailsJson)
    VALUES (@VisitId, @PatientId, @DoctorId, @StepName, @Status, @DurationMs, @DetailsJson);
    SET @AuditLogId = SCOPE_IDENTITY();
END
GO

IF OBJECT_ID('dbo.usp_AuditLog_ListByVisit', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_AuditLog_ListByVisit;
GO
CREATE PROCEDURE dbo.usp_AuditLog_ListByVisit
    @VisitId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT AuditLogId, StepName, Status, DurationMs, DetailsJson, CreatedAt
    FROM dbo.AuditLog
    WHERE VisitId = @VisitId
    ORDER BY CreatedAt;
END
GO
