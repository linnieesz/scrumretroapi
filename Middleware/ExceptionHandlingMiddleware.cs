using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScrumRetroApi.Common;

namespace ScrumRetroApi.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            await ApiErrorWriter.WriteAsync(context, ex.StatusCode, ex.Code, ex.Message, ex.Errors);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Cliente cancelou a requisição; nada a responder.
            logger.LogDebug("Requisição cancelada pelo cliente.");
        }
        catch (BadHttpRequestException ex)
        {
            logger.LogWarning(ex, "Requisição malformada.");
            await ApiErrorWriter.WriteAsync(context, 400, "requisicao_invalida", "Requisição inválida.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogWarning(ex, "Violação de unicidade.");
            await ApiErrorWriter.WriteAsync(context, 409, "conflito", "Já existe um registro com os mesmos dados.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro não tratado em {Method} {Path}", context.Request.Method, context.Request.Path);
            await ApiErrorWriter.WriteAsync(context, 500, "erro_interno",
                "Ocorreu um erro inesperado. Tente novamente mais tarde.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19, SqliteExtendedErrorCode: 2067 or 1555 };
}
