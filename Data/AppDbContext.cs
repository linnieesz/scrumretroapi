using Microsoft.EntityFrameworkCore;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<SuggestionTracking> SuggestionTrackings => Set<SuggestionTracking>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Company>(e =>
        {
            e.HasIndex(c => c.Cnpj).IsUnique();
            e.Property(c => c.Name).HasMaxLength(150).IsRequired();
            e.Property(c => c.LegalName).HasMaxLength(200);
            e.Property(c => c.Cnpj).HasMaxLength(14).IsRequired();
        });

        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.CompanyId);
            e.HasOne(u => u.Company).WithMany(c => c.Members).HasForeignKey(u => u.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.FullName).HasMaxLength(150).IsRequired();
            e.Property(u => u.Email).HasMaxLength(254).IsRequired();
            e.Property(u => u.Department).HasMaxLength(100);
            e.Property(u => u.Phone).HasMaxLength(30);
            e.Property(u => u.Bio).HasMaxLength(500);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Project>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(150).IsRequired();
            e.Property(p => p.Description).HasMaxLength(2000);
            e.HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ProjectMember>(e =>
        {
            e.HasKey(m => new { m.ProjectId, m.UserId });
            e.HasOne(m => m.Project).WithMany(p => p.Members).HasForeignKey(m => m.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Sprint>(e =>
        {
            e.HasIndex(s => new { s.ProjectId, s.Number }).IsUnique();
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(s => s.Project).WithMany(p => p.Sprints).HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Card>(e =>
        {
            e.Property(c => c.Content).HasMaxLength(1000).IsRequired();
            e.Property(c => c.Ceremony).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.Type).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(c => new { c.SprintId, c.Ceremony });
            e.HasIndex(c => c.AuthorId);
            e.HasOne(c => c.Sprint).WithMany(s => s.Cards).HasForeignKey(c => c.SprintId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<SuggestionTracking>(e =>
        {
            e.HasIndex(t => t.CardId).IsUnique();
            e.HasIndex(t => t.ProjectId);
            e.HasOne(t => t.Card).WithOne(c => c.Tracking).HasForeignKey<SuggestionTracking>(t => t.CardId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(t => t.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
