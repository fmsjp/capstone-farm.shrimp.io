export function optimalState(p: any, raw: number | string | null | undefined): 'empty' | 'ok' | 'out' | 'invalid' {
    if (raw === null || raw === undefined || raw === '') {
        return 'empty';
    }
    const v = Number(raw);
    if (isNaN(v) || v < p.physicalMin || v > p.physicalMax) {
        return 'invalid';
    }
    if (p.optimalMin !== null && p.optimalMin !== undefined) {
        if (p.optimalMinExclusive ? v <= p.optimalMin : v < p.optimalMin) {
            return 'out';
        }
    }
    if (p.optimalMax !== null && p.optimalMax !== undefined) {
        if (p.optimalMaxExclusive ? v >= p.optimalMax : v > p.optimalMax) {
            return 'out';
        }
    }
    return 'ok';
}
