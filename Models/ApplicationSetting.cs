namespace Raven.Models;

public class ApplicationSetting
{
    public int Id { get; set; }

    // ========================================================
    // Identité entreprise
    // ========================================================

    public string CompanyName { get; set; } = "Raven";

    public string CompanySlogan { get; set; } =
        "Oracle NetSuite Cloud ERP Â· Maintenance & Planification";

    public string CompanyEmail { get; set; } = "";
    public string CompanyPhone { get; set; } = "";
    public string CompanyAddress { get; set; } = "";


    // ========================================================
    // Apparence
    // ========================================================

    public string PrimaryColor { get; set; } = "#0d9488";
    public string ThemeMode { get; set; } = "light";


    // ========================================================
    // Valeurs métier par défaut
    // ========================================================

    public int DefaultHours { get; set; } = 40;
    public int DefaultSla { get; set; } = 24;
    public string DefaultCurrency { get; set; } = "MAD";
    public int DefaultVisiteDuration { get; set; } = 120;


    // ========================================================
    // Agences / Bases
    // Stockées en JSON car liste simple de strings.
    // ========================================================

    public string AgencesJson { get; set; } = "[]";


    // ========================================================
    // Horodatage
    // ========================================================

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

