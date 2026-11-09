using Npgsql;

namespace ScrumRetroApi.Common;

/// <summary>
/// Resolve ConnectionStrings:Default para PostgreSQL. Aceita o formato do Npgsql ("Host=...;Database=...") e também
/// a URL que o Render entrega (postgres://usuario:senha@host:5432/banco), convertendo-a.
/// Nenhum valor padrão é embutido: sem configuração, a aplicação não sobe (evita rodar sem querer em outro banco).
/// </summary>
public static class ConnectionStringHelper
{
    public static string Resolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default não configurada. Defina a variável de ambiente ConnectionStrings__Default " +
                "(ou, em desenvolvimento, use 'dotnet user-secrets set \"ConnectionStrings:Default\" \"...\"').");
        }

        var value = raw.Trim();
        var isUrl = value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
        if (!isUrl) return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null
        };

        var result = builder.ConnectionString;

        // URL externa do Render/Neon/Supabase costuma exigir TLS: ?sslmode=require.
        if (uri.Query.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase))
            result += ";SSL Mode=Require;Trust Server Certificate=true";

        return result;
    }
}
