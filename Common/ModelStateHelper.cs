using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ScrumRetroApi.Common;

public static class ModelStateHelper
{
    public static Dictionary<string, string[]> ToErrors(ModelStateDictionary modelState)
    {
        var result = new Dictionary<string, List<string>>();
        foreach (var (key, entry) in modelState)
        {
            if (entry.Errors.Count == 0) continue;
            var name = Normalize(key);
            if (!result.TryGetValue(name, out var list))
                result[name] = list = new List<string>();

            foreach (var error in entry.Errors)
            {
                var message = error.Exception is not null || string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "Valor inválido."
                    : error.ErrorMessage;
                if (!list.Contains(message)) list.Add(message);
            }
        }

        return result.ToDictionary(k => k.Key, k => k.Value.ToArray());
    }

    private static string Normalize(string key)
    {
        if (string.IsNullOrEmpty(key)) return "corpo";
        if (key.StartsWith("$.")) key = key[2..];
        return string.Join('.', key.Split('.').Select(ToCamel));
    }

    private static string ToCamel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
