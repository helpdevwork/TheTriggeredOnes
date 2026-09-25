using ClarityClaim.Domain.DTOs;
using ClarityClaim.Domain.Models;

namespace ClarityClaim.Domain.Interfaces;

public interface ILanguageModelClient
{
    Task<string> CompleteAsync(
        string modelId,
        string systemPrompt,
        string userPrompt,
        float temperature = 0.1f,
        int maxTokens = 4096,
        CancellationToken ct = default
    );
}

public interface ITranscriptionService
{
    Task<TranscriptResponse> TranscribeAsync(string audioFilePath, CancellationToken ct = default);
}

public interface IFhirService
{
    Task<FhirPatientContext> GetPatientContextAsync(string patientId, CancellationToken ct = default);
}

// Lexical (TF-IDF) policy search -- no embedding model is available on the
// configured Ollama-compatible server, so retrieval ranks policy chunks by
// term relevance to the query instead of vector similarity.
public interface IPolicySearchService
{
    Task IndexDocumentAsync(string text, string source, string lcdId);
    Task<List<PolicyChunk>> RetrieveAsync(string query, int topK = 3);
}

public interface IPdfIndexingService
{
    Task IndexAllPoliciesAsync(string policiesFolderPath);
}

public record PolicyChunk(string Text, string Source, string LcdId, int Page);

// ── SQL Server data access -- ClarityClaim-Database ─────────────────────

public interface IDoctorRepository
{
    Task<DoctorProfile> UpsertAsync(string fullName, string email, string? specialty, string? npiNumber, CancellationToken ct = default);
    Task<DoctorProfile?> GetByIdAsync(int doctorId, CancellationToken ct = default);
}

public interface IPatientRepository
{
    Task<List<PatientDashboardRow>> ListDashboardAsync(CancellationToken ct = default);
    Task<List<PatientDashboardRow>> SearchAsync(string keyword, CancellationToken ct = default);
    Task<PatientDetail?> GetDetailAsync(int patientId, CancellationToken ct = default);
}

public interface IVisitRepository
{
    Task<int> CreateAsync(int patientId, int? doctorId, CancellationToken ct = default);
    Task CompleteAsync(int visitId, CancellationToken ct = default);
    Task<VisitHistory> GetHistoryAsync(int visitId, CancellationToken ct = default);
}

// Persists each pipeline step's output for audit/history, and writes the
// audit-trail row alongside it. Failures here must never break the API
// response -- callers catch and log, matching the fail-gracefully principle.
public interface IPipelinePersistenceRepository
{
    Task<int> SaveTranscriptionAsync(int visitId, string transcriptText, string source, CancellationToken ct = default);
    Task<int> SaveSoapNoteAsync(int visitId, SoapNote note, bool fromFallback, CancellationToken ct = default);
    Task UpdateSoapNoteAsync(int soapNoteId, SoapNote note, CancellationToken ct = default);
    Task<int> SaveValidationResultAsync(int visitId, int? soapNoteId, ValidationResult result, bool fromFallback, CancellationToken ct = default);
    Task LogAuditAsync(int? visitId, int? patientId, int? doctorId, string stepName, string status, int? durationMs, string? detailsJson, CancellationToken ct = default);
}

// The review-team hand-off workflow: a doctor sends a finalized SOAP note to
// one member of the review/coding team instead of running validation directly.
public interface IReviewTeamRepository
{
    Task<List<ReviewTeamMember>> ListAsync(CancellationToken ct = default);
    Task AssignReviewerAsync(int visitId, int reviewTeamMemberId, CancellationToken ct = default);
    Task<VisitReviewContext?> GetReviewContextAsync(int visitId, CancellationToken ct = default);
}
