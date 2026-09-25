/*
    ClarityClaim-Database — 05_SeedData.sql
    Run after 04_StoredProcedures.sql. Seeds 5 demo patients with full
    demographics + insurance/eligibility, and ZERO visits (visits are created
    only once a doctor starts an encounter from the dashboard). Safe to re-run
    — skips patients that already exist by Mrn.
*/

USE ClarityClaimDb;
GO

-- ── 1. Margaret Chen — lumbar radiculopathy scenario (LCD L34220) ─────────
-- Matches the FHIR cache patient (demo-001) and the demo encounter script.
IF NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE Mrn = 'demo-001')
BEGIN
    INSERT INTO dbo.Patients (Mrn, FullName, DateOfBirth, Gender, PreferredLanguage, ContactNumber, Email)
    VALUES ('demo-001', 'Margaret Chen', '1966-04-12', 'female', 'English', '910-828-3671', 'margaret.chen@example.com');

    DECLARE @P1 INT = SCOPE_IDENTITY();

    INSERT INTO dbo.PatientConditions (PatientId, ConditionText) VALUES
        (@P1, 'Type 2 diabetes mellitus'), (@P1, 'Hypertension'), (@P1, 'Chronic low back pain');
    INSERT INTO dbo.PatientMedications (PatientId, MedicationText) VALUES
        (@P1, 'Metformin 500mg twice daily'), (@P1, 'Lisinopril 10mg daily');
    INSERT INTO dbo.PatientAllergies (PatientId, AllergyText) VALUES
        (@P1, 'Penicillin');

    INSERT INTO dbo.PatientInsurance
        (PatientId, PayorName, PlanName, GroupId, GroupName, MemberId, PPN, CopayAmount,
         RemainingDeductibleIndividual, RemainingDeductibleFamily,
         RemainingOutOfPocketIndividual, RemainingOutOfPocketFamily,
         EligibilityStartDate, EligibilityEndDate, IsPrimary, LastEligibilityCheckAt)
    VALUES
        (@P1, 'Aetna Life & Casualty', 'Aetna Open Access Plus', '3343452', 'STATE OF COLORADO', '3215-01',
         'Open Access Plus', 10.00, 0.00, 150.00, 350.00, 20000.00,
         '2026-01-01', '2026-12-31', 1, SYSUTCDATETIME());
END
GO

-- ── 2. Robert Nguyen — sleep apnea / polysomnography scenario (LCD L36839) ─
IF NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE Mrn = 'demo-002')
BEGIN
    INSERT INTO dbo.Patients (Mrn, FullName, DateOfBirth, Gender, PreferredLanguage, ContactNumber, Email)
    VALUES ('demo-002', 'Robert Nguyen', '1978-09-03', 'male', 'English', '919-555-0142', 'robert.nguyen@example.com');

    DECLARE @P2 INT = SCOPE_IDENTITY();

    INSERT INTO dbo.PatientConditions (PatientId, ConditionText) VALUES
        (@P2, 'Suspected obstructive sleep apnea'), (@P2, 'Obesity');
    INSERT INTO dbo.PatientMedications (PatientId, MedicationText) VALUES
        (@P2, 'None currently prescribed');

    INSERT INTO dbo.PatientInsurance
        (PatientId, PayorName, PlanName, GroupId, GroupName, MemberId, PPN, CopayAmount,
         RemainingDeductibleIndividual, RemainingDeductibleFamily,
         RemainingOutOfPocketIndividual, RemainingOutOfPocketFamily,
         EligibilityStartDate, EligibilityEndDate, IsPrimary, LastEligibilityCheckAt)
    VALUES
        (@P2, 'Blue Cross Blue Shield', 'BlueEdge PPO', '7788120', 'ACME MANUFACTURING', '7788-14',
         'PPO', 25.00, 200.00, 400.00, 900.00, 4000.00,
         '2026-01-01', '2026-12-31', 1, SYSUTCDATETIME());
END
GO

