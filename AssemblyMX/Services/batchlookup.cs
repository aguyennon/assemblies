using Microsoft.Data.SqlClient;

namespace AssemblyMX.Services;

public class BatchLookupService
{
    private readonly string _connectionString;
    private readonly ILogger<BatchLookupService> _logger;

    public BatchLookupService(IConfiguration config, ILogger<BatchLookupService> logger)
    {
        _logger = logger;

        var sql = config.GetSection("SqlServer");
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = sql["Host"],
            InitialCatalog = sql["Database"],
            TrustServerCertificate = sql.GetValue<bool>("TrustServerCertificate"),
            ConnectTimeout = 5
        };

        if (sql.GetValue<bool>("IntegratedSecurity"))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = sql["User"];
            builder.Password = sql["Password"];
        }

        _connectionString = builder.ConnectionString;
    }

    private static (string CommandeId, string ProduitId, string QuantiteId)? Parse(string scanned)
    {
        var parts = scanned.Trim().Split('-');
        if (parts.Length != 4) return null;

        var commandeId = $"{parts[0]}-{parts[1]}";
        return (commandeId, parts[2], parts[3]);
    }

    public async Task<string?> GetBatchNoAsync(string scannedCode)
    {
        var parsed = Parse(scannedCode);
        if (parsed is null)
        {
            _logger.LogWarning("Scan '{Code}' did not parse into 4 parts", scannedCode);
            return null;
        }

        var (commandeId, produitId, quantiteId) = parsed.Value;

        var produitAlt = produitId.TrimStart('0');
        if (produitAlt.Length == 0) produitAlt = "0";

        const string query = @"
            SELECT TOP 1 [BatchNO]
            FROM [dbo].[CODEBAR]
            WHERE [CommandeID] = @commande
              AND [ProduitID]  IN (@produit, @produitAlt)
              AND [QuantiteID] = @quantite";

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@commande", commandeId);
            cmd.Parameters.AddWithValue("@produit", produitId);
            cmd.Parameters.AddWithValue("@produitAlt", produitAlt);
            cmd.Parameters.AddWithValue("@quantite", quantiteId);

            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQL lookup failed for scan '{Code}'", scannedCode);
            return null;
        }
    }
}