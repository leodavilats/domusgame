using Npgsql;

namespace Domus.Api.Common;

/// <summary>
/// Resolve a conexao com o Postgres a partir da configuracao.
///
/// Aceita duas formas, nesta ordem:
///   1. ConnectionStrings__Postgres  - formato ADO.NET ("Host=...;Database=...")
///   2. DATABASE_URL                 - URI "postgresql://usuário:senha@host:porta/banco"
///      (formato que Railway, Render, Fly, Heroku e Neon expoem por padrao)
///
/// Não existe valor padrao em producao: sem configuracao a aplicacao falha imediatamente com
/// uma mensagem clara, em vez de tentar um localhost que nunca vai existir dentro do container.
/// </summary>
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

    /// <summary>Descricao segura para log: host, porta e banco, nunca a senha.</summary>
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

            // Prefer cifra quando o servidor oferece TLS e continua funcionando quando não ha
            // (caso do Postgres interno de algumas plataformas, em rede privada).
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
