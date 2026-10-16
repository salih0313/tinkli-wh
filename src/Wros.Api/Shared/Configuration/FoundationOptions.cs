using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;

namespace Wros.Api.Shared.Configuration;

public sealed class DatabaseOptions
{
    public string DefaultConnection { get; set; } = "";

    public static bool IsValid(DatabaseOptions options)
    {
        try
        {
            var connection = new SqlConnectionStringBuilder(options.DefaultConnection);
            return !string.IsNullOrWhiteSpace(connection.DataSource)
                && !string.IsNullOrWhiteSpace(connection.InitialCatalog)
                && !connection.PersistSecurityInfo;
        }
        catch (ArgumentException) { return false; }
    }
    public static bool IsSupportedOnCurrentPlatform(DatabaseOptions options)
    {
        if (OperatingSystem.IsWindows() || !IsValid(options)) return true;
        var sql = new SqlConnectionStringBuilder(options.DefaultConnection);
        return !sql.DataSource.TrimStart().StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSecure(DatabaseOptions options)
    {
        if (!IsValid(options)) return false;
        var sql = new SqlConnectionStringBuilder(options.DefaultConnection);
        return sql.Encrypt.ToString() != "False" && !sql.TrustServerCertificate;
    }
}

public sealed class JwtOptions
{
    [Required] public string Issuer { get; set; } = "";
    [Required] public string Audience { get; set; } = "";
    [Range(1, 60)] public int AccessTokenMinutes { get; set; }
}

public sealed class CorsOptions
{
    public string[] AllowedOrigins { get; set; } = [];
    public static bool IsValid(CorsOptions options) => options.AllowedOrigins.Length > 0
        && options.AllowedOrigins.All(origin => !origin.Contains('*')
            && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || uri.Scheme == "http")
            && uri.GetLeftPart(UriPartial.Authority) == origin
            && string.IsNullOrEmpty(uri.UserInfo));
}

public sealed class InventoryOptions
{
    [Range(0, 3650)] public int ExpiryAlertDays { get; set; } = 30;
}

public sealed class WrosSessionOptions
{
    [Range(1, 60)] public int SessionMinutes { get; set; } = 15;
}

// Retained configuration flags for compatibility; account seeding is owned by an EF migration.
public sealed class SeedOptions
{
    public bool Enabled { get; set; }
    public bool SampleDataEnabled { get; set; }
}

public sealed class IntegrationOptions
{
    public string[] AllowedClients { get; set; } = [];
}
