using ConsoleApp1.API.Endpoints;
using ConsoleApp1.Application.Contracts.Auth;
using ConsoleApp1.Application.Contracts.Mcp;
using ConsoleApp1.Application.Contracts.Startup;
using ConsoleApp1.Application.Observability;
using ConsoleApp1.Application.Services;
using ConsoleApp1.Infrastructure.Health;
using ConsoleApp1.Infrastructure.Persistence;
using ConsoleApp1.Infrastructure.Workers;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text.Json;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var otlpEndpoint = builder.Configuration["OpenTelemetry:Otlp:Endpoint"];
var localExecution = builder.Configuration.GetSection("LocalExecution").Get<LocalExecutionOptions>() ?? new LocalExecutionOptions();
var configuredConnectionString = builder.Configuration.GetConnectionString("HotelDb");
var databaseDecision = ResolveDatabaseBootstrapDecision(configuredConnectionString, localExecution);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddOptions<LocalExecutionOptions>()
    .Bind(builder.Configuration.GetSection("LocalExecution"))
    .Validate(options =>
            options.PreferredDatabaseProvider.Equals("inmemory", StringComparison.OrdinalIgnoreCase) ||
            options.PreferredDatabaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase),
        "LocalExecution:PreferredDatabaseProvider deve ser 'inmemory' ou 'postgres'.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.InMemoryDatabaseName),
        "LocalExecution:InMemoryDatabaseName é obrigatório.")
    .ValidateOnStart();

builder.Services.AddOptions<OtlpOptions>()
    .Bind(builder.Configuration.GetSection("OpenTelemetry:Otlp"))
    .Validate(options =>
            string.IsNullOrWhiteSpace(options.Endpoint) ||
            Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _),
        "OpenTelemetry:Otlp:Endpoint deve ser uma URI absoluta válida ou vazio.")
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer é obrigatório.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience é obrigatório.")
    .Validate(options => options.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes deve ser maior que zero.")
    .Validate(options => options.Keys.Count > 0 || !string.IsNullOrWhiteSpace(options.SigningKey),
        "Jwt:Keys ou Jwt:SigningKey deve ser informado.")
    .Validate(options =>
    {
        if (options.Keys.Count > 0)
        {
            return options.Keys.All(key =>
                !string.IsNullOrWhiteSpace(key.KeyId) &&
                !string.IsNullOrWhiteSpace(key.Secret) &&
                key.Secret.Length >= 32);
        }

        return !string.IsNullOrWhiteSpace(options.SigningKey) && options.SigningKey.Length >= 32;
    }, "JWT inválido: chaves precisam de KeyId e Secret com ao menos 32 caracteres.")
    .ValidateOnStart();

builder.Services.AddOptions<AuthClientOptions>()
    .Bind(builder.Configuration.GetSection("Auth"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId), "Auth:ClientId é obrigatório.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ClientSecret), "Auth:ClientSecret é obrigatório.")
    .Validate(options => options.RefreshTokenDays > 0, "Auth:RefreshTokenDays deve ser maior que zero.")
    .Validate(options => options.MaxFailedAttempts > 0, "Auth:MaxFailedAttempts deve ser maior que zero.")
    .Validate(options => options.LockoutMinutes > 0, "Auth:LockoutMinutes deve ser maior que zero.")
    .ValidateOnStart();

builder.Services.AddOptions<ComplianceOptions>()
    .Bind(builder.Configuration.GetSection("Compliance"))
    .Validate(options => options.SecurityAuditRetentionDays > 0,
        "Compliance:SecurityAuditRetentionDays deve ser maior que zero.")
    .Validate(options => options.IpAnonymizationDays > 0,
        "Compliance:IpAnonymizationDays deve ser maior que zero.")
    .Validate(options => options.SecurityAuditRetentionDays >= options.IpAnonymizationDays,
        "Compliance inválido: retenção deve ser >= janela de anonimização.")
    .ValidateOnStart();

builder.Services.AddOptions<McpQuotaOptions>()
    .Bind(builder.Configuration.GetSection("Mcp:Quota"))
    .Validate(options => options.RequestsPerMinutePerClient > 0,
        "Mcp:Quota:RequestsPerMinutePerClient deve ser maior que zero.")
    .Validate(options => options.RequestsPerMinutePerClient <= 10_000,
        "Mcp:Quota:RequestsPerMinutePerClient está acima do limite permitido (10000).")
    .ValidateOnStart();

builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

if (jwtOptions.Keys.Count == 0)
{
    if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
    {
        throw new InvalidOperationException("Jwt:SigningKey deve possuir ao menos 32 caracteres.");
    }

    jwtOptions.Keys = [new JwtSigningKeyOptions { KeyId = jwtOptions.CurrentKeyId, Secret = jwtOptions.SigningKey }];
}

if (jwtOptions.Keys.Any(key => string.IsNullOrWhiteSpace(key.Secret) || key.Secret.Length < 32 || string.IsNullOrWhiteSpace(key.KeyId)))
{
    throw new InvalidOperationException("Todas as chaves JWT devem possuir KeyId e Secret com ao menos 32 caracteres.");
}

var jwtValidationKeys = jwtOptions.Keys
    .Select(key =>
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key.Secret));
        securityKey.KeyId = key.KeyId;
        return (SecurityKey)securityKey;
    })
    .ToArray();

