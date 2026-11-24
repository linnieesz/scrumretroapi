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

        // Recuperação explícita (Seed__Admin__ForceReset=true): garante o admin configurado, mesmo que já exista outro.
        if (seed.ForceReset)
        {
            await ForceResetAdminAsync(db, seed, logger);
            return;
        }

        // Só semeia se não existir nenhum administrador (evita recriar um admin removido de propósito).
        // Atenção: se o MESMO banco já foi usado por outra instância (ex.: sua máquina local), o admin dela vale
        // e o Seed__Admin__* configurado agora é ignorado, sem erro.
        var existingAdmins = await db.Users
            .Where(u => u.Role == UserRole.Admin && !u.IsDeleted)
            .Select(u => u.Email)
            .ToListAsync();
        if (existingAdmins.Count > 0)
        {
            logger.LogInformation(
                "Seed do admin ignorado: já existe(m) administrador(es) neste banco: {Emails}. O Seed:Admin atual NÃO altera usuários existentes. " +
                "Para criar/redefinir o admin configurado, defina Seed__Admin__ForceReset=true e reinicie (depois remova a variável).",
                string.Join(", ", existingAdmins));
            return;
        }

        var email = InputRules.NormalizeEmail(seed.Email);
        if (!IsValid(seed, email, logger)) return;

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            logger.LogWarning(
                "Seed do admin ignorado: já existe um usuário (não administrador) com o e-mail configurado. " +
                "Use Seed__Admin__ForceReset=true para promovê-lo e redefinir a senha.");
            return;
        }

        db.Users.Add(UserFactory.Create(seed.FullName, email, null, null, null, UserRole.Admin, seed.Password));
        await db.SaveChangesAsync();
        logger.LogInformation("Administrador inicial criado: {Email}", email);
    }

    /// <summary>Cria o admin configurado ou, se o e-mail já existe, redefine senha/papel/status. Nunca registra a senha.</summary>
    private static async Task ForceResetAdminAsync(AppDbContext db, SeedAdminOptions seed, ILogger logger)
    {
        var email = InputRules.NormalizeEmail(seed.Email);
        if (!IsValid(seed, email, logger)) return;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            db.Users.Add(UserFactory.Create(seed.FullName, email, null, null, null, UserRole.Admin, seed.Password));
            await db.SaveChangesAsync();
            logger.LogWarning("Seed__Admin__ForceReset ativo: administrador CRIADO ({Email}). REMOVA a variável Seed__Admin__ForceReset.", email);
            return;
        }

        var (hash, salt) = PasswordHasher.HashPassword(seed.Password);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.Role = UserRole.Admin;
        user.IsActive = true;
        user.IsDeleted = false;
        user.DeletedAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogWarning(
            "Seed__Admin__ForceReset ativo: administrador REDEFINIDO ({Email}) - senha trocada, papel Admin, conta ativa. " +
            "REMOVA a variável Seed__Admin__ForceReset (senão a senha volta ao valor da variável a cada reinício).", email);
    }

    private static bool IsValid(SeedAdminOptions seed, string email, ILogger logger)
    {
        var v = new ValidationBuilder();
        InputRules.FullName(v, seed.FullName);
        InputRules.Email(v, email);
        InputRules.Password(v, seed.Password, "password");
        if (v.HasErrors)
        {
            try { v.ThrowIfAny(); }
            catch (RequestValidationException ex)
            {
                // Só nomes de campo e regras violadas: nunca o valor da senha.
                logger.LogWarning("Seed do admin ignorado: configuração 'Seed:Admin' inválida -> {Errors}",
                    string.Join(" | ", ex.Errors!.SelectMany(e => e.Value.Select(m => $"{e.Key}: {m}"))));
            }

            return false;
        }

        if (seed.Password != seed.Password.Trim())
            logger.LogWarning("Seed:Admin:Password tem espaço/quebra de linha no início ou no fim: confira a variável no Render.");

        return true;
    }
}
