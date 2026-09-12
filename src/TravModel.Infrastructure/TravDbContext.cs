using Microsoft.EntityFrameworkCore;
using TravModel.Domain;

namespace TravModel.Infrastructure;

public sealed class TravDbContext(DbContextOptions<TravDbContext> options) : DbContext(options)
{
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<Race> Races => Set<Race>();
    public DbSet<Horse> Horses => Set<Horse>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Starter> Starters => Set<Starter>();
    public DbSet<HistoricalStart> HistoricalStarts => Set<HistoricalStart>();
    public DbSet<HistoricalStartRevision> HistoricalStartRevisions => Set<HistoricalStartRevision>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<IdentityReview> IdentityReviews => Set<IdentityReview>();
    public DbSet<ProviderCheckpoint> ProviderCheckpoints => Set<ProviderCheckpoint>();
    public DbSet<SourceQualification> SourceQualifications => Set<SourceQualification>();
    public DbSet<RaceResult> Results => Set<RaceResult>();
    public DbSet<BettingMarketMembership> BettingMarketMemberships => Set<BettingMarketMembership>();
    public DbSet<Observation> Observations => Set<Observation>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();
    public DbSet<RawArtifactManifest> RawArtifactManifests => Set<RawArtifactManifest>();
    public DbSet<PredictionRun> PredictionRuns => Set<PredictionRun>();
    public DbSet<StarterPrediction> StarterPredictions => Set<StarterPrediction>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<ModelEvaluation> ModelEvaluations => Set<ModelEvaluation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Track>().HasIndex(x => new { x.ExternalSource, x.ExternalId }).IsUnique();
        modelBuilder.Entity<Meeting>().HasIndex(x => new { x.ExternalSource, x.ExternalId }).IsUnique();
        modelBuilder.Entity<Race>().HasIndex(x => new { x.ExternalSource, x.ExternalId }).IsUnique();
        modelBuilder.Entity<Horse>().HasIndex(x => new { x.ExternalSource, x.ExternalId }).IsUnique();
        modelBuilder.Entity<Person>().HasIndex(x => new { x.ExternalSource, x.ExternalId }).IsUnique();
        modelBuilder.Entity<Starter>().HasIndex(x => new { x.RaceId, x.HorseId }).IsUnique();
        modelBuilder.Entity<HistoricalStart>().HasIndex(x => new { x.HorseId, x.CanonicalStartKey }).IsUnique();
        modelBuilder.Entity<HistoricalStart>().HasIndex(x => new { x.SourceName, x.ExternalRaceId });
        modelBuilder.Entity<HistoricalStartRevision>().HasIndex(x => x.ContentHash).IsUnique();
        modelBuilder.Entity<HistoricalStartRevision>().HasIndex(x => new { x.HistoricalStartId, x.RetrievedAtUtc });
        modelBuilder.Entity<ExternalIdentity>().HasIndex(x => new { x.EntityType, x.SourceName, x.ExternalId }).IsUnique();
        modelBuilder.Entity<ExternalIdentity>().HasIndex(x => new { x.EntityType, x.EntityId });
        modelBuilder.Entity<IdentityReview>().HasIndex(x => new { x.EntityType, x.SourceName, x.ExternalId, x.Status });
        modelBuilder.Entity<ProviderCheckpoint>().HasIndex(x => new { x.Provider, x.Scope }).IsUnique();
        modelBuilder.Entity<SourceQualification>().HasIndex(x => new { x.SourceName, x.Capability }).IsUnique();
        modelBuilder.Entity<RaceResult>().HasIndex(x => new { x.RaceId, x.StarterId }).IsUnique();
        modelBuilder.Entity<BettingMarketMembership>().HasIndex(x => new { x.RaceId, x.Product, x.ProductId }).IsUnique();
        modelBuilder.Entity<Observation>().HasIndex(x => x.ContentHash).IsUnique();
        modelBuilder.Entity<Observation>().HasIndex(x => new { x.EntityType, x.EntityId, x.Field, x.RetrievedAtUtc });
        modelBuilder.Entity<PredictionRun>().HasIndex(x => new { x.RaceId, x.ModelVersionId, x.FeatureCutoffUtc }).IsUnique();
        modelBuilder.Entity<StarterPrediction>().HasIndex(x => new { x.PredictionRunId, x.StarterId }).IsUnique();
        modelBuilder.Entity<ModelVersion>().HasIndex(x => x.Version).IsUnique();
        modelBuilder.Entity<RawArtifactManifest>().HasIndex(x => new { x.IngestionRunId, x.Sha256 }).IsUnique();

