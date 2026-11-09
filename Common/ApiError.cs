using System.Text.Json.Serialization;

namespace ScrumRetroApi.Common;

public sealed record ApiError(
    int Status,
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IDictionary<string, string[]>? Errors,
    string? TraceId);

public static class ApiErrorWriter
{
    public static Task WriteAsync(HttpContext context, int status, string code, string message,
        IDictionary<string, string[]>? errors = null)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ApiError(status, code, message, errors, context.TraceIdentifier));
    }
}
