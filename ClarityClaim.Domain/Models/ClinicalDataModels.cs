namespace ClarityClaim.Domain.Models;

// Doctor profile captured once via the UI's intake form -- "assume a doctor is
// always logged in" means this is a session profile, not password auth.
public record DoctorProfile
{
    public int DoctorId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public string? NpiNumber { get; set; }
}

// One row per patient for the landing-page dashboard grid -- backed by
// dbo.vw_PatientDashboard (demographics + primary insurance/eligibility + last visit).
public record PatientDashboardRow
{
    public int PatientId { get; set; }
    public string Mrn { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? PayorName { get; set; }
    public string? PlanName { get; set; }
    public string? GroupName { get; set; }
    public string? PPN { get; set; }
    public DateTime? EligibilityStartDate { get; set; }
    public DateTime? EligibilityEndDate { get; set; }
    public decimal? RemainingDeductibleIndividual { get; set; }
    public decimal? RemainingOutOfPocketIndividual { get; set; }
    public DateTime? LastEligibilityCheckAt { get; set; }
    public int? LastVisitId { get; set; }
    public DateTime? LastVisitDate { get; set; }
    public string? LastVisitStatus { get; set; }
    public int VisitCount { get; set; }
}

public record VisitSummary(int VisitId, DateTime VisitDate, string Status, string? DoctorName);

// Full expanded detail for one patient -- dashboard row + child collections.
public record PatientDetail
{
    public PatientDashboardRow Dashboard { get; set; } = new();
    public List<string> Conditions { get; set; } = [];
    public List<string> Medications { get; set; } = [];
    public List<string> Allergies { get; set; } = [];
    public List<VisitSummary> Visits { get; set; } = [];
}

public record AuditLogEntry(long AuditLogId, string StepName, string Status, int? DurationMs, string? DetailsJson, DateTime CreatedAt);

// Full pipeline history for one visit -- transcript, SOAP note + codes,
// validation result + children, and the audit trail.
public record VisitHistory
{
    public string? Transcript { get; set; }
    public SoapNote? SoapNote { get; set; }
    public ValidationResult? Validation { get; set; }
    public List<AuditLogEntry> AuditTrail { get; set; } = [];
}

// A member of the review/coding team a doctor can hand a finished SOAP note
// to -- medical-necessity validation is a reviewer/coder function, not
// something the treating physician runs on their own note.
public record ReviewTeamMember
{
    public int ReviewTeamMemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Specialty { get; set; }
}

// What the reviewer's tab loads: who the patient/ordering doctor are, the
// reviewer's own name (shown top-right), and the exact SOAP note the doctor sent.
public record VisitReviewContext
{
    public int VisitId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PatientFullName { get; set; } = string.Empty;
    public DateTime PatientDateOfBirth { get; set; }
    public string? PatientGender { get; set; }
    public string? DoctorFullName { get; set; }
    public string? ReviewerFullName { get; set; }
    public int? SoapNoteId { get; set; }
    public SoapNote? SoapNote { get; set; }
}
