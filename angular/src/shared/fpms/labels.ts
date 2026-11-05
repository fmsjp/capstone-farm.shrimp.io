export interface Option {
    value: number;
    label: string;
}

function opts(map: Record<number, string>): Option[] {
    return Object.keys(map).map((k) => ({ value: Number(k), label: map[Number(k)] }));
}

export const PondPurposes = { 1: 'Grow-out', 2: 'Nursery', 3: 'Reservoir', 4: 'Treatment' };
export const ConstructionTypes = { 1: 'Earthen', 2: 'Concrete', 3: 'HDPE-lined' };
export const FarmingSystems = { 1: 'Extensive', 2: 'Semi-intensive', 3: 'Intensive' };
export const PondManualStatuses = { 1: 'Active', 2: 'Under maintenance', 3: 'Inactive' };
export const PondDisplayStatuses = { 1: 'Available', 2: 'Under preparation', 7: 'Prepared', 3: 'In production', 4: 'Post-harvest', 5: 'Under maintenance', 6: 'Inactive', 8: 'Archived' };
export const PondChangeTypes = { 1: 'Created', 2: 'Status change', 3: 'Infrastructure modification', 4: 'Preparation change', 5: 'Archived', 6: 'Restored' };
export const AccreditationStatuses = { 1: 'Accredited', 2: 'Pending', 3: 'Not accredited', 4: 'Unknown' };
export const CertificateTypes = { 1: 'Health', 2: 'Other' };
export const BuyerTypes = { 1: 'Individual', 2: 'Company' };
export const InventoryCategories = { 1: 'Feed', 2: 'Additive / supplement', 3: 'Chemical' };
export const MovementTypes = { 1: 'Receipt', 2: 'Usage', 3: 'Adjustment' };
export const CycleStatuses = { 2: 'Active', 4: 'Closed' };
export const BatchStatuses = { 2: 'Stocked', 3: 'Harvested', 4: 'Failed', 5: 'Closed' };
export const TransportModes = { 1: 'Truck', 2: 'Van', 3: 'Motorcycle', 4: 'Boat', 5: 'Other' };
export const FryDeliveryStatuses = { 1: 'Ordered', 2: 'In transit', 3: 'Received', 4: 'Rejected' };
export const LogTypes = {
    1: 'Pond preparation (historical)',
    2: 'Pre-stocking water quality',
    3: 'Feeding (historical)',
    4: 'Water quality',
    5: 'Growth sampling',
    6: 'Chemical and supplement application',
    7: 'Mortality',
    8: 'Post-production (historical)',
    9: 'General note',
};
export const NewLogTypes = [4, 5, 6, 7, 9];
export const LogTypeNames: Record<string, number> = {
    PondPreparation: 1,
    PreStockingWaterQuality: 2,
    Feeding: 3,
    WaterQuality: 4,
    GrowthSampling: 5,
    ChemicalApplication: 6,
    Mortality: 7,
    PostProduction: 8,
    GeneralNote: 9,
};
export const PrepActivities = { 1: 'Draining', 2: 'Drying', 3: 'Liming', 4: 'Disinfection', 5: 'Filling', 6: 'Other' };
export const FeedingMethods = { 1: 'Broadcast', 2: 'Tray', 3: 'Auto-feeder', 4: 'Other' };
export const FeedingSessions = { 1: 'AM', 2: 'PM' };
export const FeedingStatuses = { 1: 'Fed', 2: 'Not fed' };
export const DensityFlags = { 1: 'Within band', 2: 'Below band', 3: 'Above band' };
export const ShortfallReasons = { 1: 'Dead on arrival', 2: 'Discarded', 3: 'Returned', 4: 'Count difference' };
export const CycleOutcomes = { 1: 'Completed', 2: 'Failed' };
export const MortalityCauses = { 1: 'Disease', 4: 'Water quality', 7: 'Others' };
export const ReadingSides = { 1: 'Low', 2: 'High' };
export const PostProductionActivities = { 1: 'Sludge removal', 2: 'Drying', 3: 'Equipment maintenance', 4: 'Other' };
export const HarvestTypes = { 1: 'Partial', 2: 'Total' };
export const DisposalReasons = { 1: 'Staff or home use', 2: 'Given away', 3: 'Spoiled or lost', 4: 'Other' };
export const PaymentStatuses = { 1: 'Unpaid', 2: 'Partial', 3: 'Paid' };
export const ExpiryStatuses = { 0: 'No expiry', 1: 'Active', 2: 'Expiring soon', 3: 'Expired' };
export const NotificationTypes = { 1: 'Permit expiring', 2: 'Permit expired', 3: 'Water quality', 4: 'Low stock', 5: 'Negative stock', 6: 'High mortality' };
export const VerificationMethods = { 1: 'Second Farm Owner', 2: 'Pre-agreed security question' };

export const Options = {
    pondPurpose: opts(PondPurposes),
    construction: opts(ConstructionTypes),
    farmingSystem: opts(FarmingSystems),
    pondManualStatus: opts(PondManualStatuses),
    pondChangeType: opts(PondChangeTypes),
    accreditation: opts(AccreditationStatuses),
    certificateType: opts(CertificateTypes),
    buyerType: opts(BuyerTypes),
    inventoryCategory: opts(InventoryCategories),
    cycleStatus: opts(CycleStatuses),
    batchStatus: opts(BatchStatuses),
    transportMode: opts(TransportModes),
    fryDeliveryStatus: opts(FryDeliveryStatuses),
    logType: opts(LogTypes),
    newLogType: opts(LogTypes).filter((o) => NewLogTypes.includes(o.value)),
    feedingSession: opts(FeedingSessions),
    mortalityCause: opts(MortalityCauses),
    harvestType: opts(HarvestTypes),
    disposalReason: opts(DisposalReasons),
    shortfallReason: opts(ShortfallReasons),
    cycleOutcome: opts(CycleOutcomes),
    paymentStatus: opts(PaymentStatuses),
    verification: opts(VerificationMethods),
};

export const InventoryUnits = ['kg', 'g', 'L', 'mL', 'pcs'];

export function toneFor(label: string): string {
    switch (label) {
        case 'Available':
        case 'Active':
        case 'Approved':
        case 'Valid':
        case 'Paid':
        case 'Accredited':
        case 'Income':
            return 'success';
        case 'Stocked':
        case 'In production':
        case 'Prepared':
        case 'Received':
        case 'In transit':
        case 'No expiry':
            return 'info';
        case 'Planned':
        case 'Ordered':
        case 'Under preparation':
        case 'Pending':
        case 'Partial':
        case 'Expiring soon':
        case 'Low':
        case 'Reconcile':
        case 'Under maintenance':
        case 'Post-harvest':
        case 'Harvested':
            return 'warning';
        case 'Failed':
        case 'Expired':
        case 'Rejected':
        case 'Unpaid':
        case 'Negative':
        case 'Not accredited':
        case 'Expense':
            return 'danger';
        default:
            return 'secondary';
    }
}

export function pondSystemWarning(construction: number, system: number): string | null {
    if (system === 1 && (construction === 2 || construction === 3)) {
        return 'Extensive culture relies on natural food from an earthen pond bottom; a concrete or HDPE-lined pond is unusual for this system.';
    }
    return null;
}
