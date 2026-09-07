using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Raven.Data;
using Raven.Models;

namespace Raven.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _context;

    public SettingsController(AppDbContext context)
    {
        _context = context;
    }


    // ============================================================
    // GET /api/settings
    // ============================================================
    //
    // Accessible Ã  tout utilisateur authentifié.
    // Crée la ligne de configuration par défaut si elle n'existe
    // pas encore (premier démarrage ou base réinitialisée).
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _context.ApplicationSettings
            .FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = CreateDefaultSettings();

            _context.ApplicationSettings.Add(settings);
            await _context.SaveChangesAsync();
        }
        else if (settings.CompanyName == "Raven")
        {
            settings.CompanyName = "Raven";
            settings.CompanySlogan = "Oracle NetSuite Cloud ERP Â· Maintenance & Planification";
            await _context.SaveChangesAsync();
        }

        return Ok(ToDto(settings));
    }


    // ============================================================
    // PUT /api/settings
    // ============================================================
    //
    // Seuls les Responsables peuvent modifier la configuration.
    // ============================================================

    [HttpPut]
    [Authorize(Roles = "Responsable")]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] ApplicationSettingsDto dto)
    {
        if (dto == null)
        {
            return BadRequest(new
            {
                message = "Configuration invalide."
            });
        }

        if (string.IsNullOrWhiteSpace(dto.CompanyName))
        {
            return BadRequest(new
            {
                message = "Le nom de l'entreprise est obligatoire."
            });
        }

        if (dto.DefaultHours <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Le nombre d'heures hebdomadaires doit être supérieur Ã  0."
            });
        }

        if (dto.DefaultSla <= 0)
        {
            return BadRequest(new
            {
                message = "Le SLA doit être supérieur Ã  0."
            });
        }

        if (dto.DefaultVisiteDuration <= 0)
        {
            return BadRequest(new
            {
                message =
                    "La durée de visite doit être supérieure Ã  0."
            });
        }

        var settings = await _context.ApplicationSettings
            .FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = new ApplicationSetting();
            _context.ApplicationSettings.Add(settings);
        }

        settings.CompanyName = dto.CompanyName.Trim();
        settings.CompanySlogan = dto.CompanySlogan?.Trim() ?? "";
        settings.CompanyEmail = dto.CompanyEmail?.Trim() ?? "";
        settings.CompanyPhone = dto.CompanyPhone?.Trim() ?? "";
        settings.CompanyAddress = dto.CompanyAddress?.Trim() ?? "";

        settings.PrimaryColor =
            string.IsNullOrWhiteSpace(dto.PrimaryColor)
                ? "#0d9488"
                : dto.PrimaryColor.Trim();

        settings.ThemeMode =
            dto.ThemeMode is "light" or "dark"
                ? dto.ThemeMode
                : "light";

        settings.DefaultHours = dto.DefaultHours;
        settings.DefaultSla = dto.DefaultSla;

        settings.DefaultCurrency =
            string.IsNullOrWhiteSpace(dto.DefaultCurrency)
                ? "MAD"
                : dto.DefaultCurrency.Trim();

        settings.DefaultVisiteDuration = dto.DefaultVisiteDuration;

        settings.AgencesJson = JsonSerializer.Serialize(
            (dto.Agences ?? new List<string>())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());

        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ToDto(settings));
    }


    // ============================================================
    // Helpers
    // ============================================================

    private static ApplicationSetting CreateDefaultSettings()
    {
        return new ApplicationSetting
        {
            AgencesJson = JsonSerializer.Serialize(
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
    }

    private static ApplicationSettingsDto ToDto(
        ApplicationSetting settings)
    {
        List<string> agences;

        try
        {
            agences = JsonSerializer.Deserialize<List<string>>(
                settings.AgencesJson) ?? new();
        }
        catch
        {
            agences = new();
        }

        return new ApplicationSettingsDto
        {
            Id = settings.Id,
            CompanyName = settings.CompanyName,
            CompanySlogan = settings.CompanySlogan,
            CompanyEmail = settings.CompanyEmail,
            CompanyPhone = settings.CompanyPhone,
            CompanyAddress = settings.CompanyAddress,
            PrimaryColor = settings.PrimaryColor,
            ThemeMode = settings.ThemeMode,
            Agences = agences,
            DefaultHours = settings.DefaultHours,
            DefaultSla = settings.DefaultSla,
            DefaultCurrency = settings.DefaultCurrency,
            DefaultVisiteDuration = settings.DefaultVisiteDuration,
            UpdatedAt = settings.UpdatedAt
        };
    }
}


// ============================================================
// DTO
// ============================================================

public class ApplicationSettingsDto
{
    public int Id { get; set; }

    public string CompanyName { get; set; } = "";
    public string CompanySlogan { get; set; } = "";

    public string CompanyEmail { get; set; } = "";
    public string CompanyPhone { get; set; } = "";
    public string CompanyAddress { get; set; } = "";

    public string PrimaryColor { get; set; } = "#0d9488";
    public string ThemeMode { get; set; } = "light";

    public List<string> Agences { get; set; } = new();

    public int DefaultHours { get; set; } = 40;
    public int DefaultSla { get; set; } = 24;
    public string DefaultCurrency { get; set; } = "MAD";
    public int DefaultVisiteDuration { get; set; } = 120;

    public DateTime UpdatedAt { get; set; }
}

