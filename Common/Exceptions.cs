namespace ScrumRetroApi.Common;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    public IDictionary<string, string[]>? Errors { get; }

    public AppException(int statusCode, string code, string message, IDictionary<string, string[]>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Errors = errors;
    }
}

public sealed class BadRequestException(string message) : AppException(400, "requisicao_invalida", message);

public sealed class UnauthorizedException(string message = "Autenticação necessária.")
    : AppException(401, "nao_autenticado", message);

public sealed class ForbiddenException(string message = "Você não tem permissão para realizar esta ação.")
    : AppException(403, "acesso_negado", message);

public sealed class NotFoundException(string message = "Recurso não encontrado.")
    : AppException(404, "nao_encontrado", message);

public sealed class ConflictException(string message) : AppException(409, "conflito", message);

public sealed class RequestValidationException(IDictionary<string, string[]> errors)
    : AppException(400, "validacao", "Um ou mais campos são inválidos.", errors);
