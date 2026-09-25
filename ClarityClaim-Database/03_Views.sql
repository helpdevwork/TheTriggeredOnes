/*
    ClarityClaim-Database — 03_Views.sql
    Run after 02_Schema.sql.
*/

USE ClarityClaimDb;
GO

IF OBJECT_ID('dbo.vw_PatientDashboard', 'V') IS NOT NULL DROP VIEW dbo.vw_PatientDashboard;
GO

-- One row per patient for the landing-page grid: demographics, primary
-- insurance/eligibility window, and last-visit summary in a single read.
CREATE VIEW dbo.vw_PatientDashboard
AS
SELECT
    p.PatientId,
    p.Mrn,
    p.FullName,
    p.DateOfBirth,
    p.Gender,
    p.PreferredLanguage,
    p.ContactNumber,
    p.Email,
    ins.PayorName,
    ins.PlanName,
    ins.GroupName,
    ins.PPN,
    ins.EligibilityStartDate,
    ins.EligibilityEndDate,
    ins.RemainingDeductibleIndividual,
    ins.RemainingOutOfPocketIndividual,
    ins.LastEligibilityCheckAt,
    lv.LastVisitId,
    lv.LastVisitDate,
    lv.LastVisitStatus,
    ISNULL(vc.VisitCount, 0) AS VisitCount
FROM dbo.Patients p
OUTER APPLY (
    SELECT TOP 1 pi.PayorName, pi.PlanName, pi.GroupName, pi.PPN,
                 pi.EligibilityStartDate, pi.EligibilityEndDate,
                 pi.RemainingDeductibleIndividual, pi.RemainingOutOfPocketIndividual,
                 pi.LastEligibilityCheckAt
    FROM dbo.PatientInsurance pi
    WHERE pi.PatientId = p.PatientId AND pi.IsPrimary = 1
    ORDER BY pi.CreatedAt DESC
) ins
OUTER APPLY (
    SELECT TOP 1 v.VisitId AS LastVisitId, v.VisitDate AS LastVisitDate, v.Status AS LastVisitStatus
    FROM dbo.Visits v
    WHERE v.PatientId = p.PatientId
    ORDER BY v.VisitDate DESC
) lv
OUTER APPLY (
    SELECT COUNT(*) AS VisitCount FROM dbo.Visits v2 WHERE v2.PatientId = p.PatientId
) vc;
GO