builder.Services.AddDbContext<HotelDbContext>(options =>
{
    if (databaseDecision.UsePostgres)
    {
        options.UseNpgsql(databaseDecision.ConnectionString);
        return;
    }

    options.UseInMemoryDatabase(localExecution.InMemoryDatabaseName);
});

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<HotelDbContext>("database", tags: ["readiness"])
    .AddCheck<OtlpHealthCheck>("otlp", tags: ["diagnostics"])
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["liveness"]);

builder.Services.AddHttpClient("external-hotel-partners")
    .AddStandardResilienceHandler();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        var problem = new
        {
            title = "Muitas requisições",
            detail = "Limite de requisições excedido.",
            status = StatusCodes.Status429TooManyRequests
        };
        await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(problem), cancellationToken);
    };

    options.AddPolicy("auth", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(HotelTelemetry.ServiceName, serviceVersion: HotelTelemetry.ServiceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(HotelTelemetry.ActivitySource.Name)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = false;
                options.SetDbStatementForStoredProcedure = false;
            })
            .AddOtlpExporter(options =>
            {
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                }
            });

        if (builder.Environment.IsDevelopment())
        {
            tracing.AddConsoleExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(HotelTelemetry.Meter.Name)
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(options =>
            {
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                }
            });

        if (builder.Environment.IsDevelopment())
        {
            metrics.AddConsoleExporter();
        }
    });

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKeyResolver = (_, _, kid, _) =>
            {
                if (string.IsNullOrWhiteSpace(kid))
                {
                    return jwtValidationKeys;
                }

                var match = jwtValidationKeys.Where(key => key.KeyId == kid).ToArray();
                return match.Length > 0 ? match : jwtValidationKeys;
            },
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReservationsWrite", policy =>
        policy.RequireClaim("scope", "reservations.write", "hotel.full"));
    options.AddPolicy("OperationsRead", policy =>
        policy.RequireClaim("scope", "operations.read", "hotel.full"));
    options.AddPolicy("McpAccess", policy =>
        policy.RequireClaim("scope", "mcp.access", "hotel.full"));
    options.AddPolicy("McpToolsRead", policy =>
        policy.RequireClaim("scope", "mcp.tools.read", "hotel.full"));
    options.AddPolicy("McpAvailabilityQuery", policy =>
        policy.RequireClaim("scope", "mcp.availability.query", "hotel.full"));
    options.AddPolicy("McpHousekeepingRead", policy =>
        policy.RequireClaim("scope", "mcp.housekeeping.read", "hotel.full"));
    options.AddPolicy("AuditRead", policy =>
        policy.RequireClaim("scope", "audit.read", "hotel.full"));
});

