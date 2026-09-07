using Microsoft.EntityFrameworkCore;
using Raven.Models;

namespace Raven.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // ========================================================
    // Tables
    // ========================================================

    public DbSet<Marche> Marches => Set<Marche>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Equipement> Equipements => Set<Equipement>();
    public DbSet<Visite> Visites => Set<Visite>();
    public DbSet<Technicien> Techniciens => Set<Technicien>();
    public DbSet<Specialite> Specialites => Set<Specialite>();
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<ApplicationSetting> ApplicationSettings
        => Set<ApplicationSetting>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<StagingRecord> StagingRecords => Set<StagingRecord>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        // ====================================================
        // UNIQUE CONSTRAINTS / INDEXES
        // ====================================================

        modelBuilder.Entity<Utilisateur>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Client>()
            .HasIndex(c => c.CodeClient)
            .IsUnique();

        modelBuilder.Entity<Site>()
            .HasIndex(s => s.CodeSite)
            .IsUnique();

        modelBuilder.Entity<Marche>()
            .HasIndex(m => m.CodeMarche)
            .IsUnique();

        modelBuilder.Entity<Equipement>()
            .HasIndex(e => e.SerialNumber)
            .IsUnique();

        modelBuilder.Entity<Visite>()
            .HasIndex(v => v.Reference)
            .IsUnique();

        modelBuilder.Entity<Specialite>()
            .HasIndex(s => s.Nom)
            .IsUnique();

        modelBuilder.Entity<Technicien>()
            .HasIndex(t => t.Matricule)
            .IsUnique();

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();

        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(t => t.Utilisateur)
            .WithMany()
            .HasForeignKey(t => t.UtilisateurId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OtpCode>()
            .HasIndex(o => o.TempToken)
            .IsUnique();

        modelBuilder.Entity<OtpCode>()
            .HasOne(o => o.Utilisateur)
            .WithMany()
            .HasForeignKey(o => o.UtilisateurId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StagingRecord>()
            .HasIndex(sr => new { sr.BatchId, sr.Status });


        // ====================================================
        // TECHNICIEN <-> SPECIALITE
        // Many-to-Many relationship
        // ====================================================

        modelBuilder.Entity<Technicien>()
            .HasMany(t => t.Specialites)
            .WithMany(s => s.Techniciens)
            .UsingEntity(j =>
                j.ToTable("TechnicienSpecialites"));


        // ====================================================
        // VISITE -> TECHNICIEN
        // A visit may not have a technician assigned yet.
        // ====================================================

        modelBuilder.Entity<Visite>()
            .HasOne(v => v.Technicien)
            .WithMany(t => t.Visites)
            .HasForeignKey(v => v.TechnicienId)
            .OnDelete(DeleteBehavior.Restrict);


        // ====================================================
        // VISITE -> EQUIPEMENT
        //
        // We keep the visit history if an equipment record
        // is removed or archived.
        // ====================================================

        modelBuilder.Entity<Visite>()
            .HasOne(v => v.Equipement)
            .WithMany(e => e.Visites)
            .HasForeignKey(v => v.EquipementId)
            .OnDelete(DeleteBehavior.Restrict);


        // ====================================================
        // VISITE -> MARCHE
        // A visit may optionally belong to a contract/market.
        // ====================================================

        modelBuilder.Entity<Visite>()
            .HasOne(v => v.Marche)
            .WithMany(m => m.Visites)
            .HasForeignKey(v => v.MarcheId)
            .OnDelete(DeleteBehavior.Restrict);


        // ====================================================
        // NO SEED DATA HERE
        //
        // Initial application data is initialized separately
        // during application startup.
        // ====================================================
    }
}
