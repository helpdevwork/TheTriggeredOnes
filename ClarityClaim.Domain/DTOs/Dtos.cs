using System.ComponentModel.DataAnnotations;
using ClarityClaim.Domain.Models;
using ClarityClaim.Domain.Validation;

namespace ClarityClaim.Domain.DTOs;

// ── REQUEST DTOs ──────────────────────────────────────────
public record GenerateSoapRequest(
    string Transcript,
    FhirPatientContext PatientContext,
    int? VisitId = null  // when supplied, the pipeline step and result are persisted + audited
);

public record ValidateDocumentRequest(
    SoapNote SoapNote,
    List<string> Icd10Codes,  // ["M51.16", "M54.5"]
    int? VisitId = null,
    int? SoapNoteId = null
);

public record PatientSummaryRequest(
    SoapNote SoapNote,
    string Language  // "en" | "es" | "hi" | "zh"
);

public record DoctorUpsertRequest(
    [Required(ErrorMessage = "Full name is required.")]
    [RegularExpression(@"^[A-Za-z ]+$", ErrorMessage = "Full name may only contain letters and spaces.")]
    string FullName,

    [Required(ErrorMessage = "Email is required.")]
    [RegularExpression(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", ErrorMessage = "Enter a valid email address.")]
    string Email,

    [RegularExpression(@"^[A-Za-z ]*$", ErrorMessage = "Specialty may only contain letters and spaces.")]
    string? Specialty,

    [NpiCheckDigit]
    string? NpiNumber
);

public record CreateVisitRequest(int? DoctorId);

public record SaveTranscriptRequest(int? VisitId, string TranscriptText);

public record UpdateSoapNoteRequest(SoapNote SoapNote);

public record AssignReviewerRequest(int ReviewTeamMemberId);

// ── RESPONSE DTOs ─────────────────────────────────────────
public record TranscriptResponse(
    string FullText,
    List<TranscriptSegment> Segments
);
public record TranscriptSegment(double Start, double End, string Text);

public record SoapResponse(
    SoapNote SoapNote,
    bool FromFallback,  // true if live inference failed
    int? SoapNoteId = null  // set when VisitId was supplied and the note was persisted
);

public record ValidationResponse(
    Models.ValidationResult Result,
    bool FromFallback
);

public record PatientSummaryResponse(string SummaryText, string Language);

public record RegulatoryUpdate(
    string LcdId,
    string Title,
    string ChangeDescription,
    string DetectedAt
);
