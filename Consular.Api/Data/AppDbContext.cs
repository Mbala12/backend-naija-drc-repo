using Consular.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Consular.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RegionLookup> RegionLookups => Set<RegionLookup>();
    public DbSet<TypeService> TypeServices => Set<TypeService>();
    public DbSet<Statut> Statuts => Set<Statut>();
    public DbSet<Demande> Demandes => Set<Demande>();
    public DbSet<DemandeDocument> DemandeDocuments => Set<DemandeDocument>();
    public DbSet<DemandeHistorique> DemandeHistoriques => Set<DemandeHistorique>();
    public DbSet<DemandeVisa> DemandeVisas => Set<DemandeVisa>();
    public DbSet<DemandeEtatCivil> DemandeEtatCivils => Set<DemandeEtatCivil>();
    public DbSet<DemandePasseport> DemandePasseports => Set<DemandePasseport>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<AppointmentSlotTemplate> AppointmentSlotTemplates => Set<AppointmentSlotTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Applicant>(e =>
        {
            e.HasIndex(c => c.Email).IsUnique();
            e.Property(c => c.Nom).HasMaxLength(200).IsRequired();
            e.Property(c => c.Email).HasMaxLength(200).IsRequired();
            e.Property(c => c.Telephone).HasMaxLength(30).IsRequired();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Nom).HasMaxLength(200).IsRequired();
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.Property(u => u.MotDePasseHash).IsRequired();

            e.HasOne(u => u.Role)
                .WithMany()
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Code).HasMaxLength(50).IsRequired();
            e.Property(p => p.Label).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasIndex(r => r.Name).IsUnique();
            e.Property(r => r.Name).HasMaxLength(100).IsRequired();

            // Implicit many-to-many (EF Core 8) — no join entity needed since RolePermission
            // carries no data of its own beyond the two FKs.
            e.HasMany(r => r.Permissions).WithMany();
        });

        modelBuilder.Entity<RegionLookup>(e =>
        {
            e.HasIndex(r => r.Code).IsUnique();
            e.Property(r => r.Code).HasMaxLength(50).IsRequired();
            e.Property(r => r.LibelleFr).HasMaxLength(100).IsRequired();
            e.Property(r => r.LibelleEn).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<TypeService>(e =>
        {
            e.HasIndex(t => t.Code).IsUnique();
            e.Property(t => t.Code).HasMaxLength(50).IsRequired();
            e.Property(t => t.Libelle).HasMaxLength(200).IsRequired();
            e.Property(t => t.MontantFrais).HasColumnType("numeric(12,2)");
        });

        modelBuilder.Entity<Statut>(e =>
        {
            e.HasIndex(s => s.Code).IsUnique();
            e.Property(s => s.Code).HasMaxLength(50).IsRequired();
            e.Property(s => s.Libelle).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Demande>(e =>
        {
            e.HasIndex(d => d.NumeroReference).IsUnique();
            e.Property(d => d.NumeroReference).IsRequired();
            e.Property(d => d.CanalDepot).HasMaxLength(50).IsRequired();
            e.Property(d => d.Attributs).HasColumnType("jsonb");

            e.HasOne(d => d.Applicant)
                .WithMany(a => a.Demandes)
                .HasForeignKey(d => d.ApplicantId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.SoumisParApplicant)
                .WithMany()
                .HasForeignKey(d => d.SoumisParApplicantId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.SoumisParUser)
                .WithMany()
                .HasForeignKey(d => d.SoumisParUserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.TypeService)
                .WithMany(t => t.Demandes)
                .HasForeignKey(d => d.TypeServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.Statut)
                .WithMany()
                .HasForeignKey(d => d.StatutId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(d => d.Documents)
                .WithOne()
                .HasForeignKey(doc => doc.DemandeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(d => d.Historique)
                .WithOne()
                .HasForeignKey(h => h.DemandeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DemandeHistorique>(e =>
        {
            e.HasOne(h => h.StatutOrigine)
                .WithMany()
                .HasForeignKey(h => h.StatutOrigineId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(h => h.StatutDestination)
                .WithMany()
                .HasForeignKey(h => h.StatutDestinationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Staff will query "everything that happened to dossier X" and, eventually,
            // "everything liaison did today" for reporting.
            e.HasIndex(h => new { h.DemandeId, h.DateChangement });
        });

        modelBuilder.Entity<DemandeVisa>(e =>
        {
            e.HasKey(v => v.DemandeId);
            e.HasOne(v => v.Demande)
                .WithOne(d => d.DemandeVisa)
                .HasForeignKey<DemandeVisa>(v => v.DemandeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DemandeEtatCivil>(e =>
        {
            e.HasKey(v => v.DemandeId);
            e.HasOne(v => v.Demande)
                .WithOne(d => d.DemandeEtatCivil)
                .HasForeignKey<DemandeEtatCivil>(v => v.DemandeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DemandePasseport>(e =>
        {
            e.HasKey(v => v.DemandeId);
            e.HasOne(v => v.Demande)
                .WithOne(d => d.DemandePasseport)
                .HasForeignKey<DemandePasseport>(v => v.DemandeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminAuditLog>(e =>
        {
            e.Property(a => a.ActorName).HasMaxLength(200).IsRequired();
            e.Property(a => a.Action).HasMaxLength(20).IsRequired();
            e.Property(a => a.EntityType).HasMaxLength(50).IsRequired();
            e.Property(a => a.EntityId).HasMaxLength(50).IsRequired();

            // Staff will query "everything that happened to entity X" and "everything actor Y
            // did", same access patterns as DemandeHistorique above.
            e.HasIndex(a => new { a.EntityType, a.EntityId });
            e.HasIndex(a => a.CreatedAt);
        });

        modelBuilder.Entity<AppointmentSlotTemplate>(e =>
        {
            // The natural key — prevents two admin rows describing the same weekly slot for the
            // same region+category.
            e.HasIndex(t => new { t.Region, t.Categorie, t.DayOfWeek, t.StartTime }).IsUnique();
            e.Property(t => t.CapaciteMax).IsRequired();
        });
    }
}
