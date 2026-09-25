using System.Data;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Domain.Models;
using Dapper;

namespace ClarityClaim.Infrastructure.SqlServer;

public class ReviewTeamRepository : IReviewTeamRepository
{
    private readonly SqlConnectionFactory _factory;

    public ReviewTeamRepository(SqlConnectionFactory factory) => _factory = factory;

    public async Task<List<ReviewTeamMember>> ListAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ReviewTeamMember>(new CommandDefinition(
            "dbo.usp_ReviewTeamMember_List", commandType: CommandType.StoredProcedure, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task AssignReviewerAsync(int visitId, int reviewTeamMemberId, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_Visit_AssignReviewer", new { VisitId = visitId, ReviewTeamMemberId = reviewTeamMemberId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }

    public async Task<VisitReviewContext?> GetReviewContextAsync(int visitId, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            "dbo.usp_Visit_GetReviewContext", new { VisitId = visitId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));

        var header = await multi.ReadSingleOrDefaultAsync<VisitReviewContext>();
        if (header is null) return null;

        var soapRow = await multi.ReadSingleOrDefaultAsync<SoapNoteRow>();
        var icd10 = (await multi.ReadAsync<CodedDiagnosis>()).ToList();
        var cpt = (await multi.ReadAsync<CodedProcedure>()).ToList();

        if (soapRow is not null)
        {
            header.SoapNoteId = soapRow.SoapNoteId;
            header.SoapNote = new SoapNote
            {
                Subjective = soapRow.Subjective ?? "",
                Objective = soapRow.Objective ?? "",
                Assessment = soapRow.Assessment ?? "",
                Plan = soapRow.Plan ?? "",
                ClinicalReasoning = soapRow.ClinicalReasoning ?? "",
                Icd10Codes = icd10,
                CptCodes = cpt
            };
        }

        return header;
    }

    private record SoapNoteRow(int SoapNoteId, string? Subjective, string? Objective, string? Assessment, string? Plan, string? ClinicalReasoning);
}
