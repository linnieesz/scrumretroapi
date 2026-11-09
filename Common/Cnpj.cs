using System.Text.RegularExpressions;

namespace ScrumRetroApi.Common;

/// <summary>
/// CNPJ numérico e alfanumérico (vigente desde julho/2026): 12 posições [0-9A-Z] + 2 dígitos verificadores numéricos.
/// Módulo 11; cada caractere vale (código ASCII - 48), então A=17 ... Z=42 e o CNPJ numérico antigo valida igual.
/// </summary>
public static class Cnpj
{
    private static readonly int[] Weights1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
    private static readonly int[] Weights2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
    private static readonly Regex Shape = new(@"^[0-9A-Z]{12}[0-9]{2}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Remove pontuação (. / - e espaços) e coloca em maiúsculas.</summary>
    public static string Normalize(string? value) =>
        new string((value ?? string.Empty)
                .Where(c => c is not ('.' or '/' or '-') && !char.IsWhiteSpace(c))
                .ToArray())
            .ToUpperInvariant();

    public static bool IsValid(string? value)
    {
        var cnpj = Normalize(value);
        if (!Shape.IsMatch(cnpj)) return false;
        if (cnpj.Distinct().Count() == 1) return false; // 00000000000000, 11111111111111...

        var dv1 = CheckDigit(cnpj.AsSpan(0, 12), Weights1);
        var dv2 = CheckDigit(cnpj.AsSpan(0, 13), Weights2);
        return cnpj[12] - '0' == dv1 && cnpj[13] - '0' == dv2;
    }

    private static int CheckDigit(ReadOnlySpan<char> chars, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < chars.Length; i++)
            sum += (chars[i] - '0') * weights[i];

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
