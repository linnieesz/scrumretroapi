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
}
