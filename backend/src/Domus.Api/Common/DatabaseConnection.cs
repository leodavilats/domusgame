using Npgsql;

namespace Domus.Api.Common;

public static class DatabaseConnection
{
    public static string Resolve(IConfiguration configuration)
    {
        var direct = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(direct)) return direct;

        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl)) return FromUri(databaseUrl);

        throw new InvalidOperationException(
            "Nenhuma conexao com o banco configurada. Defina a variavel de ambiente " +
            "ConnectionStrings__Postgres (formato Host=...;Port=5432;Database=...;Username=...;Password=...) " +
            "ou DATABASE_URL (formato postgresql://usuário:senha@host:5432/banco).");
    }

    public static string Describe(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return $"{builder.Host}:{builder.Port}/{builder.Database}";
        }
        catch (Exception)
        {
            return "(não foi possivel interpretar a connection string)";
        }
    }

    private static string FromUri(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                "DATABASE_URL invalida. Use o formato postgresql://usuário:senha@host:5432/banco.");
        }

        var credentials = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.Trim('/'),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty,
            SslMode = ParseSslMode(uri.Query)
        };

        return builder.ConnectionString;
    }

    private static SslMode ParseSslMode(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return SslMode.Prefer;

        var value = query
            .TrimStart('?')
            .Split('&')
            .Select(pair => pair.Split('=', 2))
            .FirstOrDefault(pair => pair.Length == 2 && pair[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            ?[1];

        return Enum.TryParse<SslMode>(value?.Replace("-", string.Empty), ignoreCase: true, out var parsed)
            ? parsed
            : SslMode.Prefer;
    }
}
