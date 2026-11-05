import { FarmTime } from './farm-time';
import { MortalityCauses, PostProductionActivities, PrepActivities } from './labels';

export function logSummary(l: any): string {
    if (l.measurements?.length) {
        return l.measurements
            .map((m: any) => `${m.parameterName ?? m.parameterCode} ${m.value} ${m.unitCode ?? ''}${m.isOutOfRange ? (m.side === 1 ? ' (low)' : m.side === 2 ? ' (high)' : ' (out of range)') : ''}`.trim())
            .join('; ');
    }
    if (l.growth) {
        const rate = l.growthRateGPerDay != null ? `, ${Number(l.growthRateGPerDay).toFixed(2)} g/day` : '';
        const survival = l.growth.estimatedSurvivalPct != null ? `, est. survival ${l.growth.estimatedSurvivalPct}%` : '';
        return `ABW ${l.abwG ?? '—'} g from ${l.growth.sampleCount} pcs${rate}${survival}`;
    }
    if (l.chemicalApplication) {
        const used = (l.usages ?? []).map((u: any) => `${u.label} ${u.quantity} ${u.unit ?? ''}`.trim()).join(', ');
        const c = l.chemicalApplication;
        const dose = c.dosage != null ? `${c.dosage} ${c.dosageUnit ?? ''}`.trim() : null;
        const safe = l.safeHarvestDate ? `safe harvest from ${FarmTime.date(l.safeHarvestDate)}` : null;
        return [used, c.reason, dose, c.method, safe].filter((x) => !!x).join(' · ');
    }
    if (l.mortality) {
        const m = l.mortality;
        const cause = MortalityCauses[m.cause] ?? '';
        const detail = m.cause === 1 ? m.diseaseName : m.cause === 7 ? m.otherCause : null;
        const high = l.isHighMortality ? ' · high (over 1% of population)' : '';
        return `${m.count} dead · ${cause}${detail ? ': ' + detail : ''}${m.symptoms ? ' · ' + m.symptoms : ''}${high}`;
    }
    if (l.pondPreparation) {
        return PrepActivities[l.pondPreparation.activityType] ?? '';
    }
    if (l.postProduction) {
        return PostProductionActivities[l.postProduction.activityType] ?? '';
    }
    return l.notes ?? '';
}