        modelBuilder.Entity<Starter>().HasOne(x => x.Horse).WithMany().HasForeignKey(x => x.HorseId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Starter>().HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Starter>().HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<HistoricalStart>().HasOne(x => x.Horse).WithMany().HasForeignKey(x => x.HorseId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<HistoricalStart>().HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<HistoricalStart>().HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<HistoricalStartRevision>().HasOne(x => x.HistoricalStart).WithMany().HasForeignKey(x => x.HistoricalStartId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RaceResult>().HasOne(x => x.Race).WithMany().HasForeignKey(x => x.RaceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RaceResult>().HasOne(x => x.Starter).WithMany().HasForeignKey(x => x.StarterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PredictionRun>().HasOne(x => x.Race).WithMany().HasForeignKey(x => x.RaceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PredictionRun>().HasOne(x => x.ModelVersion).WithMany().HasForeignKey(x => x.ModelVersionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StarterPrediction>().HasOne(x => x.Starter).WithMany().HasForeignKey(x => x.StarterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ModelEvaluation>().HasOne(x => x.ModelVersion).WithMany().HasForeignKey(x => x.ModelVersionId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Race>().Property(x => x.StartMethod).HasConversion<string>().HasMaxLength(16);
        modelBuilder.Entity<ModelVersion>().Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        modelBuilder.Entity<ModelVersion>().Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        modelBuilder.Entity<Observation>().Property(x => x.State).HasConversion<string>().HasMaxLength(24);
        modelBuilder.Entity<IdentityReview>().Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        modelBuilder.Entity<SourceQualification>().Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        modelBuilder.Entity<SourceQualification>().Property(x => x.BaseUri).HasConversion<string>();

        modelBuilder.Entity<Observation>()
            .HasOne(x => x.IngestionRun)
            .WithMany()
            .HasForeignKey(x => x.IngestionRunId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RaceResult>().Property(x => x.KilometerTimeSeconds).HasPrecision(8, 3);
        modelBuilder.Entity<RaceResult>().Property(x => x.WinningMarginMetres).HasPrecision(10, 3);
        modelBuilder.Entity<RaceResult>().Property(x => x.PrizeMoneySek).HasPrecision(18, 2);
        modelBuilder.Entity<HistoricalStart>().Property(x => x.KilometerTimeSeconds).HasPrecision(8, 3);
        modelBuilder.Entity<HistoricalStart>().Property(x => x.Odds).HasPrecision(12, 4);
        modelBuilder.Entity<HistoricalStart>().Property(x => x.PrizeMoneySek).HasPrecision(18, 2);
        modelBuilder.Entity<HistoricalStartRevision>().Property(x => x.KilometerTimeSeconds).HasPrecision(8, 3);
        modelBuilder.Entity<HistoricalStartRevision>().Property(x => x.Odds).HasPrecision(12, 4);
        modelBuilder.Entity<HistoricalStartRevision>().Property(x => x.PrizeMoneySek).HasPrecision(18, 2);
        modelBuilder.Entity<StarterPrediction>().Property(x => x.MarketOdds).HasPrecision(12, 4);
        modelBuilder.Entity<Track>().Property(x => x.WidthMetres).HasPrecision(8, 2);
    }
}
