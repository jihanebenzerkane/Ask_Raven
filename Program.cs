using System.Security.Claims;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

using QuestPDF.Infrastructure;

using Raven.Data;
using Raven.Models;
using Raven.Services;

var builder = WebApplication.CreateBuilder(args);

//
// ============================================================
// RAVEN
// Enterprise Maintenance Management Platform
// ============================================================
//


// ------------------------------------------------------------
// APPLICATION
// ------------------------------------------------------------

const string ApplicationName = "Raven";

builder.Services.AddSingleton(new ApplicationInfo
{
    Name = ApplicationName,
    FullName = "Raven Maintenance Management Platform"
});


// ------------------------------------------------------------
// QUESTPDF
// ------------------------------------------------------------

QuestPDF.Settings.License = LicenseType.Community;


// ------------------------------------------------------------
// CONTROLLERS
// ------------------------------------------------------------

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;

        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });


// ------------------------------------------------------------
// OPENAPI
// ------------------------------------------------------------

builder.Services.AddOpenApi();


// ------------------------------------------------------------
// MEMORY CACHE
// ------------------------------------------------------------

builder.Services.AddMemoryCache();


// ------------------------------------------------------------
// APPLICATION SERVICES
// ------------------------------------------------------------

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddScoped<ScoringService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<CsvExportService>();
builder.Services.AddScoped<StagingService>();


// ------------------------------------------------------------
// AUTHENTICATION & ENTERPRISE SSO
// ------------------------------------------------------------
//
// Dual-mode authentication:
// 1. Local secure cookie auth (Raven.Auth)
// 2. Microsoft Entra ID (OpenID Connect) for corporate ECS federation
// ------------------------------------------------------------

var cookieName =
    builder.Configuration["Authentication:CookieName"]
    ?? "Raven.Auth";

var entraTenantId = builder.Configuration["EntraId:TenantId"];
var entraClientId = builder.Configuration["EntraId:ClientId"];

var authBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = cookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(
            builder.Configuration.GetValue<int?>("Authentication:SessionHours") ?? 8);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

if (!string.IsNullOrWhiteSpace(entraClientId))
{
    authBuilder.AddOpenIdConnect("EntraId", options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{(string.IsNullOrWhiteSpace(entraTenantId) ? "common" : entraTenantId)}/v2.0";
        options.ClientId = entraClientId;
        options.ClientSecret = builder.Configuration["EntraId:ClientSecret"];
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.CallbackPath = "/signin-oidc";
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
    });
}


// ------------------------------------------------------------
// AUTHORIZATION
// ------------------------------------------------------------

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ResponsableOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Responsable");
    });

    options.AddPolicy("TechnicienOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Technicien");
    });

    options.AddPolicy("ResponsableOrTechnicien", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Responsable", "Technicien");
    });

    options.AddPolicy("EcsEmployee", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim(c => c.Type == "TenantDomain" || c.Type == "groups" || c.Type == ClaimTypes.Role));
    });
});


// ------------------------------------------------------------
// RATE LIMITING
// ------------------------------------------------------------
//
// Protect authentication endpoints against brute-force attacks.
// ------------------------------------------------------------

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("LoginPolicy", httpContext =>
    {
        var ip =
            httpContext.Connection.RemoteIpAddress?
                .ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            ip,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,

                Window = TimeSpan.FromMinutes(1),

                QueueProcessingOrder =
                    QueueProcessingOrder.OldestFirst,

                QueueLimit = 0,

                AutoReplenishment = true
            });
    });
});


// ------------------------------------------------------------
// CORS
// ------------------------------------------------------------
//
// Development only.
// In production Raven should normally serve frontend and API
// from the same origin.
// ------------------------------------------------------------

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCors", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5196",
                "https://localhost:7196",
                "http://127.0.0.1:5196"
            )
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


// ------------------------------------------------------------
// DATABASE CONNECTION
// ------------------------------------------------------------
//
// PRIORITY:
//
// 1. DB_CONNECTION_STRING environment variable
// 2. ConnectionStrings:DefaultConnection
//
// This allows us to keep credentials outside Git.
// ------------------------------------------------------------

