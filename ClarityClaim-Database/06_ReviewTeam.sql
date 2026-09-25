/*
    ClarityClaim-Database — 06_ReviewTeam.sql
    Run after 05_SeedData.sql. Adds the review-team workflow: doctors send a
    finalized SOAP note to a reviewer instead of running validation themselves
    (validation/medical-necessity sign-off is a reviewer/coder function, not
    typically the treating physician's). Safe to re-run.
*/

USE ClarityClaimDb;
GO

-- ── Review Team Members ──────────────────────────────────────────────────
IF OBJECT_ID('dbo.ReviewTeamMembers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReviewTeamMembers
    (
        ReviewTeamMemberId  INT IDENTITY(1,1)  NOT NULL PRIMARY KEY,
        FullName            NVARCHAR(200)      NOT NULL,
        Email               NVARCHAR(256)      NOT NULL,
        Specialty           NVARCHAR(200)      NULL,
        CreatedAt           DATETIME2          NOT NULL CONSTRAINT DF_ReviewTeamMembers_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_ReviewTeamMembers_Email UNIQUE (Email)
    );
END
GO

-- ── Visits: reviewer assignment + PendingReview status ─────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Visits') AND name = 'AssignedReviewTeamMemberId')
BEGIN
    ALTER TABLE dbo.Visits ADD AssignedReviewTeamMemberId INT NULL;
    ALTER TABLE dbo.Visits ADD CONSTRAINT FK_Visits_ReviewTeamMembers
        FOREIGN KEY (AssignedReviewTeamMemberId) REFERENCES dbo.ReviewTeamMembers (ReviewTeamMemberId);
END
GO

-- Widen the Status check constraint to include 'PendingReview' (drop + recreate)
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Visits_Status')
    ALTER TABLE dbo.Visits DROP CONSTRAINT CK_Visits_Status;
GO
ALTER TABLE dbo.Visits ADD CONSTRAINT CK_Visits_Status CHECK (Status IN ('InProgress', 'PendingReview', 'Completed'));
GO

-- ── Stored Procedures ───────────────────────────────────────────────────

IF OBJECT_ID('dbo.usp_ReviewTeamMember_List', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_ReviewTeamMember_List;
GO
CREATE PROCEDURE dbo.usp_ReviewTeamMember_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ReviewTeamMemberId, FullName, Email, Specialty
    FROM dbo.ReviewTeamMembers
    ORDER BY FullName;
END
GO

IF OBJECT_ID('dbo.usp_Visit_AssignReviewer', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Visit_AssignReviewer;
GO
CREATE PROCEDURE dbo.usp_Visit_AssignReviewer
    @VisitId              INT,
    @ReviewTeamMemberId   INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Visits
    SET AssignedReviewTeamMemberId = @ReviewTeamMemberId,
        Status = 'PendingReview',
        UpdatedAt = SYSUTCDATETIME()
    WHERE VisitId = @VisitId;
END
GO

IF OBJECT_ID('dbo.usp_SoapNote_Update', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_SoapNote_Update;
GO
-- Persists the doctor's final edits before handing off to review -- does not
-- touch the ICD-10/CPT code rows, which the UI doesn't let the doctor edit.
CREATE PROCEDURE dbo.usp_SoapNote_Update
    @SoapNoteId         INT,
    @Subjective         NVARCHAR(MAX) = NULL,
    @Objective          NVARCHAR(MAX) = NULL,
    @Assessment         NVARCHAR(MAX) = NULL,
    @Plan               NVARCHAR(MAX) = NULL,
    @ClinicalReasoning  NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.SoapNotes
    SET Subjective = @Subjective, Objective = @Objective, Assessment = @Assessment,
        [Plan] = @Plan, ClinicalReasoning = @ClinicalReasoning
    WHERE SoapNoteId = @SoapNoteId;
END
GO

IF OBJECT_ID('dbo.usp_Visit_GetReviewContext', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Visit_GetReviewContext;
GO
-- Everything the reviewer's tab needs on load: who the patient/ordering doctor
-- are, the reviewer's own name (for the header), and the exact SOAP note +
-- codes the doctor sent -- 4 result sets, read in this order by the API.
CREATE PROCEDURE dbo.usp_Visit_GetReviewContext
    @VisitId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        v.VisitId, v.Status,
        p.FullName AS PatientFullName, p.DateOfBirth AS PatientDateOfBirth, p.Gender AS PatientGender,
        d.FullName AS DoctorFullName,
        r.FullName AS ReviewerFullName
    FROM dbo.Visits v
    JOIN dbo.Patients p ON p.PatientId = v.PatientId
    LEFT JOIN dbo.Doctors d ON d.DoctorId = v.DoctorId
    LEFT JOIN dbo.ReviewTeamMembers r ON r.ReviewTeamMemberId = v.AssignedReviewTeamMemberId
    WHERE v.VisitId = @VisitId;

    DECLARE @SoapNoteId INT;
    SELECT TOP 1 @SoapNoteId = SoapNoteId FROM dbo.SoapNotes WHERE VisitId = @VisitId ORDER BY CreatedAt DESC;
    SELECT SoapNoteId, Subjective, Objective, Assessment, [Plan], ClinicalReasoning
    FROM dbo.SoapNotes WHERE SoapNoteId = @SoapNoteId;

    SELECT Code, Description, CAST(Confidence AS FLOAT) AS Confidence FROM dbo.SoapNoteDiagnosisCodes WHERE SoapNoteId = @SoapNoteId;
    SELECT Code, Description, CAST(Confidence AS FLOAT) AS Confidence FROM dbo.SoapNoteProcedureCodes WHERE SoapNoteId = @SoapNoteId;
END
GO

-- ── Seed: review team members ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.ReviewTeamMembers)
BEGIN
    INSERT INTO dbo.ReviewTeamMembers (FullName, Email, Specialty) VALUES
        ('Susan Cole', 'susan.cole@clarityclaim.example', 'Utilization Review'),
        ('Mark Feldstein', 'mark.feldstein@clarityclaim.example', 'Medical Coding'),
        ('Amira Hassan', 'amira.hassan@clarityclaim.example', 'Medical Director'),
        ('Priya Natarajan', 'priya.natarajan@clarityclaim.example', 'Compliance Review');
END
GO
