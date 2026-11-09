using System.Text.RegularExpressions;

namespace ScrumRetroApi.Common;

public sealed class ValidationBuilder
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public bool HasErrors => _errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
            _errors[field] = list = new List<string>();
        list.Add(message);
    }

    public void ThrowIfAny()
    {
        if (_errors.Count > 0)
            throw new RequestValidationException(_errors.ToDictionary(k => k.Key, k => k.Value.ToArray()));
    }
}

/// <summary>Regras de entrada reutilizadas pelos serviços (a validação não depende só dos atributos).</summary>
public static class InputRules
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PhoneRegex =
        new(@"^[0-9\s()+\-.]{8,30}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static string? Clean(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    public static void FullName(ValidationBuilder v, string? value, string field = "fullName")
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) v.Add(field, "O nome completo é obrigatório.");
        else if (t.Length < 3 || t.Length > 150) v.Add(field, "O nome completo deve ter entre 3 e 150 caracteres.");
    }

    public static void Email(ValidationBuilder v, string? value, string field = "email")
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) v.Add(field, "O e-mail é obrigatório.");
        else if (t.Length > 254 || !EmailRegex.IsMatch(t)) v.Add(field, "Informe um e-mail válido.");
    }

    public static void Password(ValidationBuilder v, string? value, string field = "password")
    {
        if (string.IsNullOrEmpty(value))
        {
            v.Add(field, "A senha é obrigatória.");
            return;
        }

        if (value.Length < 8 || value.Length > 100)
            v.Add(field, "A senha deve ter entre 8 e 100 caracteres.");
        if (!value.Any(char.IsLetter) || !value.Any(char.IsDigit))
            v.Add(field, "A senha deve conter letras e números.");
    }

    public static void OptionalText(ValidationBuilder v, string field, string label, string? value, int max)
    {
        if (value is not null && value.Trim().Length > max)
            v.Add(field, $"{label} deve ter no máximo {max} caracteres.");
    }

    public static void RequiredText(ValidationBuilder v, string field, string? value, int max,
        string requiredMessage, string tooLongMessage)
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) v.Add(field, requiredMessage);
        else if (t.Length > max) v.Add(field, tooLongMessage);
    }

    public static void Phone(ValidationBuilder v, string? value, string field = "phone")
    {
        var t = value?.Trim();
        if (!string.IsNullOrEmpty(t) && !PhoneRegex.IsMatch(t))
            v.Add(field, "Informe um telefone válido (somente números, espaços, +, -, ponto e parênteses).");
    }

    public static void Profile(ValidationBuilder v, string? fullName, string? email, string? department,
        string? phone, string? bio)
    {
        FullName(v, fullName);
        Email(v, email);
        OptionalText(v, "department", "O departamento", department, 100);
        Phone(v, phone);
        OptionalText(v, "bio", "A bio", bio, 500);
    }
}
