using System.Data;
using System.Text.Json;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Domain.Models;
using Dapper;

namespace ClarityClaim.Infrastructure.SqlServer;

public class PipelinePersistenceRepository : IPipelinePersistenceRepository
{
    private readonly SqlConnectionFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PipelinePersistenceRepository(SqlConnectionFactory factory) => _factory = factory;

    public async Task<int> SaveTranscriptionAsync(int visitId, string transcriptText, string source, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        var p = new DynamicParameters();
        p.Add("VisitId", visitId);
        p.Add("TranscriptText", transcriptText);
        p.Add("Source", source);
        p.Add("TranscriptionId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_Transcription_Insert", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return p.Get<int>("TranscriptionId");
    }

    public async Task<int> SaveSoapNoteAsync(int visitId, SoapNote note, bool fromFallback, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        var p = new DynamicParameters();
        p.Add("VisitId", visitId);
        p.Add("Subjective", note.Subjective);
        p.Add("Objective", note.Objective);
        p.Add("Assessment", note.Assessment);
        p.Add("Plan", note.Plan);
        p.Add("ClinicalReasoning", note.ClinicalReasoning);
        p.Add("FromFallback", fromFallback);
        p.Add("Icd10CodesJson", JsonSerializer.Serialize(note.Icd10Codes, JsonOpts));
        p.Add("CptCodesJson", JsonSerializer.Serialize(note.CptCodes, JsonOpts));
        p.Add("SoapNoteId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_SoapNote_Insert", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return p.Get<int>("SoapNoteId");
    }

    public async Task UpdateSoapNoteAsync(int soapNoteId, SoapNote note, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_SoapNote_Update",
            new
            {
                SoapNoteId = soapNoteId,
                note.Subjective,
                note.Objective,
                note.Assessment,
                note.Plan,
                note.ClinicalReasoning
            },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<int> SaveValidationResultAsync(int visitId, int? soapNoteId, ValidationResult result, bool fromFallback, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        var p = new DynamicParameters();
        p.Add("VisitId", visitId);
        p.Add("SoapNoteId", soapNoteId);
        p.Add("ApprovalProbability", result.ApprovalProbability);
        p.Add("FromFallback", fromFallback);
        p.Add("GapsJson", JsonSerializer.Serialize(result.Gaps.Select((g, i) => new
        {
            gapIndex = i, policyRef = g.PolicyRef, requirement = g.Requirement,
            currentDoc = g.CurrentDoc, severity = g.Severity.ToString()
        }), JsonOpts));
        p.Add("FixesJson", JsonSerializer.Serialize(result.Fixes, JsonOpts));
        p.Add("PolicyReferencesJson", JsonSerializer.Serialize(result.PolicyReferences, JsonOpts));
        p.Add("PassingCriteriaJson", JsonSerializer.Serialize(result.PassingCriteria, JsonOpts));
        p.Add("HumanReviewFlagsJson", JsonSerializer.Serialize(result.HumanReviewFlags, JsonOpts));
        p.Add("ValidationResultId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_ValidationResult_Insert", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return p.Get<int>("ValidationResultId");
    }

    public async Task LogAuditAsync(int? visitId, int? patientId, int? doctorId, string stepName, string status, int? durationMs, string? detailsJson, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        var p = new DynamicParameters();
        p.Add("VisitId", visitId);
        p.Add("PatientId", patientId);
        p.Add("DoctorId", doctorId);
        p.Add("StepName", stepName);
        p.Add("Status", status);
        p.Add("DurationMs", durationMs);
        p.Add("DetailsJson", detailsJson);
        p.Add("AuditLogId", dbType: DbType.Int64, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_AuditLog_Insert", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }
}
