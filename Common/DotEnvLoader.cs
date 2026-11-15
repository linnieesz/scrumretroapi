namespace ScrumRetroApi.Common;

/// <summary>
/// Lê um arquivo .env (ex.: o .env.local que a CLI da Neon gera com `neon init` / `neon env pull`) e expõe apenas
/// as chaves permitidas como configuração. Só deve ser usado em Development: em produção as variáveis vêm do ambiente.
/// Nunca registra nem imprime os valores.
/// </summary>
public static class DotEnvLoader
{
    public static void AddIfPresent(IConfigurationBuilder configuration, string path, params string[] allowedKeys)
    {
        if (!File.Exists(path)) return;

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;

            var key = line[..separator].Trim();
            if (!allowedKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            values[key] = value;
        }

        if (values.Count > 0) configuration.AddInMemoryCollection(values);
    }
}
