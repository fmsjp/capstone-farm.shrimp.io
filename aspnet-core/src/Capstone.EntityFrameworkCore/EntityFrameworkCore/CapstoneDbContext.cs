using Abp.Zero.EntityFrameworkCore;
using Capstone.Alerts;
using Capstone.Authorization.Roles;
using Capstone.Authorization.Users;
using Capstone.Compliance;
using Capstone.Domain;
using Capstone.Farms;
using Capstone.Harvests;
using Capstone.Inventory;
using Capstone.MultiTenancy;
using Capstone.Ponds;
using Capstone.Production;
using Capstone.ReferenceData;
using Capstone.Resources;
using Capstone.Sales;
using Microsoft.EntityFrameworkCore;

namespace Capstone.EntityFrameworkCore;

public class CapstoneDbContext : AbpZeroDbContext<Tenant, Role, User, CapstoneDbContext>
{
    public DbSet<FarmProfile> FarmProfiles { get; set; }
    public DbSet<DocumentCategory> DocumentCategories { get; set; }
    public DbSet<DocumentPreset> DocumentPresets { get; set; }
    public DbSet<ComplianceDocument> ComplianceDocuments { get; set; }
    public DbSet<FarmNotification> FarmNotifications { get; set; }
    public DbSet<FarmNotificationRead> FarmNotificationReads { get; set; }
    public DbSet<PondNamePrefix> PondNamePrefixes { get; set; }
    public DbSet<SizeGrade> SizeGrades { get; set; }
    public DbSet<NumberSequence> NumberSequences { get; set; }
    public DbSet<Pond> Ponds { get; set; }
    public DbSet<PondHistory> PondHistories { get; set; }
    public DbSet<PondPreparationRound> PondPreparationRounds { get; set; }
    public DbSet<PondPreparationEntry> PondPreparationEntries { get; set; }
    public DbSet<FryDelivery> FryDeliveries { get; set; }
    public DbSet<FryDeliveryStatusChange> FryDeliveryStatusChanges { get; set; }
    public DbSet<BatchFailure> BatchFailures { get; set; }
    public DbSet<HatcherySource> HatcherySources { get; set; }
    public DbSet<HatcheryCertificate> HatcheryCertificates { get; set; }
    public DbSet<Buyer> Buyers { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<InventoryLot> InventoryLots { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<ProductionCycle> ProductionCycles { get; set; }
    public DbSet<PondBatch> PondBatches { get; set; }
    public DbSet<ProductionLog> ProductionLogs { get; set; }
    public DbSet<PondPreparationDetail> PondPreparationDetails { get; set; }
    public DbSet<FeedingSession> FeedingSessions { get; set; }
    public DbSet<GrowthSamplingDetail> GrowthSamplingDetails { get; set; }
    public DbSet<ChemicalApplicationDetail> ChemicalApplicationDetails { get; set; }
    public DbSet<MortalityDetail> MortalityDetails { get; set; }
    public DbSet<PostProductionDetail> PostProductionDetails { get; set; }
    public DbSet<WaterQualityMeasurement> WaterQualityMeasurements { get; set; }
    public DbSet<Harvest> Harvests { get; set; }
    public DbSet<HarvestGradeLine> HarvestGradeLines { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleLine> SaleLines { get; set; }
    public DbSet<SalePayment> SalePayments { get; set; }
    public DbSet<HarvestDisposal> HarvestDisposals { get; set; }
    public DbSet<ParameterDefinition> ParameterDefinitions { get; set; }
    public DbSet<DensityBand> DensityBands { get; set; }
    public DbSet<FarmParameterRange> FarmParameterRanges { get; set; }
    public DbSet<FarmDensityBand> FarmDensityBands { get; set; }
    public DbSet<FarmThreshold> FarmThresholds { get; set; }
    public DbSet<Production.ReopenEvent> ReopenEvents { get; set; }
    public DbSet<Resources.FryDeliveryShortfall> FryDeliveryShortfalls { get; set; }
    public DbSet<Health.BackupRun> BackupRuns { get; set; }

    public CapstoneDbContext(DbContextOptions<CapstoneDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        FpmsModelConfiguration.Configure(modelBuilder);
    }
}
