using HistoryMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HistoryMap.Api.Data;

public class HistoryMapDbContext : DbContext
{
    public HistoryMapDbContext(DbContextOptions<HistoryMapDbContext> options)
        : base(options)
    {
    }

    public DbSet<Place> Places => Set<Place>();
    public DbSet<Summary> Summaries => Set<Summary>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<ViewCount> ViewCounts => Set<ViewCount>();
    public DbSet<SummaryQueueItem> SummaryQueue => Set<SummaryQueueItem>();
    public DbSet<SchemaMigration> SchemaMigrations => Set<SchemaMigration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Place>(e =>
        {
            e.ToTable("Places");
            e.HasKey(x => x.Ref);
            e.Property(x => x.Ref).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.Property(x => x.AddressLine1).HasMaxLength(256);
            e.Property(x => x.AddressLine2).HasMaxLength(256);
            e.Property(x => x.City).HasMaxLength(128);
            e.Property(x => x.State).HasMaxLength(64);
            e.Property(x => x.PostalCode).HasMaxLength(20);
            e.Property(x => x.Country).HasMaxLength(64);
            e.Property(x => x.PlaceType).HasMaxLength(64);
            e.HasIndex(x => new { x.Latitude, x.Longitude });
        });

        modelBuilder.Entity<Summary>(e =>
        {
            e.ToTable("Summaries");
            e.HasKey(x => x.Ref);
            e.Property(x => x.Ref).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.SummaryText).IsRequired();
            e.Property(x => x.SourceHeading).HasMaxLength(256);
            e.Property(x => x.Confidence).HasPrecision(4, 3);
            e.Property(x => x.SourceUrl).HasMaxLength(1024);
            e.Property(x => x.ReviewNote).HasMaxLength(1024);
            e.HasOne(x => x.Place)
                .WithOne(p => p.Summary)
                .HasForeignKey<Summary>(x => x.Ref);
        });

        modelBuilder.Entity<Photo>(e =>
        {
            e.ToTable("Photos");
            e.HasKey(x => x.PhotoId);
            e.Property(x => x.Ref).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.BlobPath).HasMaxLength(512).IsRequired();
            e.Property(x => x.Caption).HasMaxLength(512);
            e.Property(x => x.Source).HasMaxLength(256);
            e.HasIndex(x => x.Ref);
            e.HasOne(x => x.Place)
                .WithMany(p => p.Photos)
                .HasForeignKey(x => x.Ref);
        });

        modelBuilder.Entity<ViewCount>(e =>
        {
            e.ToTable("ViewCounts");
            e.HasKey(x => new { x.Ref, x.ViewDay, x.AppName });
            e.Property(x => x.Ref).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.AppName).HasMaxLength(32);
            e.HasOne(x => x.Place)
                .WithMany(p => p.ViewCounts)
                .HasForeignKey(x => x.Ref);
        });

        modelBuilder.Entity<SummaryQueueItem>(e =>
        {
            e.ToTable("SummaryQueue");
            e.HasKey(x => x.Ref);
            e.Property(x => x.Ref).HasMaxLength(12).IsUnicode(false);
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.LastError).HasMaxLength(1024);
            e.HasOne(x => x.Place)
                .WithOne(p => p.SummaryQueueItem)
                .HasForeignKey<SummaryQueueItem>(x => x.Ref);
        });

        modelBuilder.Entity<SchemaMigration>(e =>
        {
            e.ToTable("SchemaMigrations");
            e.HasKey(x => x.MigrationId);
            e.Property(x => x.MigrationId).HasMaxLength(64);
            e.Property(x => x.Notes).HasMaxLength(512);
        });
    }
}
