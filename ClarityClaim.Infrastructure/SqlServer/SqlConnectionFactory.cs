using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ClarityClaim.Infrastructure.SqlServer;

// One factory, one named connection string ("ClarityClaimDb"). Every repository
// opens a fresh short-lived SqlConnection per call via this factory -- the
// standard Dapper pattern; ADO.NET pools the underlying physical connections.
public class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("ClarityClaimDb")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:ClarityClaimDb in configuration.");
    }

    public async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
