using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ScrumRetroApi.Common;
using ScrumRetroApi.Data;
using ScrumRetroApi.Middleware;
using ScrumRetroApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configuração ----------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Configuração inválida: 'Jwt:Key' deve ter pelo menos 32 bytes.");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<RegistrationOptions>(builder.Configuration.GetSection(RegistrationOptions.SectionName));
builder.Services.Configure<SeedAdminOptions>(builder.Configuration.GetSection(SeedAdminOptions.SectionName));
builder.Services.Configure<SprintOptions>(builder.Configuration.GetSection(SprintOptions.SectionName));

// ---------- Banco ----------
var connectionString = ConnectionStringHelper.Resolve(builder.Configuration.GetConnectionString("Default"));
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// ---------- Serviços ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<BusinessCalendar>();
builder.Services.AddScoped<IProjectAccessService, ProjectAccessService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ISprintService, SprintService>();
builder.Services.AddScoped<ICardService, CardService>();
builder.Services.AddScoped<ISuggestionService, SuggestionService>();

// ---------- Controllers, JSON e validação em português ----------
builder.Services
    .AddControllers(options =>
    {
        // Não gera "required" implícito em strings não anuláveis (usamos mensagens explícitas em português).
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

        var messages = options.ModelBindingMessageProvider;
        messages.SetValueIsInvalidAccessor(value => $"O valor '{value}' é inválido.");
        messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"O valor '{value}' não é válido para {field}.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "O corpo da requisição é obrigatório.");
        messages.SetValueMustNotBeNullAccessor(value => $"O campo {value} é obrigatório.");
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var error = new ApiError(400, "validacao", "Um ou mais campos são inválidos.",
                ModelStateHelper.ToErrors(context.ModelState), context.HttpContext.TraceIdentifier);
            return new BadRequestObjectResult(error);
        };
    });

// ---------- Autenticação JWT (stateless) ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // mantém "sub", "role" etc. como estão no token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        options.Events = new JwtBearerEvents
        {
            // Token válido não basta: o usuário precisa continuar ativo e com o mesmo papel.
            OnTokenValidated = async context =>
            {
                var sub = context.Principal?.FindFirstValue("sub");
                var role = context.Principal?.FindFirstValue("role");
                if (!Guid.TryParse(sub, out var userId))
                {
                    context.Fail("Token inválido.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var user = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.IsActive, u.IsDeleted, u.Role })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                if (user is null || user.IsDeleted || !user.IsActive || user.Role.ToString() != role)
                    context.Fail("Sessão inválida.");
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                return ApiErrorWriter.WriteAsync(context.HttpContext, 401, "nao_autenticado",
                    "Autenticação necessária ou token inválido/expirado.");
            },
            OnForbidden = context =>
                ApiErrorWriter.WriteAsync(context.HttpContext, 403, "acesso_negado",
                    "Você não tem permissão para acessar este recurso.")
        };
    });

// Todo endpoint exige JWT, exceto os marcados com [AllowAnonymous] (login, cadastro e papéis).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// ---------- CORS (front Angular) ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ScrumRetroApi",
        Version = "v1",
        Description = "API para Sprint Review e Retrospectiva do Scrum."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Cole apenas o token JWT (sem o prefixo 'Bearer')."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

// ---------- Pipeline ----------
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "ScrumRetroApi v1"));
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
