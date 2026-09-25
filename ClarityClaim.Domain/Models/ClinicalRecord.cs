using ClarityClaim.Domain.Enums;

namespace ClarityClaim.Domain.Models;

// The master object that flows through the entire pipeline. Created empty in
// Screen 1, enriched at each stage, fully populated by Screen 3.
public record ClinicalRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public FhirPatientContext Patient { get; set; } = new();
    public string Transcript { get; set; } = string.Empty;
    public SoapNote SoapNote { get; set; } = new();
    public ValidationResult Validation { get; set; } = new();
    public string PatientSummary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public record FhirPatientContext
{
    public string PatientId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public List<string> Conditions { get; set; } = [];
    public List<string> Medications { get; set; } = [];
    public List<string> Allergies { get; set; } = [];
}

public record SoapNote
{
    public string Subjective { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Assessment { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public List<CodedDiagnosis> Icd10Codes { get; set; } = [];
    public List<CodedProcedure> CptCodes { get; set; } = [];
    public string ClinicalReasoning { get; set; } = string.Empty;
}

public record CodedDiagnosis(
    string Code,        // e.g. "M51.16"
    string Description, // e.g. "Intervertebral disc degeneration, lumbar region"
    double Confidence    // 0.0 - 1.0
);

public record CodedProcedure(
    string Code,
    string Description,
    double Confidence
);

public record ValidationResult
{
    public int ApprovalProbability { get; set; }  // 0-100
    public List<ValidationGap> Gaps { get; set; } = [];
    public List<ValidationFix> Fixes { get; set; } = [];
    public List<string> PolicyReferences { get; set; } = [];
    public List<string> PassingCriteria { get; set; } = [];
    public List<string> HumanReviewFlags { get; set; } = [];
}

public record ValidationGap(
    string PolicyRef,            // "LCD L34220"
    string Requirement,          // "minimum 4 weeks conservative treatment"
    string CurrentDoc,           // what the SOAP note currently says
    ValidationSeverity Severity  // Critical | Major | Minor
);

public record ValidationFix(
    int GapIndex,
    string SuggestedLanguage
);
