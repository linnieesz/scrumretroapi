using ScrumRetroApi.Models;

namespace ScrumRetroApi.Common;

/// <summary>Estado mínimo do usuário usado para validar o JWT a cada requisição (guardado em cache por 30 s).</summary>
public sealed record UserAuthState(bool IsActive, bool IsDeleted, UserRole Role);
