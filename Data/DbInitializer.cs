using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScrumRetroApi.Common;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var seed = services.GetRequiredService<IOptions<SeedAdminOptions>>().Value;

        await db.Database.EnsureCreatedAsync();
        await UpgradeExistingDatabaseAsync(db);

        // Só semeia se não existir nenhum administrador (evita recriar um admin removido de propósito).
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin && !u.IsDeleted))
            return;

        var email = InputRules.NormalizeEmail(seed.Email);
        var v = new ValidationBuilder();
        InputRules.FullName(v, seed.FullName);
        InputRules.Email(v, email);
        InputRules.Password(v, seed.Password, "password");
        if (v.HasErrors)
        {
            logger.LogWarning("Seed do admin ignorado: configuração 'Seed:Admin' inválida (nome, e-mail ou senha).");
            return;
        }

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            logger.LogWarning("Seed do admin ignorado: já existe um usuário (não administrador) com o e-mail configurado.");
            return;
        }

        db.Users.Add(UserFactory.Create(seed.FullName, email, null, null, null, UserRole.Admin, seed.Password));
        await db.SaveChangesAsync();
        logger.LogInformation("Administrador inicial criado: {Email}", email);
    }

    /// <summary>
    /// EnsureCreated() não altera um banco já existente. Para quem já tem o scrumretro.db, aplica as mudanças
    /// de esquema sem apagar dados: Sprints.StartDate, tabela Companies e Users.CompanyId.
    /// (Bancos novos já nascem completos e nada acontece aqui.)
    /// </summary>
    private static async Task UpgradeExistingDatabaseAsync(AppDbContext db)
    {
        // Sprints.StartDate (preenchida com a data de criação nas sprints antigas)
        if (!await ColumnExistsAsync(db, "Sprints", "StartDate"))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sprints ADD COLUMN StartDate TEXT NOT NULL DEFAULT '0001-01-01'");
            await db.Database.ExecuteSqlRawAsync("UPDATE Sprints SET StartDate = date(CreatedAt)");
        }

        // Empresas
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Companies" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Companies" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "LegalName" TEXT NULL,
                "Cnpj" TEXT NOT NULL,
                "IsActive" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            )
            """);
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Companies_Cnpj\" ON \"Companies\" (\"Cnpj\")");

        // Users.CompanyId (usuários antigos ficam sem empresa até o Admin vinculá-los: PUT /api/users/{id})
        if (!await ColumnExistsAsync(db, "Users", "CompanyId"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Users ADD COLUMN CompanyId TEXT NULL REFERENCES Companies (Id) ON DELETE RESTRICT");
        }
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Users_CompanyId\" ON \"Users\" (\"CompanyId\")");
    }

    private static async Task<bool> ColumnExistsAsync(AppDbContext db, string table, string column)
    {
        var rows = await db.Database
            .SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info('{table}') WHERE name = '{column}'")
            .ToListAsync();
        return rows.FirstOrDefault() > 0;
    }
}
