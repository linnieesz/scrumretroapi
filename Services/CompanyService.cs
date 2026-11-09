using Microsoft.EntityFrameworkCore;
using ScrumRetroApi.Common;
using ScrumRetroApi.Data;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.Services;

public interface ICompanyService
{
    /// <summary>Picker do cadastro (público): empresas ativas, só id e nome.</summary>
    Task<IReadOnlyList<CompanyOptionDto>> OptionsAsync(CancellationToken ct);

    Task<IReadOnlyList<CompanyDto>> ListAsync(string? search, CancellationToken ct);
    Task<CompanyDto> GetAsync(Guid id, CancellationToken ct);
    Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken ct);
    Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken ct);

    /// <summary>Busca de membros DENTRO da empresa (sem e-mail). Admin em qualquer empresa; Manager só na sua.</summary>
    Task<IReadOnlyList<CompanyMemberDto>> SearchMembersAsync(Guid companyId, string? search, UserRole? role,
        CancellationToken ct);
}

public sealed class CompanyService(AppDbContext db, ICurrentUser current) : ICompanyService
{
    public async Task<IReadOnlyList<CompanyOptionDto>> OptionsAsync(CancellationToken ct) =>
        await db.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CompanyOptionDto(c.Id, c.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CompanyDto>> ListAsync(string? search, CancellationToken ct)
    {
        EnsureAdmin();

        var query = db.Companies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            var cnpjTerm = $"%{Cnpj.Normalize(search)}%";
            query = query.Where(c =>
                EF.Functions.Like(c.Name, term) ||
                (c.LegalName != null && EF.Functions.Like(c.LegalName, term)) ||
                EF.Functions.Like(c.Cnpj, cnpjTerm));
        }

        // Ordena ANTES de projetar para o record (o EF não traduz OrderBy sobre membros de construtor).
        return await ToDtos(query.OrderBy(c => c.Name)).ToListAsync(ct);
    }

    public async Task<CompanyDto> GetAsync(Guid id, CancellationToken ct)
    {
        EnsureAdmin();
        return await LoadDtoAsync(id, ct);
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyRequest request, CancellationToken ct)
    {
        EnsureAdmin(); // apenas o Admin cria empresas

        var cnpj = Cnpj.Normalize(request.Cnpj);
        Validate(request.Name, request.LegalName, request.Cnpj);
        await EnsureCnpjAvailableAsync(cnpj, null, ct);

        var now = DateTime.UtcNow;
        var company = new Company
        {
            Name = request.Name.Trim(),
            LegalName = InputRules.Clean(request.LegalName),
            Cnpj = cnpj,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);
        return await LoadDtoAsync(company.Id, ct);
    }

    public async Task<CompanyDto> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken ct)
    {
        EnsureAdmin();

        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == id, ct)
                      ?? throw new NotFoundException("Empresa não encontrada.");

        var cnpj = Cnpj.Normalize(request.Cnpj);
        var v = Validate(request.Name, request.LegalName, request.Cnpj, throwNow: false);
        if (request.IsActive is null) v.Add("isActive", "Informe se a empresa está ativa.");
        v.ThrowIfAny();

        await EnsureCnpjAvailableAsync(cnpj, company.Id, ct);

        company.Name = request.Name.Trim();
        company.LegalName = InputRules.Clean(request.LegalName);
        company.Cnpj = cnpj;
        company.IsActive = request.IsActive!.Value;
        company.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await LoadDtoAsync(company.Id, ct);
    }

    public async Task<IReadOnlyList<CompanyMemberDto>> SearchMembersAsync(Guid companyId, string? search,
        UserRole? role, CancellationToken ct)
    {
        if (!current.IsManagerOrAdmin)
            throw new ForbiddenException("Apenas Admin e Manager podem buscar membros.");

        if (!await db.Companies.AnyAsync(c => c.Id == companyId, ct))
            throw new NotFoundException("Empresa não encontrada.");

        // Manager só enxerga a própria empresa; o Admin enxerga todas.
        if (!current.IsAdmin)
        {
            var uid = current.Id;
            var myCompanyId = await db.Users.Where(u => u.Id == uid).Select(u => u.CompanyId).FirstOrDefaultAsync(ct);
            if (myCompanyId is null)
                throw new ForbiddenException("Você não está vinculado a uma empresa. Peça a um administrador para vinculá-lo.");
            if (myCompanyId != companyId)
                throw new ForbiddenException("Você só pode buscar membros da sua própria empresa.");
        }

        var query = db.Users.AsNoTracking()
            .Where(u => u.CompanyId == companyId && !u.IsDeleted && u.IsActive);

        // A busca é só por NOME (e departamento). Buscar por e-mail permitiria descobrir e-mails por tentativa,
        // mesmo sem exibi-los no resultado.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(u => EF.Functions.Like(u.FullName, term) ||
                                     (u.Department != null && EF.Functions.Like(u.Department, term)));
        }

        if (role.HasValue)
        {
            var r = role.Value;
            query = query.Where(u => u.Role == r);
        }

        return await query
            .OrderBy(u => u.FullName)
            .Take(50)
            .Select(u => new CompanyMemberDto(u.Id, u.FullName, u.Department, u.Role))
            .ToListAsync(ct);
    }

    // ---------- Helpers ----------

    private void EnsureAdmin()
    {
        if (!current.IsAdmin)
            throw new ForbiddenException("Apenas administradores podem gerenciar empresas.");
    }

    private static ValidationBuilder Validate(string? name, string? legalName, string? cnpj, bool throwNow = true)
    {
        var v = new ValidationBuilder();
        var n = name?.Trim();
        if (string.IsNullOrEmpty(n)) v.Add("name", "O nome da empresa é obrigatório.");
        else if (n.Length < 2 || n.Length > 150) v.Add("name", "O nome da empresa deve ter entre 2 e 150 caracteres.");
        InputRules.OptionalText(v, "legalName", "A razão social", legalName, 200);
        if (string.IsNullOrWhiteSpace(cnpj)) v.Add("cnpj", "O CNPJ é obrigatório.");
        else if (!Cnpj.IsValid(cnpj)) v.Add("cnpj", "Informe um CNPJ válido.");
        if (throwNow) v.ThrowIfAny();
        return v;
    }

    private async Task EnsureCnpjAvailableAsync(string cnpj, Guid? ignoreId, CancellationToken ct)
    {
        var ignore = ignoreId ?? Guid.Empty;
        if (await db.Companies.AnyAsync(c => c.Cnpj == cnpj && c.Id != ignore, ct))
            throw new ConflictException("Já existe uma empresa cadastrada com este CNPJ.");
    }

    private async Task<CompanyDto> LoadDtoAsync(Guid id, CancellationToken ct) =>
        await ToDtos(db.Companies.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Empresa não encontrada.");

    private static IQueryable<CompanyDto> ToDtos(IQueryable<Company> query) =>
        query.Select(c => new CompanyDto(
            c.Id,
            c.Name,
            c.LegalName,
            c.Cnpj,
            c.IsActive,
            c.Members.Count(m => !m.IsDeleted),
            c.CreatedAt,
            c.UpdatedAt));
}
