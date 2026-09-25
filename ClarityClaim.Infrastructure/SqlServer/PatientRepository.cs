using System.Data;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Domain.Models;
using Dapper;

namespace ClarityClaim.Infrastructure.SqlServer;

public class PatientRepository : IPatientRepository
{
    private readonly SqlConnectionFactory _factory;

    public PatientRepository(SqlConnectionFactory factory) => _factory = factory;

    public async Task<List<PatientDashboardRow>> ListDashboardAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<PatientDashboardRow>(new CommandDefinition(
            "dbo.usp_Patient_List", commandType: CommandType.StoredProcedure, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<List<PatientDashboardRow>> SearchAsync(string keyword, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        var rows = await conn.QueryAsync<PatientDashboardRow>(new CommandDefinition(
            "dbo.usp_Patient_Search", new { Keyword = keyword },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<PatientDetail?> GetDetailAsync(int patientId, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            "dbo.usp_Patient_GetDetail", new { PatientId = patientId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));

        var dashboard = await multi.ReadSingleOrDefaultAsync<PatientDashboardRow>();
        if (dashboard is null) return null;

        var conditions = (await multi.ReadAsync<string>()).ToList();
        var medications = (await multi.ReadAsync<string>()).ToList();
        var allergies = (await multi.ReadAsync<string>()).ToList();
        var visits = (await multi.ReadAsync<VisitSummary>()).ToList();

        return new PatientDetail
        {
            Dashboard = dashboard,
            Conditions = conditions,
            Medications = medications,
            Allergies = allergies,
            Visits = visits
        };
    }
}
