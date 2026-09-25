using System.Data;
using ClarityClaim.Domain.Enums;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Domain.Models;
using Dapper;

namespace ClarityClaim.Infrastructure.SqlServer;

public class VisitRepository : IVisitRepository
{
    private readonly SqlConnectionFactory _factory;

    public VisitRepository(SqlConnectionFactory factory) => _factory = factory;

    public async Task<int> CreateAsync(int patientId, int? doctorId, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        var p = new DynamicParameters();
        p.Add("PatientId", patientId);
        p.Add("DoctorId", doctorId);
        p.Add("VisitId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_Visit_Create", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return p.Get<int>("VisitId");
    }

    public async Task CompleteAsync(int visitId, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_Visit_Complete", new { VisitId = visitId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<VisitHistory> GetHistoryAsync(int visitId, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            "dbo.usp_Visit_GetHistory", new { VisitId = visitId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));

        var transcript = await multi.ReadSingleOrDefaultAsync<TranscriptRow>();
        var soapRow = await multi.ReadSingleOrDefaultAsync<SoapNoteRow>();
        var icd10 = (await multi.ReadAsync<CodedDiagnosis>()).ToList();
        var cpt = (await multi.ReadAsync<CodedProcedure>()).ToList();
        var validationRow = await multi.ReadSingleOrDefaultAsync<ValidationResultRow>();
        var gapRows = (await multi.ReadAsync<GapRow>()).ToList();
        var fixes = (await multi.ReadAsync<ValidationFix>()).ToList();
        var policyRefs = (await multi.ReadAsync<string>()).ToList();
        var passingCriteria = (await multi.ReadAsync<string>()).ToList();
        var humanReviewFlags = (await multi.ReadAsync<string>()).ToList();
        var auditRows = (await multi.ReadAsync<AuditLogEntry>()).ToList();

        SoapNote? soapNote = soapRow is null ? null : new SoapNote
        {
            Subjective = soapRow.Subjective ?? "",
            Objective = soapRow.Objective ?? "",
            Assessment = soapRow.Assessment ?? "",
            Plan = soapRow.Plan ?? "",
            ClinicalReasoning = soapRow.ClinicalReasoning ?? "",
            Icd10Codes = icd10,
            CptCodes = cpt
        };

        ValidationResult? validation = validationRow is null ? null : new ValidationResult
        {
            ApprovalProbability = validationRow.ApprovalProbability,
            Gaps = gapRows.Select(g => new ValidationGap(
                g.PolicyRef ?? "", g.Requirement ?? "", g.CurrentDoc ?? "",
                Enum.Parse<ValidationSeverity>(g.Severity, ignoreCase: true))).ToList(),
            Fixes = fixes,
            PolicyReferences = policyRefs,
            PassingCriteria = passingCriteria,
            HumanReviewFlags = humanReviewFlags
        };

        return new VisitHistory
        {
            Transcript = transcript?.TranscriptText,
            SoapNote = soapNote,
            Validation = validation,
            AuditTrail = auditRows
        };
    }

    private record TranscriptRow(string TranscriptText);
    private record SoapNoteRow(string? Subjective, string? Objective, string? Assessment, string? Plan, string? ClinicalReasoning);
    private record ValidationResultRow(int ApprovalProbability);
    private record GapRow(int GapIndex, string? PolicyRef, string? Requirement, string? CurrentDoc, string Severity);
}
