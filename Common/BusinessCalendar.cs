using Microsoft.Extensions.Options;

namespace ScrumRetroApi.Common;

/// <summary>Datas de negócio: "hoje" no fuso configurado e dias úteis (segunda a sexta; feriados não são considerados).</summary>
public sealed class BusinessCalendar(IOptions<SprintOptions> options)
{
    private readonly TimeZoneInfo _timeZone = Resolve(options.Value.TimeZoneId);

    public DateOnly Today() => ToLocalDate(DateTime.UtcNow);

    /// <summary>Converte um instante UTC (vindo do banco, sem Kind) para a data local do fuso configurado.</summary>
    public DateOnly ToLocalDate(DateTime utc)
    {
        var asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(asUtc, _timeZone));
    }

    public static bool IsBusinessDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>A própria data, se for dia útil; senão a próxima segunda-feira.</summary>
    public static DateOnly OnOrNextBusinessDay(DateOnly date)
    {
        while (!IsBusinessDay(date)) date = date.AddDays(1);
        return date;
    }

    /// <summary>Próximo dia útil estritamente depois da data informada.</summary>
    public static DateOnly NextBusinessDay(DateOnly date) => OnOrNextBusinessDay(date.AddDays(1));

    private static TimeZoneInfo Resolve(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Local;
    }
}
