import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header [narrow]="true" title="Farm standards" subtitle="Water-quality ranges, density bands and warning thresholds this farm uses."></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid fpms-narrow">
                @if (!s()) {
                    <fpms-loading></fpms-loading>
                } @else {
                    @let x = s();
                    <div class="alert alert-light border small">
                        <i class="fas fa-info-circle mr-1"></i>Defaults come from PNS/BAFS 197:2025 and the team's defaults (marked †, to be confirmed with the partner farm).
                        A farm value applies to new entries only; earlier readings and stockings keep the flag they were given. Nothing here blocks saving: values outside a range only warn.
                        @if (x.canEdit) {
                            <div class="mt-2">
                                @if (x.reviewedAtUtc) {
                                    <span class="text-success"><i class="fas fa-check mr-1"></i>Reviewed {{ x.reviewedAtUtc | farmDateTime }}.</span>
                                } @else {
                                    <button class="btn btn-sm btn-outline-primary" [disabled]="busy()" (click)="markReviewed()">I have reviewed these standards</button>
                                }
                            </div>
                        }
                    </div>

                    <div class="card">
                        <div class="card-header"><h3 class="card-title"><i class="fas fa-tint" aria-hidden="true"></i>Water-quality ranges</h3></div>
                        <div class="card-body p-0 table-responsive">
                            <table class="table table-sm mb-0 fpms-standards">
                                <thead><tr><th>Parameter</th><th>Standard</th><th>Farm minimum</th><th>Farm maximum</th><th></th></tr></thead>
                                <tbody>
                                    @for (p of x.parameters; track p.code) {
                                        <tr>
                                            <td>{{ p.name }} <span class="text-muted small">{{ p.unit }}</span></td>
                                            <td class="small">{{ p.standardRange }}<div class="text-muted">{{ p.source }}</div></td>
                                            <td><input type="number" step="any" class="form-control form-control-sm" [(ngModel)]="edit[p.code].min" [disabled]="!x.canEdit" [attr.aria-label]="p.name + ' farm minimum'" [placeholder]="p.standardMin ?? 'none'" /></td>
                                            <td><input type="number" step="any" class="form-control form-control-sm" [(ngModel)]="edit[p.code].max" [disabled]="!x.canEdit" [attr.aria-label]="p.name + ' farm maximum'" [placeholder]="p.standardMax ?? 'none'" /></td>
                                            <td class="text-nowrap">
                                                @if (p.differs) { <span class="badge badge-info mr-1">Differs from standard</span> }
                                                @if (x.canEdit) {
                                                    <button class="btn btn-xs btn-primary mr-1" [disabled]="busy()" (click)="saveParameter(p)">Save</button>
                                                    @if (p.differs) { <button class="btn btn-xs btn-outline-secondary" [disabled]="busy()" (click)="resetParameter(p)">Reset to standard</button> }
                                                }
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        </div>
                    </div>

                    <div class="card">
                        <div class="card-header"><h3 class="card-title"><i class="fas fa-shrimp" aria-hidden="true"></i>Stocking density bands (PL/m²)</h3></div>
                        <div class="card-body p-0 table-responsive">
                            <table class="table table-sm mb-0 fpms-standards">
                                <thead><tr><th>Farming system</th><th>Construction</th><th>Standard</th><th>Farm minimum</th><th>Farm maximum</th><th></th></tr></thead>
                                <tbody>
                                    @for (b of x.densityBands; track bandKey(b)) {
                                        <tr>
                                            <td>{{ b.farmingSystem | label: L.FarmingSystems }}</td>
                                            <td>{{ b.constructionType | label: L.ConstructionTypes }}</td>
                                            <td class="small">{{ b.standardMin ?? '—' }}–{{ b.standardMax ?? '—' }}<div class="text-muted">{{ b.source }}</div></td>
                                            <td><input type="number" min="0" step="any" class="form-control form-control-sm" [(ngModel)]="bandEdit[bandKey(b)].min" [disabled]="!x.canEdit" aria-label="Farm minimum" [placeholder]="b.standardMin ?? ''" /></td>
                                            <td><input type="number" min="0" step="any" class="form-control form-control-sm" [(ngModel)]="bandEdit[bandKey(b)].max" [disabled]="!x.canEdit" aria-label="Farm maximum" [placeholder]="b.standardMax ?? ''" /></td>
                                            <td class="text-nowrap">
                                                @if (b.differs) { <span class="badge badge-info mr-1">Differs from standard</span> }
                                                @if (x.canEdit) {
                                                    <button class="btn btn-xs btn-primary mr-1" [disabled]="busy()" (click)="saveBand(b)">Save</button>
                                                    @if (b.differs) { <button class="btn btn-xs btn-outline-secondary" [disabled]="busy()" (click)="resetBand(b)">Reset to standard</button> }
                                                }
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        </div>
                    </div>

                    <div class="card">
                        <div class="card-header"><h3 class="card-title"><i class="fas fa-exclamation-triangle" aria-hidden="true"></i>Warning thresholds</h3></div>
                        <div class="card-body p-0 table-responsive">
                            <table class="table table-sm mb-0 fpms-standards">
                                <thead><tr><th>Warn when</th><th>Standard</th><th>Farm value</th><th></th></tr></thead>
                                <tbody>
                                    @for (t of x.thresholds; track t.code) {
                                        <tr>
                                            <td>{{ t.name }}<div class="small text-muted">allowed {{ t.min }}–{{ t.max }} {{ t.unit }}</div></td>
                                            <td class="small">{{ t.default }} {{ t.unit }}<div class="text-muted">{{ t.source }}</div></td>
                                            <td style="width: 9rem">
                                                <div class="input-group input-group-sm">
                                                    <input type="number" step="any" [min]="t.min" [max]="t.max" class="form-control" [(ngModel)]="thresholdEdit[t.code]" [disabled]="!x.canEdit" [attr.aria-label]="t.name" [placeholder]="t.default" />
                                                    <div class="input-group-append"><span class="input-group-text">{{ t.unit }}</span></div>
                                                </div>
                                            </td>
                                            <td class="text-nowrap">
                                                @if (t.differs) { <span class="badge badge-info mr-1">Differs from standard</span> }
                                                @if (x.canEdit) {
                                                    <button class="btn btn-xs btn-primary mr-1" [disabled]="busy() || thresholdEdit[t.code] == null" (click)="saveThreshold(t)">Save</button>
                                                    @if (t.differs) { <button class="btn btn-xs btn-outline-secondary" [disabled]="busy()" (click)="resetThreshold(t)">Reset to standard</button> }
                                                }
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        </div>
                    </div>
                }
            </div>
        </section>
    `,
    styles: [
        `
            .fpms-standards input.form-control {
                min-width: 6rem;
            }
        `,
    ],
})
export class FarmStandardsComponent extends FpmsPage implements OnInit {
    s = signal<any>(null);
    edit: Record<string, { min: number | null; max: number | null }> = {};
    bandEdit: Record<string, { min: number | null; max: number | null }> = {};
    thresholdEdit: Record<string, number | null> = {};

    ngOnInit(): void {
        this.load();
    }

    bandKey(b: any): string {
        return `${b.farmingSystem}-${b.constructionType}`;
    }

    async load(): Promise<void> {
        const x = await this.api.get<any>('FarmStandards', 'Get');
        this.edit = Object.fromEntries(x.parameters.map((p: any) => [p.code, { min: p.differs ? p.farmMin : null, max: p.differs ? p.farmMax : null }]));
        this.bandEdit = Object.fromEntries(x.densityBands.map((b: any) => [this.bandKey(b), { min: b.differs ? b.farmMin : null, max: b.differs ? b.farmMax : null }]));
        this.thresholdEdit = Object.fromEntries(x.thresholds.map((t: any) => [t.code, t.differs ? t.farmValue : null]));
        this.s.set(x);
    }

    private num(v: any): number | null {
        return v === '' || v == null ? null : Number(v);
    }

    private async save(method: string, body: any, message: string): Promise<void> {
        if (await this.act(() => this.api.post('FarmStandards', method, body))) {
            this.notifySaved(message);
            await this.load();
        }
    }

    saveParameter(p: any): Promise<void> {
        const e = this.edit[p.code];
        return this.save('SetParameterRange', { code: p.code, optimalMin: this.num(e.min), optimalMax: this.num(e.max) }, `${p.name} range saved. New readings use it.`);
    }

    resetParameter(p: any): Promise<void> {
        return this.save('ResetParameterRange', { code: p.code }, `${p.name} reset to the standard.`);
    }

    saveBand(b: any): Promise<void> {
        const e = this.bandEdit[this.bandKey(b)];
        return this.save('SetDensityBand', { farmingSystem: b.farmingSystem, constructionType: b.constructionType, minPlM2: this.num(e.min), maxPlM2: this.num(e.max) }, 'Density band saved.');
    }

    resetBand(b: any): Promise<void> {
        return this.save('ResetDensityBand', { farmingSystem: b.farmingSystem, constructionType: b.constructionType }, 'Density band reset to the standard.');
    }

    saveThreshold(t: any): Promise<void> {
        return this.save('SetThreshold', { code: t.code, value: this.num(this.thresholdEdit[t.code]) }, 'Threshold saved.');
    }

    resetThreshold(t: any): Promise<void> {
        return this.save('ResetThreshold', { code: t.code }, 'Threshold reset to the standard.');
    }

    markReviewed(): Promise<void> {
        return this.save('MarkReviewed', {}, 'Farm Standards marked as reviewed.');
    }
}
