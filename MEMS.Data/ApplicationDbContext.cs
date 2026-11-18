using System;
using MEMS.Models;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public int? CurrentTenantId { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Municipality>().ToTable("Municipality");
        builder.Entity<Position>().ToTable("Position");
        builder.Entity<Candidate>().ToTable("Candidate");

        builder.Entity<Municipality>().HasIndex(m => m.Slug).IsUnique();

        builder.Entity<Position>()
            .HasOne(p => p.Municipality)
            .WithMany(m => m.Positions)
            .HasForeignKey(p => p.MunicipalityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Candidate>()
            .HasOne(c => c.Municipality)
            .WithMany(m => m.Candidates)
            .HasForeignKey(c => c.MunicipalityId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Candidate>()
            .HasOne(c => c.Position)
            .WithMany(p => p.Candidates)
            .HasForeignKey(c => c.PositionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Municipality>().HasQueryFilter(m => CurrentTenantId == null || m.MunicipalityId == CurrentTenantId);
        builder.Entity<Position>().HasQueryFilter(p => CurrentTenantId == null || p.MunicipalityId == CurrentTenantId);
        builder.Entity<Candidate>().HasQueryFilter(c => CurrentTenantId == null || c.MunicipalityId == CurrentTenantId);
    }

    public DbSet<Municipality>? Municipalities { get; set; }
    public DbSet<Position>? Positions { get; set; }
    public DbSet<Candidate>? Candidates { get; set; }
}
