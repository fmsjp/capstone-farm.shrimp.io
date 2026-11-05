using Capstone.Alerts;
using Capstone.Authorization.Users;
using Capstone.Compliance;
using Capstone.Domain;
using Capstone.Farms;
using Capstone.Harvests;
using Capstone.Inventory;
using Capstone.Ponds;
using Capstone.Production;
using Capstone.ReferenceData;
using Capstone.MultiTenancy;
using Capstone.Resources;
using Capstone.Sales;
using Microsoft.EntityFrameworkCore;

namespace Capstone.EntityFrameworkCore;

public static class FpmsModelConfiguration
{
    private const string NotDeleted = "[IsDeleted] = 0";

    public static void Configure(ModelBuilder b)
    {
        b.Entity<Tenant>(e => e.HasIndex(x => x.TenancyName).IsUnique());
        b.Entity<User>(e => e.HasIndex(x => new { x.TenantId, x.NormalizedUserName }).IsUnique());

        b.Entity<FarmProfile>(e =>
        {
            e.ToTable("FpmsFarmProfiles");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FarmName).IsRequired().HasMaxLength(FarmProfile.MaxFarmNameLength);
            e.Property(x => x.AddressLine1).HasMaxLength(FarmProfile.MaxAddressLineLength);
            e.Property(x => x.AddressLine2).HasMaxLength(FarmProfile.MaxAddressLineLength);
            e.Property(x => x.CityMunicipality).HasMaxLength(FarmProfile.MaxCityLength);
            e.Property(x => x.Province).HasMaxLength(FarmProfile.MaxProvinceLength);
            e.Property(x => x.PostalCode).HasMaxLength(FarmProfile.MaxPostalCodeLength);
            e.Property(x => x.Country).HasMaxLength(FarmProfile.MaxCountryLength).HasDefaultValue(FarmProfile.DefaultCountry);
            e.Property(x => x.TotalAreaHectares).HasPrecision(10, 2);
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.Property(x => x.ContactName).HasMaxLength(100);
            e.Property(x => x.ContactPhone).HasMaxLength(50);
            e.Property(x => x.ContactEmail).HasMaxLength(256);

            e.Property(x => x.TimeZone).IsRequired().HasMaxLength(64);
            e.Property(x => x.StockingWindowDays).HasDefaultValue(FarmThresholds.DefaultStockingWindowDays);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FpmsFarmProfiles_StockingWindow", "[StockingWindowDays] BETWEEN 0 AND 3");
                t.HasCheckConstraint("CK_FpmsFarmProfiles_FarmingSystem", "[FarmingSystem] IN (1, 2, 3)");
            });
            e.HasIndex(x => x.TenantId).IsUnique();
        });

        b.Entity<DocumentCategory>(e =>
        {
            e.ToTable("FpmsDocumentCategories");
            e.Property(x => x.Name).IsRequired().HasMaxLength(DocumentCategory.MaxNameLength);
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter(NotDeleted);
        });

        b.Entity<DocumentPreset>(e =>
        {
            e.ToTable("FpmsDocumentPresets");
            e.Property(x => x.Name).IsRequired().HasMaxLength(DocumentPreset.MaxNameLength);
            e.Property(x => x.DefaultIssuingAgency).HasMaxLength(DocumentPreset.MaxAgencyLength);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.CategoryId, x.Name }).IsUnique().HasFilter(NotDeleted);
        });

        b.Entity<ComplianceDocument>(e =>
        {
            e.ToTable("FpmsComplianceDocuments");
            e.Property(x => x.Title).IsRequired().HasMaxLength(ComplianceDocument.MaxTitleLength);
            e.Property(x => x.DocumentNumber).IsRequired().HasMaxLength(ComplianceDocument.MaxNumberLength);
            e.Property(x => x.IssuingAgency).HasMaxLength(ComplianceDocument.MaxAgencyLength);
            e.Property(x => x.Notes).HasMaxLength(ComplianceDocument.MaxNotesLength);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Preset).WithMany().HasForeignKey(x => x.PresetId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.ExpiryDate });
            e.HasIndex(x => new { x.TenantId, x.CategoryId, x.DocumentNumber }).IsUnique().HasFilter("[IsDeleted] = 0 AND [IsArchived] = 0");
        });

        b.Entity<FarmNotification>(e =>
        {
            e.ToTable("FpmsNotifications");
            e.Property(x => x.Message).IsRequired().HasMaxLength(FarmNotification.MaxMessageLength);
            e.Property(x => x.RelatedRecordType).HasMaxLength(FarmNotification.MaxTypeLength);
            e.HasIndex(x => new { x.TenantId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.TenantId, x.Type, x.RelatedRecordId });
        });

        b.Entity<FarmNotificationRead>(e =>
        {
            e.ToTable("FpmsNotificationReads");
            e.HasIndex(x => new { x.TenantId, x.UserId, x.NotificationId }).IsUnique();
        });

        b.Entity<PondNamePrefix>(e =>
        {
            e.ToTable("FpmsPondNamePrefixes");
            e.Property(x => x.Name).IsRequired().HasMaxLength(PondNamePrefix.MaxNameLength);
            e.Property(x => x.ShortCode).IsRequired().HasMaxLength(PondNamePrefix.MaxShortCodeLength);
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter(NotDeleted);
            e.HasIndex(x => new { x.TenantId, x.ShortCode }).IsUnique().HasFilter(NotDeleted);
        });

        b.Entity<Pond>(e =>
        {
            e.ToTable("FpmsPonds");
            e.Property(x => x.PondCode).IsRequired().HasMaxLength(Pond.MaxCodeLength);
            e.HasOne(x => x.Prefix).WithMany().HasForeignKey(x => x.PrefixId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.PrefixId, x.Number }).IsUnique().HasFilter(NotDeleted);
            e.Property(x => x.AreaM2).HasPrecision(10, 2);
            e.Property(x => x.AverageDepthM).HasPrecision(5, 2);
            e.Property(x => x.MaxDepthM).HasPrecision(5, 2);
            e.Property(x => x.InletType).HasMaxLength(Pond.MaxTypeLength);
            e.Property(x => x.OutletType).HasMaxLength(Pond.MaxTypeLength);
            e.Property(x => x.AerationSystem).HasMaxLength(Pond.MaxAerationLength);
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.Property(x => x.Notes).HasMaxLength(Pond.MaxNotesLength);
            e.HasIndex(x => new { x.TenantId, x.PondCode }).IsUnique().HasFilter(NotDeleted);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FpmsPonds_Area", "[AreaM2] > 0");
                t.HasCheckConstraint("CK_FpmsPonds_Depth", "[AverageDepthM] > 0 AND [MaxDepthM] >= [AverageDepthM]");
                t.HasCheckConstraint("CK_FpmsPonds_Number", "[Number] BETWEEN 1 AND 999");
                t.HasCheckConstraint("CK_FpmsPonds_Construction", "[ConstructionType] IN (1, 2, 3)");
                t.HasCheckConstraint("CK_FpmsPonds_FarmingSystem", "[FarmingSystemOverride] IS NULL OR [FarmingSystemOverride] IN (1, 2, 3)");
            });
        });

        b.Entity<BatchFailure>(e =>
        {
            e.ToTable("FpmsBatchFailures");
            e.Property(x => x.Reason).IsRequired().HasMaxLength(BatchFailure.MaxReasonLength);
            e.Property(x => x.EstimatedLossPhp).HasPrecision(14, 2);
            e.HasOne<PondBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.BatchId).IsUnique().HasFilter(NotDeleted);
        });

        b.Entity<PondHistory>(e =>
        {
            e.ToTable("FpmsPondHistories");
            e.Property(x => x.Description).HasMaxLength(PondHistory.MaxDescriptionLength);
            e.HasOne<Pond>().WithMany().HasForeignKey(x => x.PondId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.PondId, x.ChangeDateUtc });
        });

        b.Entity<HatcherySource>(e =>
        {
            e.ToTable("FpmsHatcherySources");
            e.Property(x => x.Name).IsRequired().HasMaxLength(HatcherySource.MaxNameLength);
            e.Property(x => x.Address).HasMaxLength(HatcherySource.MaxAddressLength);
            e.Property(x => x.ContactPerson).HasMaxLength(HatcherySource.MaxShortLength);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.BfarAccreditationNo).HasMaxLength(HatcherySource.MaxShortLength);
            e.Property(x => x.ShortCode).IsRequired().HasMaxLength(HatcherySource.MaxShortCodeLength);
            e.HasIndex(x => new { x.TenantId, x.ShortCode }).IsUnique().HasFilter(NotDeleted);
        });

        b.Entity<HatcheryCertificate>(e =>
        {
            e.ToTable("FpmsHatcheryCertificates");
            e.Property(x => x.CertificateNo).IsRequired().HasMaxLength(HatcheryCertificate.MaxNumberLength);
            e.HasOne<HatcherySource>().WithMany().HasForeignKey(x => x.HatcheryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.HatcheryId, x.CertificateType, x.ExpiryDate });
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsHatcheryCertificates_Dates", "[ExpiryDate] IS NULL OR [IssueDate] IS NULL OR [ExpiryDate] >= [IssueDate]"));
        });

        b.Entity<Buyer>(e =>
        {
            e.ToTable("FpmsBuyers");
            e.Property(x => x.Name).IsRequired().HasMaxLength(Buyer.MaxNameLength);
            e.Property(x => x.ContactPerson).HasMaxLength(Buyer.MaxShortLength);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Address).HasMaxLength(Buyer.MaxAddressLength);
        });

        b.Entity<InventoryItem>(e =>
        {
            e.ToTable("FpmsInventoryItems");
            e.Property(x => x.Brand).HasMaxLength(InventoryItem.MaxShortLength);
            e.Property(x => x.ProductName).IsRequired().HasMaxLength(InventoryItem.MaxNameLength);
            e.Property(x => x.FeedType).HasMaxLength(50);
            e.Property(x => x.BaiRegistrationNo).HasMaxLength(InventoryItem.MaxShortLength);
            e.Property(x => x.Unit).IsRequired().HasMaxLength(10);
            e.Property(x => x.ReorderLevel).HasPrecision(14, 3);
            e.Property(x => x.PackSize).HasPrecision(14, 3);
            e.Property(x => x.ActiveIngredient).HasMaxLength(InventoryItem.MaxNameLength);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FpmsInventoryItems_PackSize", "[PackSize] IS NULL OR [PackSize] > 0");
                t.HasCheckConstraint("CK_FpmsInventoryItems_Withdrawal", "[DefaultWithdrawalDays] IS NULL OR [DefaultWithdrawalDays] BETWEEN 0 AND 365");
            });
            e.Ignore(x => x.DisplayName);
            e.HasIndex(x => new { x.TenantId, x.Brand, x.ProductName }).IsUnique().HasFilter(NotDeleted);
        });

        b.Entity<InventoryLot>(e =>
        {
            e.ToTable("FpmsInventoryLots");
            e.Property(x => x.LotNumber).IsRequired().HasMaxLength(InventoryLot.MaxLotLength);
            e.Property(x => x.Supplier).HasMaxLength(150);
            e.Property(x => x.UnitCost).HasPrecision(14, 4);
            e.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.ItemId, x.LotNumber }).IsUnique().HasFilter(NotDeleted);
        });

        b.Entity<StockMovement>(e =>
        {
            e.ToTable("FpmsStockMovements");
            e.Property(x => x.Quantity).HasPrecision(14, 3);
            e.Property(x => x.UnitCost).HasPrecision(14, 4);
            e.Property(x => x.Reason).HasMaxLength(StockMovement.MaxReasonLength);
            e.HasOne(x => x.Lot).WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.LotId, x.MovementDateUtc });
            e.HasIndex(x => new { x.TenantId, x.SourceLogId });
            e.HasIndex(x => new { x.TenantId, x.BatchId });
            e.HasIndex(x => x.SourceLogId);
            e.HasIndex(x => new { x.LotId, x.MovementDateUtc });
            e.HasIndex(x => x.BatchId);
            e.HasIndex(x => x.FeedingSessionId);
            e.HasOne<FeedingSession>().WithMany().HasForeignKey(x => x.FeedingSessionId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsStockMovements_OneSource", "[SourceLogId] IS NULL OR [FeedingSessionId] IS NULL"));
        });

        b.Entity<FeedingSession>(e =>
        {
            e.ToTable("FpmsFeedingSessions");
            e.Property(x => x.Notes).HasMaxLength(FeedingSession.MaxNotesLength);
            e.Property(x => x.VoidReason).HasMaxLength(FeedingSession.MaxVoidReasonLength);
            e.Property(x => x.FedAtTime).HasMaxLength(FeedingSession.MaxFedAtLength);
            e.Property(x => x.Status).HasDefaultValue(FeedingStatus.Fed);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.BatchId, x.FeedDate, x.Session }).IsUnique().HasFilter(NotDeleted);
            e.HasIndex(x => new { x.TenantId, x.ClientRequestId }).IsUnique();
            e.HasIndex(x => new { x.BatchId, x.FeedDate });
        });

        b.Entity<FryDelivery>(e =>
        {
            e.ToTable("FpmsFryDeliveries");
            e.Property(x => x.HatcheryBatchNumber).IsRequired().HasMaxLength(FryDelivery.MaxBatchNumberLength);
            e.Property(x => x.CostPhp).HasPrecision(14, 2);
            e.Property(x => x.Notes).HasMaxLength(FryDelivery.MaxNotesLength);
            e.Property(x => x.PlHealthCertificateNo).HasMaxLength(FryDelivery.MaxPermitLength);
            e.Property(x => x.TransportPermitNo).HasMaxLength(FryDelivery.MaxPermitLength);
            e.Property(x => x.SfscNo).HasMaxLength(FryDelivery.MaxPermitLength);
            e.Ignore(x => x.IsStockingFinished);
            e.HasOne(x => x.Hatchery).WithMany().HasForeignKey(x => x.HatcheryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.HatcheryId, x.HatcheryBatchNumber }).IsUnique().HasFilter(NotDeleted);
            e.HasIndex(x => new { x.TenantId, x.DeliveryDate });
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FpmsFryDeliveries_PlReceived", "[PlReceived] > 0 AND [PlReceived] <= 10000000");
                t.HasCheckConstraint("CK_FpmsFryDeliveries_PlAge", "[PlAgeDays] IS NULL OR [PlAgeDays] BETWEEN 1 AND 60");
                t.HasCheckConstraint("CK_FpmsFryDeliveries_Cost", "[CostPhp] IS NULL OR [CostPhp] >= 0");
            });
        });

        b.Entity<FryDeliveryShortfall>(e =>
        {
            e.ToTable("FpmsFryDeliveryShortfalls");
            e.Property(x => x.Note).HasMaxLength(FryDeliveryShortfall.MaxNoteLength);
            e.HasOne<FryDelivery>().WithMany().HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.DeliveryId });
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FpmsFryDeliveryShortfalls_Count", "[Count] > 0");
                t.HasCheckConstraint("CK_FpmsFryDeliveryShortfalls_Reason", "[Reason] IN (1, 2, 3, 4)");
            });
        });

        b.Entity<Capstone.Health.BackupRun>(e =>
        {
            e.ToTable("FpmsBackupRuns");
            e.Property(x => x.FileName).HasMaxLength(Capstone.Health.BackupRun.MaxFileNameLength);
            e.Property(x => x.Message).HasMaxLength(Capstone.Health.BackupRun.MaxMessageLength);
            e.Property(x => x.Host).HasMaxLength(Capstone.Health.BackupRun.MaxHostLength);
            e.HasIndex(x => new { x.Kind, x.StartedAtUtc });
        });

        b.Entity<ReopenEvent>(e =>
        {
            e.ToTable("FpmsReopenEvents");
            e.Property(x => x.Reason).IsRequired().HasMaxLength(ReopenEvent.MaxReasonLength);
            e.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
        });

        b.Entity<FarmParameterRange>(e =>
        {
            e.ToTable("FpmsFarmParameterRanges");
            e.Property(x => x.ParameterCode).IsRequired().HasMaxLength(ParameterDefinition.MaxCodeLength);
            e.Property(x => x.OptimalMin).HasPrecision(10, 3);
            e.Property(x => x.OptimalMax).HasPrecision(10, 3);
            e.HasIndex(x => new { x.TenantId, x.ParameterCode }).IsUnique().HasFilter(NotDeleted);
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsFarmParameterRanges_MinMax", "[OptimalMin] IS NULL OR [OptimalMax] IS NULL OR [OptimalMin] <= [OptimalMax]"));
        });

        b.Entity<FarmDensityBand>(e =>
        {
            e.ToTable("FpmsFarmDensityBands");
            e.Property(x => x.MinPlM2).HasPrecision(8, 2);
            e.Property(x => x.MaxPlM2).HasPrecision(8, 2);
            e.HasIndex(x => new { x.TenantId, x.FarmingSystem, x.ConstructionType }).IsUnique().HasFilter(NotDeleted);
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsFarmDensityBands_MinMax", "[MinPlM2] IS NULL OR [MaxPlM2] IS NULL OR [MinPlM2] <= [MaxPlM2]"));
        });

        b.Entity<FarmThreshold>(e =>
        {
            e.ToTable("FpmsFarmThresholds");
            e.Property(x => x.Code).IsRequired().HasMaxLength(FarmThreshold.MaxCodeLength);
            e.Property(x => x.Value).HasPrecision(10, 3);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter(NotDeleted);
        });

        b.Entity<FryDeliveryStatusChange>(e =>
        {
            e.ToTable("FpmsFryDeliveryStatusChanges");
            e.Property(x => x.Note).HasMaxLength(FryDeliveryStatusChange.MaxNoteLength);
            e.HasOne<FryDelivery>().WithMany().HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.DeliveryId, x.ChangedAtUtc });
        });

        b.Entity<PondPreparationRound>(e =>
        {
            e.ToTable("FpmsPondPreparationRounds");
            e.Ignore(x => x.IsOpen);
            e.Ignore(x => x.IsPrepared);
            e.HasOne<Pond>().WithMany().HasForeignKey(x => x.PondId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.PondId }).IsUnique().HasFilter("[UsedAtUtc] IS NULL AND [IsDeleted] = 0")
                .HasDatabaseName("UX_FpmsPondPreparationRounds_OneOpenRoundPerPond");
        });

        b.Entity<PondPreparationEntry>(e =>
        {
            e.ToTable("FpmsPondPreparationEntries");
            e.Property(x => x.Description).IsRequired().HasMaxLength(PondPreparationEntry.MaxDescriptionLength);
            e.HasOne<PondPreparationRound>().WithMany().HasForeignKey(x => x.RoundId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.RoundId, x.ObservedAtUtc });
            e.HasIndex(x => new { x.TenantId, x.ClientRequestId }).IsUnique();
        });

        b.Entity<ProductionCycle>(e =>
        {
            e.ToTable("FpmsProductionCycles");
            e.Property(x => x.CycleCode).IsRequired().HasMaxLength(ProductionCycle.MaxCodeLength);
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsCycles_Outcome", "[Outcome] IS NULL OR [Outcome] IN (1, 2)"));
            e.Ignore(x => x.IsClosed);
            e.HasOne(x => x.FryDelivery).WithMany().HasForeignKey(x => x.FryDeliveryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.CycleCode }).IsUnique().HasFilter(NotDeleted);
            e.HasIndex(x => new { x.TenantId, x.FryDeliveryId }).IsUnique().HasFilter(NotDeleted);
            e.HasIndex(x => new { x.TenantId, x.Status });
        });

        b.Entity<PondBatch>(e =>
        {
            e.ToTable("FpmsPondBatches");
            e.Property(x => x.BatchCode).IsRequired().HasMaxLength(PondBatch.MaxCodeLength);
            e.Property(x => x.Letter).IsRequired().HasMaxLength(PondBatch.MaxLetterLength);
            e.Property(x => x.PondAreaM2AtStocking).HasPrecision(10, 2);
            e.Property(x => x.InitialSurvivalEstimatePct).HasPrecision(5, 2);
            e.Property(x => x.HatcheryCertificationStatusAtStocking).HasMaxLength(500);
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsBatches_DensityFlag", "[DensityFlag] IS NULL OR [DensityFlag] IN (1, 2, 3)"));
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsBatches_DeadAtStocking", "[DeadAtStocking] IS NULL OR [DeadAtStocking] >= 0"));
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsBatches_SystemSnapshot", "[FarmingSystemAtStocking] IS NULL OR [FarmingSystemAtStocking] IN (1, 2, 3)"));
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsBatches_ConstructionSnapshot", "[ConstructionTypeAtStocking] IS NULL OR [ConstructionTypeAtStocking] IN (1, 2, 3)"));
            e.Property(x => x.FinalAbwG).HasPrecision(8, 3);
            e.Property(x => x.FinalFcr).HasPrecision(8, 3);
            e.Property(x => x.SurvivalRatePct).HasPrecision(6, 2);
            e.Property(x => x.TotalHarvestKg).HasPrecision(14, 3);
            e.Property(x => x.TotalFeedKg).HasPrecision(14, 3);
            e.Ignore(x => x.IsOpen);
            e.Ignore(x => x.StockingDensityPerM2);
            e.HasOne(x => x.Cycle).WithMany().HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Pond).WithMany().HasForeignKey(x => x.PondId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PondPreparationRound>().WithMany().HasForeignKey(x => x.PreparationRoundId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.BatchCode }).IsUnique().HasFilter(NotDeleted);
            e.HasIndex(x => new { x.TenantId, x.CycleId, x.Letter }).IsUnique().HasFilter(NotDeleted);
            e.HasIndex(x => x.PreparationRoundId).IsUnique().HasFilter("[PreparationRoundId] IS NOT NULL AND [IsDeleted] = 0");
            e.HasIndex(x => new { x.TenantId, x.PondId }).IsUnique().HasFilter("[Status] <> 5 AND [IsDeleted] = 0")
                .HasDatabaseName("UX_FpmsPondBatches_OneOpenBatchPerPond");
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FpmsPondBatches_Stocking", "[IsDeleted] = 1 OR ([StockedAtUtc] IS NOT NULL AND [StockingDate] IS NOT NULL AND [PlCountStocked] > 0)");
                t.HasCheckConstraint("CK_FpmsPondBatches_Acclimation", "[AcclimationMinutes] IS NULL OR [AcclimationMinutes] BETWEEN 0 AND 600");
                t.HasCheckConstraint("CK_FpmsPondBatches_Survival", "[InitialSurvivalEstimatePct] IS NULL OR [InitialSurvivalEstimatePct] BETWEEN 0 AND 100");
            });
            e.HasIndex(x => new { x.TenantId, x.Status });
        });

        b.Entity<ProductionLog>(e =>
        {
            e.ToTable("FpmsProductionLogs");
            e.Property(x => x.Notes).HasMaxLength(ProductionLog.MaxNotesLength);
            e.Property(x => x.VoidReason).HasMaxLength(ProductionLog.MaxVoidReasonLength);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Pond>().WithMany().HasForeignKey(x => x.PondId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.BatchId, x.LogType, x.ObservedAtUtc });
            e.HasIndex(x => new { x.BatchId, x.ObservedAtUtc });
            e.HasIndex(x => x.ObservedAtUtc);
            e.HasIndex(x => new { x.TenantId, x.ClientRequestId }).IsUnique();
            e.HasOne(x => x.PondPreparation).WithOne().HasForeignKey<PondPreparationDetail>(d => d.Id).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.GrowthSampling).WithOne().HasForeignKey<GrowthSamplingDetail>(d => d.Id).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ChemicalApplication).WithOne().HasForeignKey<ChemicalApplicationDetail>(d => d.Id).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Mortality).WithOne().HasForeignKey<MortalityDetail>(d => d.Id).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PostProduction).WithOne().HasForeignKey<PostProductionDetail>(d => d.Id).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Measurements).WithOne().HasForeignKey(m => m.LogId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PondPreparationDetail>(e =>
        {
            e.ToTable("FpmsPondPreparationDetails");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.SeawaterPct).HasPrecision(5, 2);
            e.Property(x => x.FreshwaterPct).HasPrecision(5, 2);
        });

        b.Entity<GrowthSamplingDetail>(e =>
        {
            e.ToTable("FpmsGrowthSamplingDetails");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.TotalSampleWeightG).HasPrecision(10, 2);
            e.Property(x => x.AbwG).HasPrecision(10, 3);
            e.Property(x => x.EstimatedSurvivalPct).HasPrecision(5, 2);
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsGrowth_Sample", "[SampleCount] > 0 AND [TotalSampleWeightG] > 0"));
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsGrowth_EstimatedSurvival", "[EstimatedSurvivalPct] IS NULL OR ([EstimatedSurvivalPct] >= 0 AND [EstimatedSurvivalPct] <= 100)"));
        });

        b.Entity<ChemicalApplicationDetail>(e =>
        {
            e.ToTable("FpmsChemicalApplicationDetails");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Reason).HasMaxLength(ChemicalApplicationDetail.MaxReasonLength);
            e.Property(x => x.Dosage).HasPrecision(14, 4);
            e.Property(x => x.DosageUnit).HasMaxLength(ChemicalApplicationDetail.MaxShortLength);
            e.Property(x => x.Method).HasMaxLength(ChemicalApplicationDetail.MaxMethodLength);
            e.Property(x => x.PrescriptionRef).HasMaxLength(ChemicalApplicationDetail.MaxPrescriptionLength);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FpmsChemical_Dosage", "[Dosage] IS NULL OR [Dosage] >= 0");
                t.HasCheckConstraint("CK_FpmsChemical_Withdrawal", "[WithdrawalDays] IS NULL OR [WithdrawalDays] BETWEEN 0 AND 365");
            });
        });

        b.Entity<MortalityDetail>(e =>
        {
            e.ToTable("FpmsMortalityDetails");
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Symptoms).HasMaxLength(MortalityDetail.MaxTextLength);
            e.Property(x => x.ResponseAction).HasMaxLength(MortalityDetail.MaxTextLength);
            e.Property(x => x.DiseaseName).HasMaxLength(MortalityDetail.MaxNameLength);
            e.Property(x => x.OtherCause).HasMaxLength(MortalityDetail.MaxTextLength);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FpmsMortality_Count", "[Count] >= 0");
                t.HasCheckConstraint("CK_FpmsMortality_CauseFields", "([Cause] = 1 OR [DiseaseName] IS NULL) AND ([Cause] = 7 OR [OtherCause] IS NULL)");
            });
        });

        b.Entity<PostProductionDetail>(e =>
        {
            e.ToTable("FpmsPostProductionDetails");
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        b.Entity<WaterQualityMeasurement>(e =>
        {
            e.ToTable("FpmsWaterQualityMeasurements");
            e.Property(x => x.ParameterCode).IsRequired().HasMaxLength(WaterQualityMeasurement.MaxCodeLength);
            e.Property(x => x.UnitCode).HasMaxLength(ParameterDefinition.MaxUnitLength);
            e.Property(x => x.Value).HasPrecision(12, 4);
            e.HasOne<ParameterDefinition>().WithMany().HasForeignKey(x => x.ParameterCode).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.LogId });
            e.HasIndex(x => new { x.LogId, x.ParameterCode }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.IsOutOfRange });
        });

        b.Entity<Harvest>(e =>
        {
            e.ToTable("FpmsHarvests");
            e.Property(x => x.TotalKg).HasPrecision(14, 3);
            e.Property(x => x.AbwG).HasPrecision(8, 2);
            e.Property(x => x.QualityNotes).HasMaxLength(Harvest.MaxNotesLength);
            e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.GradeLines).WithOne().HasForeignKey(g => g.HarvestId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Disposals).WithOne().HasForeignKey(d => d.HarvestId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.HarvestedAtUtc });
            e.HasIndex(x => new { x.TenantId, x.BatchId });
            e.HasIndex(x => x.BatchId);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FpmsHarvests_Kg", "[TotalKg] > 0");
                t.HasCheckConstraint("CK_FpmsHarvests_Type", "[HarvestType] IN (1, 2)");
                t.HasCheckConstraint("CK_FpmsHarvests_FinalCount", "[FinalCount] IS NULL OR [FinalCount] >= 0");
                t.HasCheckConstraint("CK_FpmsHarvests_DeadAtHarvest", "[DeadAtHarvest] IS NULL OR [DeadAtHarvest] >= 0");
                t.HasCheckConstraint("CK_FpmsHarvests_Abw", "[AbwG] IS NULL OR ([AbwG] > 0 AND [AbwG] <= 60)");
            });
        });

        b.Entity<HarvestGradeLine>(e =>
        {
            e.ToTable("FpmsHarvestGradeLines");
            e.Property(x => x.Kg).HasPrecision(14, 3);
            e.HasOne(x => x.SizeGrade).WithMany().HasForeignKey(x => x.SizeGradeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.HarvestId, x.SizeGradeId }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsGrades", "[Kg] > 0"));
        });

        b.Entity<HarvestDisposal>(e =>
        {
            e.ToTable("FpmsHarvestDisposals");
            e.Property(x => x.Kg).HasPrecision(14, 3);
            e.Property(x => x.Note).HasMaxLength(HarvestDisposal.MaxNoteLength);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_FpmsHarvestDisposals_Kg", "[Kg] > 0");
                t.HasCheckConstraint("CK_FpmsHarvestDisposals_Reason", "[Reason] IN (1, 2, 3, 4)");
            });
        });

        b.Entity<Sale>(e =>
        {
            e.ToTable("FpmsSales");
            e.Property(x => x.DispatchReceiptNo).IsRequired().HasMaxLength(Sale.MaxReceiptNoLength);
            e.Property(x => x.PaymentTerms).HasMaxLength(Sale.MaxTextLength);
            e.Property(x => x.Transporter).HasMaxLength(Sale.MaxShortLength);
            e.Property(x => x.DriverName).HasMaxLength(Sale.MaxShortLength);
            e.Property(x => x.PlateNo).HasMaxLength(20);
            e.Property(x => x.Destination).HasMaxLength(Sale.MaxTextLength);
            e.Property(x => x.LocalTransportPermitNo).HasMaxLength(Sale.MaxShortLength);
            e.Property(x => x.TemperatureNote).HasMaxLength(Sale.MaxTextLength);
            e.Property(x => x.VoidReason).HasMaxLength(Sale.MaxTextLength);
            e.Ignore(x => x.Total);
            e.Ignore(x => x.Paid);
            e.Ignore(x => x.Status);
            e.HasOne(x => x.Buyer).WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Payments).WithOne().HasForeignKey(p => p.SaleId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.SaleDate });
            e.HasIndex(x => new { x.TenantId, x.DispatchReceiptNo }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsSales_DueDate", "[DueDate] IS NULL OR [DueDate] >= [SaleDate]"));
        });

        b.Entity<SaleLine>(e =>
        {
            e.ToTable("FpmsSaleLines");
            e.Property(x => x.Kg).HasPrecision(14, 3);
            e.Property(x => x.BuyerAcceptedKg).HasPrecision(14, 3);
            e.Property(x => x.PricePerKg).HasPrecision(12, 2);
            e.Ignore(x => x.Amount);
            e.HasOne(x => x.HarvestGradeLine).WithMany().HasForeignKey(x => x.HarvestGradeLineId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.HarvestGradeLineId);
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsSaleLines", "[Kg] > 0 AND [PricePerKg] >= 0 AND ([BuyerAcceptedKg] IS NULL OR [BuyerAcceptedKg] >= 0)"));
        });

        b.Entity<SalePayment>(e =>
        {
            e.ToTable("FpmsSalePayments");
            e.Property(x => x.Amount).HasPrecision(14, 2);
            e.Property(x => x.Method).HasMaxLength(50);
            e.Property(x => x.Note).HasMaxLength(SalePayment.MaxTextLength);
            e.HasIndex(x => new { x.TenantId, x.SaleId });
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsSalePayments_Amount", "[Amount] > 0"));
        });

        b.Entity<DensityBand>(e =>
        {
            e.ToTable("FpmsDensityBands");
            e.Property(x => x.MinPlM2).HasPrecision(8, 2);
            e.Property(x => x.MaxPlM2).HasPrecision(8, 2);
            e.Property(x => x.Source).HasMaxLength(DensityBand.MaxSourceLength);
            e.HasIndex(x => new { x.FarmingSystem, x.ConstructionType }).IsUnique();
        });

        b.Entity<ParameterDefinition>(e =>
        {
            e.ToTable("FpmsParameterDefinitions");
            e.Property(x => x.Id).HasColumnName("Code").HasMaxLength(ParameterDefinition.MaxCodeLength).ValueGeneratedNever();
            e.Property(x => x.Name).IsRequired().HasMaxLength(ParameterDefinition.MaxNameLength);
            e.Property(x => x.UnitCode).IsRequired().HasMaxLength(ParameterDefinition.MaxUnitLength);
            e.Property(x => x.PhysicalMin).HasPrecision(12, 4);
            e.Property(x => x.PhysicalMax).HasPrecision(12, 4);
            e.Property(x => x.OptimalMin).HasPrecision(12, 4);
            e.Property(x => x.OptimalMax).HasPrecision(12, 4);
            e.Property(x => x.GuidanceFrequency).HasMaxLength(100);
            e.Property(x => x.LowRecommendation).HasMaxLength(ParameterDefinition.MaxRecommendationLength);
            e.Property(x => x.HighRecommendation).HasMaxLength(ParameterDefinition.MaxRecommendationLength);
            e.Property(x => x.Source).HasMaxLength(ParameterDefinition.MaxSourceLength);
        });

        b.Entity<SizeGrade>(e =>
        {
            e.ToTable("FpmsSizeGrades");
            e.Property(x => x.Label).IsRequired().HasMaxLength(SizeGrade.MaxLabelLength);
            e.HasIndex(x => new { x.TenantId, x.Label }).IsUnique().HasFilter(NotDeleted);
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsSizeGrades_CountRange",
                "([MinCountPerKg] IS NULL OR [MinCountPerKg] > 0) AND ([MaxCountPerKg] IS NULL OR [MinCountPerKg] IS NULL OR [MaxCountPerKg] >= [MinCountPerKg])"));
        });

        b.Entity<NumberSequence>(e =>
        {
            e.ToTable("FpmsNumberSequences");
            e.Property(x => x.Scope).IsRequired().HasMaxLength(NumberSequence.MaxScopeLength);
            e.HasIndex(x => new { x.TenantId, x.SequenceType, x.Scope }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("CK_FpmsNumberSequences_NextValue", "[NextValue] >= 1"));
        });
    }
}
