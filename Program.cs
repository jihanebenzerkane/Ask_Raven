using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using TechnoVIS.Data;
using TechnoVIS.Models;
using TechnoVIS.Services;

var builder = WebApplication.CreateBuilder(args);



QuestPDF.Settings.License = LicenseType.Community;

// MVC / API


builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IEmailService, EmailService>();

// Application Services

builder.Services.AddScoped<ScoringService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<CsvExportService>();


// Authentication - Cookie Authentication
// ASP.NET Core uses this cookie for subsequent requests.

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "TechnoVIS.Auth";

        // JavaScript cannot read the authentication cookie.
        options.Cookie.HttpOnly = true;

        // Suitable when frontend and backend are served
        // from the same application.
        options.Cookie.SameSite = SameSiteMode.Lax;

        // HTTPS in production.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // API should return status codes instead of
        // redirecting to an HTML login page.
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


// Authorization

builder.Services.AddAuthorization();


// Rate Limiting
//
// Protects sensitive endpoints such as login from brute-force
// attempts.

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("LoginPolicy", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder =
            System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
});


// CORS
//
// Only needed during local development if frontend/backend
// are accessed from different origins.
//
// In production, the frontend is served by the same ASP.NET Core
// application, so CORS is normally not required.

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCorsPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5196",
                "https://localhost:7196",
                "http://127.0.0.1:5196"
            )
            .AllowCredentials()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});


// Database - SQL Server + Entity Framework Core

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "La chaîne de connexion SQL Server est requise. " +
        "Configurez ConnectionStrings:DefaultConnection " +
        "ou DB_CONNECTION_STRING.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        });
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});


var app = builder.Build();


// Database initialization

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    // Apply EF Core migrations.
    dbContext.Database.Migrate();


    // --------------------------------------------------------
    // Default Specialites
    // --------------------------------------------------------

    if (!dbContext.Specialites.Any())
    {
        var defaultSpecialites = new List<Specialite>
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

        dbContext.Specialites.AddRange(defaultSpecialites);
        dbContext.SaveChanges();
    }


    // --------------------------------------------------------
    // Default ApplicationSettings
    // --------------------------------------------------------

    if (!dbContext.ApplicationSettings.Any())
    {
        var defaultSettings = new ApplicationSetting
        {
            AgencesJson = System.Text.Json.JsonSerializer.Serialize(
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

        dbContext.ApplicationSettings.Add(defaultSettings);
        dbContext.SaveChanges();
    }

    if (!dbContext.Utilisateurs.Any())
    {
        var adminEmail =
            builder.Configuration["Admin:Email"]
            ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL");

        var adminPassword =
            builder.Configuration["Admin:DefaultPassword"]
            ?? Environment.GetEnvironmentVariable("ADMIN_DEFAULT_PASSWORD");

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            throw new InvalidOperationException(
                "Admin:Email ou ADMIN_EMAIL est requis pour créer le compte administrateur initial.");
        }

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "Admin:DefaultPassword ou ADMIN_DEFAULT_PASSWORD est requis pour créer le compte administrateur initial.");
        }

        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Utilisateur>();

        var adminUser = new Utilisateur
        {
            Email = adminEmail.Trim(),
            Role = "Responsable",
            TechnicienId = null,
            DateCreation = DateTime.UtcNow
        };

        adminUser.PasswordHash =
            hasher.HashPassword(adminUser, adminPassword);

        dbContext.Utilisateurs.Add(adminUser);
        dbContext.SaveChanges();
    }

    // Seed realistic Raven ERP demo data (Clients, Sites, Marches, Techniciens, Equipements, Visites) if empty
    TechnoVIS.Services.DbSeeder.SeedAsync(dbContext).GetAwaiter().GetResult();
}


// HTTP Pipeline

if (app.Environment.IsDevelopment())
{
    app.UseCors("DevCorsPolicy");
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


// Health Check

app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "ok",
        service = "Raven ERP Maintenance API",
        time = DateTime.UtcNow
    }));


app.Run();