var connectionString =
    Environment.GetEnvironmentVariable(
        "DB_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString =
        builder.Configuration.GetConnectionString(
            "DefaultConnection");
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        """
        Raven ne peut pas dÃƒÂ©marrer car aucune chaÃƒÂ®ne
        de connexion SQL Server n'est configurÃƒÂ©e.

        Configurez la variable d'environnement:

        DB_CONNECTION_STRING

        Exemple:

        Server=localhost,1433;Database=Raven;
        User Id=sa;Password=...;
        TrustServerCertificate=True;
        """
    );
}


// ------------------------------------------------------------
// ENTITY FRAMEWORK / SQL SERVER
// ------------------------------------------------------------

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null
            );

            sqlOptions.CommandTimeout(30);
        });

    options.EnableDetailedErrors(
        builder.Environment.IsDevelopment());

    options.EnableSensitiveDataLogging(false);
});

// --- Smart Import AI Services ---
builder.Services.AddHttpClient();
// ------------------------------------------------------------
// BUILD
// ------------------------------------------------------------

var app = builder.Build();


// ------------------------------------------------------------
// DATABASE INITIALIZATION
// ------------------------------------------------------------

using (var scope = app.Services.CreateScope())
{
    var logger =
        scope.ServiceProvider
            .GetRequiredService<
                ILogger<Program>>();

    var db =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    try
    {
        logger.LogInformation(
            "Raven: testing SQL Server connection...");

        var canConnect =
            await db.Database.CanConnectAsync();

        if (!canConnect)
        {
            throw new InvalidOperationException(
                "Raven ne peut pas se connecter ÃƒÂ  SQL Server.");
        }

        logger.LogInformation(
            "Raven: SQL Server connection successful.");

        //
        // Apply EF Core migrations.
        //
        await db.Database.MigrateAsync();

        logger.LogInformation(
            "Raven: database migrations completed.");

        //
        // ----------------------------------------------------
        // DEFAULT SPECIALITIES
        // ----------------------------------------------------
        //

        if (!await db.Specialites.AnyAsync())
        {
            var specialites = new List<Specialite>
            {
                new()
                {
                    Nom = "HVAC",
                    Description =
                        "Climatisation, Chauffage, Ventilation et Groupes Froid"
                },

                new()
                {
                    Nom = "TGBT",
                    Description =
                        "Tableaux Généraux Basse Tension et Armoires Électriques"
                },

                new()
                {
                    Nom = "Haute Tension",
                    Description =
                        "Postes de Transformation et Cellules MT/HT"
                },

                new()
                {
                    Nom = "Groupe Électrogène",
                    Description =
                        "Groupes Électrogènes et Onduleurs de secours"
                },

                new()
                {
                    Nom = "Compresseur",
                    Description =
                        "Centrales d'air comprimé et pompes industrielles"
                },

                new()
                {
                    Nom = "Automatisme",
                    Description =
                        "Automates programmables, Télégestion et Régulation"
                },

                new()
                {
                    Nom = "Électricité industrielle",
                    Description =
                        "Installations et câblages électriques industriels"
                },

                new()
                {
                    Nom = "Informatique & Réseau",
                    Description =
                        "Serveurs, Postes, Baies de brassage et Switchs"
                }
            };

            await db.Specialites.AddRangeAsync(
                specialites);

            await db.SaveChangesAsync();

            logger.LogInformation(
                "Raven: default specialities created.");
        }


        //
        // ----------------------------------------------------
        // APPLICATION SETTINGS
        // ----------------------------------------------------
        //

        if (!await db.ApplicationSettings.AnyAsync())
        {
            var settings =
                new ApplicationSetting
                {
                    AgencesJson =
                        System.Text.Json.JsonSerializer.Serialize(
                            new[]
                            {
                                "Casablanca",
                                "Rabat",
                                "Tanger",
                                "Safi",
                                "Marrakech",
                                "Agadir",
                                "Fès"
                            })
                };

            await db.ApplicationSettings.AddAsync(
                settings);

            await db.SaveChangesAsync();

            logger.LogInformation(
                "Raven: application settings created.");
        }


        //
        // ----------------------------------------------------
        // ADMINISTRATOR
        // ----------------------------------------------------
        //
        // IMPORTANT:
        //
        // We DO NOT automatically create an admin with a
        // hardcoded password.
        //
        // If you already have users, nothing happens.
        //
        // If the database is empty, configure:
        //
        // ADMIN_EMAIL
        // ADMIN_DEFAULT_PASSWORD
        //
        // through environment variables.
        //

        if (!await db.Utilisateurs.AnyAsync())
        {
            var adminEmail =
                Environment.GetEnvironmentVariable(
                    "ADMIN_EMAIL");

            var adminPassword =
                Environment.GetEnvironmentVariable(
                    "ADMIN_DEFAULT_PASSWORD");

            if (!string.IsNullOrWhiteSpace(adminEmail) &&
                !string.IsNullOrWhiteSpace(adminPassword))
            {
                var hasher =
                    new Microsoft.AspNetCore.Identity
                        .PasswordHasher<Utilisateur>();

                var admin =
                    new Utilisateur
                    {
                        Email = adminEmail
                            .Trim()
                            .ToLowerInvariant(),

                        Role = "Responsable",

                        TechnicienId = null,

                        DateCreation =
                            DateTime.UtcNow
                    };

                admin.PasswordHash =
                    hasher.HashPassword(
                        admin,
                        adminPassword);

                await db.Utilisateurs.AddAsync(
                    admin);

                await db.SaveChangesAsync();

                logger.LogInformation(
                    "Raven: initial Responsable account created.");
            }
            else
            {
                logger.LogWarning(
                    """
                    Raven: Utilisateurs table is empty.

                    No administrator was created because
                    ADMIN_EMAIL and ADMIN_DEFAULT_PASSWORD
                    are not configured.

                    This is intentional for security.
                    """);
            }
        }


        //
        // ----------------------------------------------------
        // DEMO DATA
        // ----------------------------------------------------
        //

        await Raven.Services.DbSeeder
            .SeedAsync(db);

        logger.LogInformation(
            "Raven: database initialization completed.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(
            ex,
            """
            Raven failed during database initialization.

            Check:

            1. SQL Server is running.
            2. Server name is correct.
            3. Database exists or the account can create/use it.
            4. SQL username/password are correct.
            5. TCP port 1433 is accessible.
            6. DB_CONNECTION_STRING is correct.
            """);

        throw;
    }
}


// ------------------------------------------------------------
// HTTP PIPELINE
// ------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevelopmentCors");

    app.MapOpenApi();
}


app.UseDefaultFiles();

app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();


// ------------------------------------------------------------
// HEALTH CHECK
// ------------------------------------------------------------

app.MapGet(
    "/health",
    async (
        AppDbContext db,
        CancellationToken cancellationToken) =>
    {
        var databaseAvailable =
            await db.Database.CanConnectAsync(
                cancellationToken);

        if (!databaseAvailable)
        {
            return Results.Json(
                new
                {
                    status = "degraded",
                    service =
                        "Raven Maintenance Management Platform",

                    database = "unavailable",

                    time = DateTime.UtcNow
                },
                statusCode:
                    StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(
            new
            {
                status = "ok",

                service =
                    "Raven Maintenance Management Platform",

                database = "connected",

                time = DateTime.UtcNow
            });
    });


// ------------------------------------------------------------
// RUN
// ------------------------------------------------------------

app.Run();


// ------------------------------------------------------------
// APPLICATION INFORMATION
// ------------------------------------------------------------

public sealed class ApplicationInfo
{
    public string Name { get; init; } = "Raven";

    public string FullName { get; init; } =
        "Raven Maintenance Management Platform";
}


