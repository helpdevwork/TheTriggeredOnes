using System.Data;
using ClarityClaim.Domain.Interfaces;
using ClarityClaim.Domain.Models;
using Dapper;

namespace ClarityClaim.Infrastructure.SqlServer;

public class DoctorRepository : IDoctorRepository
{
    private readonly SqlConnectionFactory _factory;

    public DoctorRepository(SqlConnectionFactory factory) => _factory = factory;

    public async Task<DoctorProfile> UpsertAsync(string fullName, string email, string? specialty, string? npiNumber, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        var p = new DynamicParameters();
        p.Add("Email", email);
        p.Add("FullName", fullName);
        p.Add("Specialty", specialty);
        p.Add("NpiNumber", npiNumber);
        p.Add("DoctorId", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await conn.ExecuteAsync(new CommandDefinition(
            "dbo.usp_Doctor_Upsert", p, commandType: CommandType.StoredProcedure, cancellationToken: ct));

        var doctorId = p.Get<int>("DoctorId");
        return (await GetByIdAsync(doctorId, ct))!;
    }

    public async Task<DoctorProfile?> GetByIdAsync(int doctorId, CancellationToken ct = default)
    {
        using var conn = await _factory.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<DoctorProfile>(new CommandDefinition(
            "dbo.usp_Doctor_GetById", new { DoctorId = doctorId },
            commandType: CommandType.StoredProcedure, cancellationToken: ct));
    }
}
