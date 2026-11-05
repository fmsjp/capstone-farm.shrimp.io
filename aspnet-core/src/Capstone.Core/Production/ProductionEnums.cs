namespace Capstone.Production;

public enum CycleStatus { Active = 2, Closed = 4 }

public enum BatchStatus { Stocked = 2, Harvested = 3, Failed = 4, Closed = 5 }

public enum TransportMode { Truck = 1, Van = 2, Motorcycle = 3, Boat = 4, Other = 5 }

public enum LogType
{
    PondPreparation = 1,
    PreStockingWaterQuality = 2,
    Feeding = 3,
    WaterQuality = 4,
    GrowthSampling = 5,
    ChemicalApplication = 6,
    Mortality = 7,
    PostProduction = 8,
    GeneralNote = 9,
}

public enum FeedingSessionTime { AM = 1, PM = 2 }

public enum FeedingStatus { Fed = 1, NotFed = 2 }

public enum DensityFlag { Within = 1, Below = 2, Above = 3 }

public enum ReadingSide { Low = 1, High = 2 }

public enum PondPreparationActivity { Draining = 1, Drying = 2, Liming = 3, Disinfection = 4, Filling = 5, Other = 6 }

public enum FeedingMethod { Broadcast = 1, Tray = 2, AutoFeeder = 3, Other = 4 }

public enum MortalityCause { Disease = 1, WaterQuality = 4, Others = 7 }

public enum PostProductionActivity { SludgeRemoval = 1, Drying = 2, EquipmentMaintenance = 3, Other = 4 }

public enum CycleOutcome { Completed = 1, Failed = 2 }

public enum ReopenedEntityType { Batch = 1, Cycle = 2 }
