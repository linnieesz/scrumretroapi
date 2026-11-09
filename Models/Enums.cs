namespace ScrumRetroApi.Models;

public enum UserRole
{
    Member = 0,
    Manager = 1,
    Admin = 2
}

public enum CeremonyType
{
    Review = 0,
    Retrospective = 1
}

public enum CardType
{
    WentWell = 0,              // o que foi bom
    WentBad = 1,               // o que foi ruim
    CanImprove = 2,            // o que pode melhorar
    Praise = 3,                // parabenização pela atuação
    ImprovementSuggestion = 4  // sugestão de melhoria
}

public enum SprintStatus
{
    Open = 0,   // aberta
    Closed = 1  // encerrada
}