-- ── 3. Eleanor Whitfield — cognitive assessment scenario (LCD L39266) ─────
IF NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE Mrn = 'demo-003')
BEGIN
    INSERT INTO dbo.Patients (Mrn, FullName, DateOfBirth, Gender, PreferredLanguage, ContactNumber, Email)
    VALUES ('demo-003', 'Eleanor Whitfield', '1951-02-20', 'female', 'English', '704-555-0199', 'eleanor.whitfield@example.com');

    DECLARE @P3 INT = SCOPE_IDENTITY();

    INSERT INTO dbo.PatientConditions (PatientId, ConditionText) VALUES
        (@P3, 'Mild cognitive impairment'), (@P3, 'Hypertension');
    INSERT INTO dbo.PatientMedications (PatientId, MedicationText) VALUES
        (@P3, 'Donepezil 5mg daily'), (@P3, 'Amlodipine 5mg daily');
    INSERT INTO dbo.PatientAllergies (PatientId, AllergyText) VALUES
        (@P3, 'Sulfa drugs');

    INSERT INTO dbo.PatientInsurance
        (PatientId, PayorName, PlanName, GroupId, GroupName, MemberId, PPN, CopayAmount,
         RemainingDeductibleIndividual, RemainingDeductibleFamily,
         RemainingOutOfPocketIndividual, RemainingOutOfPocketFamily,
         EligibilityStartDate, EligibilityEndDate, IsPrimary, LastEligibilityCheckAt)
    VALUES
        (@P3, 'UnitedHealthcare', 'Medicare Advantage', '9921004', 'MEDICARE', '9921-77',
         'HMO', 0.00, 0.00, 0.00, 0.00, 0.00,
         '2026-01-01', '2026-12-31', 1, SYSUTCDATETIME());
END
GO

-- ── 4. David Okafor — general diabetes / metabolic management ─────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE Mrn = 'demo-004')
BEGIN
    INSERT INTO dbo.Patients (Mrn, FullName, DateOfBirth, Gender, PreferredLanguage, ContactNumber, Email)
    VALUES ('demo-004', 'David Okafor', '1984-11-30', 'male', 'English', '312-555-0176', 'david.okafor@example.com');

    DECLARE @P4 INT = SCOPE_IDENTITY();

    INSERT INTO dbo.PatientConditions (PatientId, ConditionText) VALUES
        (@P4, 'Type 2 diabetes mellitus'), (@P4, 'Hyperlipidemia');
    INSERT INTO dbo.PatientMedications (PatientId, MedicationText) VALUES
        (@P4, 'Metformin 1000mg twice daily'), (@P4, 'Atorvastatin 20mg daily');

    INSERT INTO dbo.PatientInsurance
        (PatientId, PayorName, PlanName, GroupId, GroupName, MemberId, PPN, CopayAmount,
         RemainingDeductibleIndividual, RemainingDeductibleFamily,
         RemainingOutOfPocketIndividual, RemainingOutOfPocketFamily,
         EligibilityStartDate, EligibilityEndDate, IsPrimary, LastEligibilityCheckAt)
    VALUES
        (@P4, 'Cigna', 'Open Access Plus', '5567341', 'TECHCORP INDUSTRIES', '5567-22',
         'Open Access Plus', 15.00, 100.00, 250.00, 800.00, 6000.00,
         '2026-01-01', '2026-12-31', 1, SYSUTCDATETIME());
END
GO

-- ── 5. Priya Sharma — general hypertension checkup ─────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Patients WHERE Mrn = 'demo-005')
BEGIN
    INSERT INTO dbo.Patients (Mrn, FullName, DateOfBirth, Gender, PreferredLanguage, ContactNumber, Email)
    VALUES ('demo-005', 'Priya Sharma', '1990-06-15', 'female', 'English', '469-555-0134', 'priya.sharma@example.com');

    DECLARE @P5 INT = SCOPE_IDENTITY();

    INSERT INTO dbo.PatientConditions (PatientId, ConditionText) VALUES
        (@P5, 'Hypertension'), (@P5, 'Seasonal allergies');
    INSERT INTO dbo.PatientMedications (PatientId, MedicationText) VALUES
        (@P5, 'Amlodipine 5mg daily');
    INSERT INTO dbo.PatientAllergies (PatientId, AllergyText) VALUES
        (@P5, 'Latex');

    INSERT INTO dbo.PatientInsurance
        (PatientId, PayorName, PlanName, GroupId, GroupName, MemberId, PPN, CopayAmount,
         RemainingDeductibleIndividual, RemainingDeductibleFamily,
         RemainingOutOfPocketIndividual, RemainingOutOfPocketFamily,
         EligibilityStartDate, EligibilityEndDate, IsPrimary, LastEligibilityCheckAt)
    VALUES
        (@P5, 'Aetna Life & Casualty', 'Choice POS II', '4471290', 'GLOBALTECH SOLUTIONS', '4471-08',
         'POS II', 20.00, 300.00, 600.00, 1200.00, 5000.00,
         '2026-01-01', '2026-12-31', 1, SYSUTCDATETIME());
END
GO

-- Verify: all 5 patients present, 0 visits each
SELECT FullName, Mrn, VisitCount FROM dbo.vw_PatientDashboard ORDER BY FullName;
GO