builder.Services.AddSingleton<JwtKeyProvider>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<HousekeepingRoutingService>();
builder.Services.AddScoped<SecurityAuditService>();
builder.Services.AddScoped<AuthLockoutService>();
builder.Services.AddScoped<IdempotencyService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddSingleton<OtlpHealthCheck>();
builder.Services.AddHostedService<ComplianceMaintenanceWorker>();
builder.Services.AddHostedService<SecurityAuditExportWorker>();

var app = builder.Build();
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

startupLogger.LogInformation(
    "Startup profile={Profile}, provider={Provider}, fallbackEnabled={FallbackEnabled}, reason={Reason}",
    localExecution.Profile,
    databaseDecision.Provider,
    localExecution.EnableDatabaseFallback,
    databaseDecision.Reason);

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<HotelDbContext>();

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        await HotelSeedData.SeedAsync(dbContext);
    }
    catch (Exception exception)
    {
        startupLogger.LogCritical(exception,
            "Falha de infraestrutura durante bootstrap de banco/seeding. Provider={Provider}; Profile={Profile}",
            databaseDecision.Provider,
            localExecution.Profile);
        throw;
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/info", () => Results.Ok(new
{
    system = "Hotel ERP API",
    version = "v1",
    mode = "api-first"
}));

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("liveness")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("readiness")
});

app.MapHealthChecks("/health/diagnostics", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("diagnostics")
});

app.MapAuthEndpoints();
app.MapReservationEndpoints();
app.MapHousekeepingEndpoints();
app.MapMcpEndpoints();
app.MapAdminAuditEndpoints();

try
{
    await app.RunAsync();
}
catch (IOException exception) when (
    exception.InnerException is AddressInUseException ||
    exception.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
{
    startupLogger.LogCritical(exception,
        "Falha de bind HTTP: endereço já em uso. Ajuste a porta (ASPNETCORE_URLS) ou encerre a instância atual.");
    throw;
}
catch (Exception exception)
{
    startupLogger.LogCritical(exception, "Falha funcional inesperada durante execução da API.");
    throw;
}

return;

static DatabaseBootstrapDecision ResolveDatabaseBootstrapDecision(
    string? connectionString,
    LocalExecutionOptions localExecution)
{
    var preferred = localExecution.PreferredDatabaseProvider?.Trim().ToLowerInvariant();

    if (preferred == "inmemory")
    {
        return new DatabaseBootstrapDecision(false, null, "inmemory", "Perfil forçado para InMemory.");
    }

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        if (localExecution.EnableDatabaseFallback)
        {
            return new DatabaseBootstrapDecision(false, null, "inmemory",
                "ConnectionStrings:HotelDb ausente; fallback explícito para InMemory aplicado.");
        }

        throw new InvalidOperationException(
            "Configuração inválida: ConnectionStrings:HotelDb está vazia para provider postgres e fallback está desabilitado.");
    }

    if (!localExecution.EnableDatabaseFallback)
    {
        return new DatabaseBootstrapDecision(true, connectionString, "postgres",
            "PostgreSQL configurado (fallback desabilitado por perfil)." );
    }

    try
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = 2,
            CommandTimeout = 5
        };

        using var probeConnection = new NpgsqlConnection(builder.ConnectionString);
        probeConnection.Open();

        return new DatabaseBootstrapDecision(true, builder.ConnectionString, "postgres",
            "PostgreSQL alcançável no probe inicial.");
    }
    catch (Exception exception)
    {
        return new DatabaseBootstrapDecision(false, null, "inmemory",
            $"Falha no probe PostgreSQL ({exception.GetType().Name}); fallback explícito para InMemory aplicado.");
    }
}

sealed record DatabaseBootstrapDecision(
    bool UsePostgres,
    string? ConnectionString,
    string Provider,
    string Reason);

public partial class Program;