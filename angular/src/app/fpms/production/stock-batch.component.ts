import { Component, OnInit } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime, newGuid } from '@shared/fpms/farm-time';
import { WizardSteps } from '@shared/fpms/wizard-steps';
import { FPMS_IMPORTS } from '../shared-imports';
import { optimalState } from './water-quality';

interface Allocation {
    pond: any;
    selected: boolean;
    plCount: number | null;
    stockedAt: string;
    transportMode: number;
    timeProcessed: string;
    acclimationMinutes: number | null;
    initialSurvivalEstimatePct: number | null;
    deadAtStocking: number | null;
    readings: Record<string, number | null>;
}

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <div class="fpms-dialog-backdrop">
            <div class="fpms-dialog" role="dialog" aria-modal="true" aria-labelledby="cb-h">
                <div class="fpms-dialog-head">
                    <div class="flex-fill">
                        <h1 id="cb-h">{{ cycleCode ? 'Create batch · ' + cycleCode : 'Create batch (stocking)' }}</h1>
                        <p class="text-muted mb-0">Stock fry from one delivery into prepared ponds. Nothing is saved until the last step.</p>
                    </div>
                    <a class="fpms-dialog-close" [routerLink]="closeLink" aria-label="Close" title="Close">&times;</a>
                </div>
                <div class="fpms-dialog-body">
                <ol class="fpms-steps mb-3" aria-label="Progress">
                    @for (s of stepNames; track $index) {
                        <li [class.active]="step === $index" [class.done]="step > $index" [attr.aria-current]="step === $index ? 'step' : null">{{ $index + 1 }} · {{ s }}</li>
                    }
                </ol>
                @if (!options) {
                    <fpms-loading></fpms-loading>
                } @else {
                    @switch (step) {
                        @case (0) {
                            <div class="card">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-truck" aria-hidden="true"></i>Fry source</h3></div>
                                <div class="card-body">
                                    @if (!options.deliveries.length) {
                                        <fpms-empty text="No Received fry delivery is waiting to be stocked. Record a delivery and set its status to Received first." icon="fas fa-truck">
                                            @if (can('Farm.FryDeliveries')) {
                                                <a routerLink="/app/resources/fry-deliveries" class="btn btn-sm btn-primary">Go to fry deliveries</a>
                                            }
                                        </fpms-empty>
                                    }
                                    @if (options.deliveries.length) {
                                        <p class="small text-muted">Newest first. Choose the delivery you are stocking from; you can enter the record on any day.</p>
                                    }
                                    @for (d of shownDeliveries; track d.id) {
                                        <div class="custom-control custom-radio mb-2 fpms-delivery-option">
                                            <input type="radio" class="custom-control-input" [id]="'d' + d.id" name="delivery" [value]="d.id" [(ngModel)]="deliveryId" [disabled]="!!cycleId" />
                                            <label class="custom-control-label" [for]="'d' + d.id">
                                                <strong>{{ d.hatcheryBatchNumber }}</strong> · {{ d.hatcheryName }} ({{ d.hatcheryShortCode }})
                                                · received {{ d.deliveryDate | farmDate }} <span class="badge" [class.badge-info]="true">{{ ago(d.daysSinceReceived) }}</span>
                                                <span class="d-block small">
                                                    {{ d.plAgeDays != null ? 'PL' + d.plAgeDays : 'PL age not recorded' }} · <strong>{{ d.plLeft | num: 0 }} PL left</strong> of {{ d.plReceived | num: 0 }}
                                                    · {{ d.plHealthCertificateNo ? 'PL health certificate ' + d.plHealthCertificateNo : 'no PL health certificate recorded' }}
                                                </span>
                                            </label>
                                        </div>
                                    }
                                </div>
                                <div class="card-footer">
                                    <button class="btn btn-primary" [disabled]="!delivery" (click)="go(1)">Next: ponds</button>
                                </div>
                            </div>
                        }
                        @case (1) {
                            <div class="card">
                                <div class="card-header d-flex align-items-center">
                                    <h3 class="card-title mr-auto"><i class="fas fa-water" aria-hidden="true"></i>Ponds and PL allocation</h3>
                                    <span [class.text-danger]="remaining < 0">Remaining: <strong>{{ remaining | num: 0 }}</strong> of {{ delivery.plLeft | num: 0 }} PL</span>
                                </div>
                                <div class="card-body p-0 table-responsive">
                                    @if (!allocations.length) {
                                        <fpms-empty text="No pond is marked Prepared. Record its preparation and select Mark Prepared on the pond page." icon="fas fa-water">
                                            <a routerLink="/app/ponds" class="btn btn-sm btn-primary">Go to ponds</a>
                                        </fpms-empty>
                                    } @else {
                                        <table class="table mb-0">
                                            <thead><tr><th></th><th>Pond</th><th class="text-right">Area (m²)</th><th>Farming system</th><th>Prepared</th><th style="width: 12rem">Stocking count (PL)</th><th class="text-right">Density</th></tr></thead>
                                            <tbody>
                                                @for (a of allocations; track a.pond.id) {
                                                    <tr>
                                                        <td><input type="checkbox" [(ngModel)]="a.selected" [attr.aria-label]="'Stock ' + a.pond.pondCode" /></td>
                                                        <td>{{ a.pond.pondCode }} <span class="small text-muted">{{ a.pond.name }}</span></td>
                                                        <td class="text-right">{{ a.pond.areaM2 | num: 0 }}</td>
                                                        <td>{{ a.pond.farmingSystem | label: L.FarmingSystems }}</td>
                                                        <td class="small">{{ a.pond.preparedAtUtc | farmDateTime }}</td>
                                                        <td><input type="number" min="1" step="1" class="form-control form-control-sm" [(ngModel)]="a.plCount" [disabled]="!a.selected" [attr.aria-label]="'PL for ' + a.pond.pondCode" /></td>
                                                        <td class="text-right">
                                                            @if (a.selected && a.plCount) {
                                                                <span [class.text-warning-dark]="densityState(a) !== 'within'" [class.font-weight-bold]="densityState(a) !== 'within'">{{ a.plCount / a.pond.areaM2 | num: 1 }} PL/m²</span>
                                                                @if (densityState(a) !== 'within') {
                                                                    <small class="d-block text-warning-dark"><i class="fas fa-exclamation-triangle mr-1"></i>{{ densityState(a) }} {{ bandText(a.pond) }}; you can still save</small>
                                                                }
                                                            } @else {
                                                                <span class="text-muted">—</span>
                                                                <small class="d-block text-muted">{{ bandText(a.pond) }}</small>
                                                            }
                                                        </td>
                                                    </tr>
                                                }
                                            </tbody>
                                        </table>
                                    }
                                </div>
                                @if (options.blockedPonds.length) {
                                    <div class="card-body border-top small">
                                        <strong>Prepared but not available:</strong>
                                        <ul class="mb-0">
                                            @for (p of options.blockedPonds; track p.id) {
                                                <li>{{ p.pondCode }}: {{ p.blockedReason }}</li>
                                            }
                                        </ul>
                                    </div>
                                }
                                <div class="card-footer">
                                    <button class="btn btn-default mr-2" (click)="go(0)">Back</button>
                                    <button class="btn btn-primary" [disabled]="!selected.length || remaining < 0 || selected.some(badCount)" (click)="go(2)">Next: batch details</button>
                                    @if (remaining < 0) { <span class="text-danger small ml-2">The allocations exceed the PL left in this delivery.</span> }
                                </div>
                            </div>
                        }
                        @case (2) {
                            @for (a of selected; track a.pond.id; let first = $first) {
                                <div class="card">
                                    <div class="card-header d-flex align-items-center">
                                        <h3 class="card-title mr-auto"><i class="fas fa-water" aria-hidden="true"></i>{{ a.pond.pondCode }} · {{ a.plCount | num: 0 }} PL · {{ a.plCount / a.pond.areaM2 | num: 1 }} PL/m²</h3>
                                        <span class="small text-muted">{{ a.pond.areaM2 | num: 0 }} m² · avg depth {{ a.pond.averageDepthM | num: 2 }} m · {{ a.pond.farmingSystem | label: L.FarmingSystems }}</span>
                                        @if (!first) {
                                            <button class="btn btn-xs btn-outline-secondary ml-2" (click)="copyFromFirst(a)">Copy from {{ selected[0].pond.pondCode }}</button>
                                        }
                                    </div>
                                    <div class="card-body">
                                        <div class="form-row">
                                            <div class="form-group col-md-4">
                                                <label [for]="'sa' + a.pond.id">Stocking date and time <span class="text-danger">*</span></label>
                                                <input [id]="'sa' + a.pond.id" type="datetime-local" class="form-control" [(ngModel)]="a.stockedAt" [min]="windowMin" [max]="windowMax" required />
                                                <small class="form-text" [class.text-danger]="!inWindow(a)" [class.text-muted]="inWindow(a)">
                                                    From {{ delivery.deliveryDate | farmDate }} (received) to now.
                                                </small>
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label [for]="'tm' + a.pond.id">Transport mode <span class="text-danger">*</span></label>
                                                <select [id]="'tm' + a.pond.id" class="form-control" [(ngModel)]="a.transportMode">
                                                    @for (o of Options.transportMode; track o.value) {
                                                        <option [ngValue]="o.value">{{ o.label }}</option>
                                                    }
                                                </select>
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label [for]="'tp' + a.pond.id">Time processed (packed at the hatchery)</label>
                                                <input [id]="'tp' + a.pond.id" type="datetime-local" class="form-control" [(ngModel)]="a.timeProcessed" [max]="a.stockedAt" />
                                            </div>
                                        </div>
                                        <div class="form-row">
                                            <div class="form-group col-md-4">
                                                <label [for]="'ac' + a.pond.id">Acclimation (minutes)</label>
                                                <input [id]="'ac' + a.pond.id" type="number" min="0" max="600" class="form-control" [(ngModel)]="a.acclimationMinutes" />
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label [for]="'is' + a.pond.id">Initial survival estimate (%)</label>
                                                <input [id]="'is' + a.pond.id" type="number" min="0" max="100" step="0.1" class="form-control" [(ngModel)]="a.initialSurvivalEstimatePct" />
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label [for]="'da' + a.pond.id">Dead at stocking (PL)</label>
                                                <input [id]="'da' + a.pond.id" type="number" min="0" step="1" class="form-control" [(ngModel)]="a.deadAtStocking" />
                                                <small class="form-text text-muted">Recorded separately; not taken off the stocking count.</small>
                                                @if (a.deadAtStocking != null && a.plCount && a.deadAtStocking > a.plCount) {
                                                    <small class="form-text text-warning-dark">More than the stocking count. Check both numbers.</small>
                                                }
                                            </div>
                                        </div>
                                        <h4 class="h6 mt-2">Pre-stocking water quality</h4>
                                        <div class="form-row">
                                            @for (p of stockingParams; track p.code) {
                                                <div class="form-group col-6 col-md-3">
                                                    <label [for]="'wq' + p.code + a.pond.id">{{ p.name }} <span class="text-danger">*</span></label>
                                                    <div class="input-group">
                                                        <input [id]="'wq' + p.code + a.pond.id" type="number" step="any" class="form-control" [(ngModel)]="a.readings[p.code]" required [min]="p.physicalMin" [max]="p.physicalMax" />
                                                        <div class="input-group-append"><span class="input-group-text">{{ p.unitCode }}</span></div>
                                                    </div>
                                                    <small class="form-text" [class.text-muted]="state(p, a) === 'ok' || state(p, a) === 'empty'" [class.text-warning-dark]="state(p, a) === 'out'" [class.text-danger]="state(p, a) === 'invalid'">
                                                        @switch (state(p, a)) {
                                                            @case ('out') { <i class="fas fa-exclamation-triangle mr-1"></i>Outside {{ p.optimalRange }}; saved and flagged. {{ advice(p, a) }} }
                                                            @case ('invalid') { Not physically possible ({{ p.physicalMin }}–{{ p.physicalMax }}). }
                                                            @default { Optimal {{ p.optimalRange }} }
                                                        }
                                                    </small>
                                                </div>
                                            }
                                        </div>
                                    </div>
                                </div>
                            }
                            <div class="d-flex mb-3">
                                <button class="btn btn-default mr-2" (click)="go(1)">Back</button>
                                <button class="btn btn-primary" [disabled]="!detailsValid()" (click)="go(3)">Next: review</button>
                                @if (!detailsValid()) { <span class="small text-muted ml-2 align-self-center">Enter a stocking time within the delivery's window and DO, temperature, pH and salinity for every pond.</span> }
                            </div>
                        }
                        @case (3) {
                            <div class="card">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-clipboard-check" aria-hidden="true"></i>Review</h3></div>
                                <div class="card-body">
                                    <div class="alert alert-light border mb-3">
                                        <div>
                                            Fry delivery <strong>{{ delivery.hatcheryBatchNumber }}</strong> from {{ delivery.hatcheryName }},
                                            received <strong>{{ delivery.deliveryDate | farmDate }}</strong> ({{ ago(delivery.daysSinceReceived) }}),
                                            {{ delivery.plAgeDays != null ? 'PL' + delivery.plAgeDays : 'PL age not recorded' }}.
                                        </div>
                                        <div class="custom-control custom-checkbox mt-2">
                                            <input type="checkbox" class="custom-control-input" id="confirmDelivery" [(ngModel)]="confirmDelivery" />
                                            <label class="custom-control-label" for="confirmDelivery">These are the fry from this delivery.</label>
                                        </div>
                                    </div>
                                    <p class="mb-2">
                                        @if (cycleCode) { Adds batches to cycle <strong>{{ cycleCode }}</strong>. } @else { Starts a new production cycle (Active); its code is generated when you create it. }
                                    </p>
                                    <div class="table-responsive">
                                    <table class="table table-sm">
                                        <thead><tr><th>Pond</th><th>Stocked</th><th class="text-right">PL</th><th class="text-right">PL/m²</th><th class="text-right">Dead</th><th>Transport</th><th>DO / Temp / pH / Sal</th></tr></thead>
                                        <tbody>
                                            @for (a of selected; track a.pond.id) {
                                                <tr>
                                                    <td>{{ a.pond.pondCode }}</td>
                                                    <td class="text-nowrap">{{ a.stockedAt.replace('T', ' ') }}</td>
                                                    <td class="text-right">{{ a.plCount | num: 0 }}</td>
                                                    <td class="text-right" [class.text-warning-dark]="densityState(a) !== 'within'">{{ a.plCount / a.pond.areaM2 | num: 1 }}{{ densityState(a) !== 'within' ? ' ⚠' : '' }}</td>
                                                    <td class="text-right">{{ a.deadAtStocking != null ? (a.deadAtStocking | num: 0) : '—' }}</td>
                                                    <td>{{ a.transportMode | label: L.TransportModes }}</td>
                                                    <td class="small">{{ readingsLine(a) }}</td>
                                                </tr>
                                            }
                                        </tbody>
                                        <tfoot>
                                            <tr><th colspan="2">Total allocated</th><th class="text-right">{{ allocated | num: 0 }}</th><th colspan="4">PL left after this: {{ remaining | num: 0 }}</th></tr>
                                        </tfoot>
                                    </table>
                                    </div>
                                </div>
                                <div class="card-footer">
                                    <button class="btn btn-default mr-2" (click)="go(2)">Back</button>
                                    <button class="btn btn-primary btn-lg" [disabled]="busy() || !confirmDelivery" (click)="create()">
                                        @if (busy()) { <i class="fas fa-spinner fa-spin mr-1"></i> }
                                        Create {{ selected.length === 1 ? 'batch' : selected.length + ' batches' }}
                                    </button>
                                </div>
                            </div>
                        }
                        @case (4) {
                            <div class="card card-outline card-success">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-check-circle" aria-hidden="true"></i>Created</h3></div>
                                <div class="card-body">
                                    <p>
                                        Cycle <a [routerLink]="['/app/production/cycles', result.cycleId]"><strong>{{ result.cycleCode }}</strong></a>
                                        {{ result.cycleCreated ? 'started (Active)' : 'updated' }}. PL left in the delivery: {{ result.plLeft | num: 0 }}.
                                    </p>
                                    <ul>
                                        @for (b of result.batches; track b.id) {
                                            <li><a [routerLink]="['/app/production/batches', b.id]">{{ b.batchCode }}</a> in {{ b.pondCode }} · {{ b.stockingDensityPerM2 | num: 1 }} PL/m²</li>
                                        }
                                    </ul>
                                    <fpms-warnings [warnings]="result.warnings"></fpms-warnings>
                                </div>
                            </div>
                        }
                    }
                }
                </div>
            </div>
        </div>
    `,
})
export class CreateBatchComponent extends FpmsPage implements OnInit {
    readonly stepNames = ['Fry source', 'Ponds', 'Batch details', 'Review', 'Create'];
    readonly nowInput = FarmTime.nowInput();
    step = 0;
    options: any = null;
    cycleId: string | null = null;

    get closeLink(): any[] {
        return this.cycleId ? ['/app/production/cycles', this.cycleId] : ['/app/production/batches'];
    }
    cycleCode: string | null = null;
    deliveryId: string | null = null;
    allocations: Allocation[] = [];
    stockingParams: any[] = [];
    result: any = null;
    confirmDelivery = false;
    private clientRequestId = newGuid();

    get shownDeliveries(): any[] {
        return this.options?.deliveries ?? [];
    }

    get windowMin(): string {
        return this.delivery ? `${this.delivery.deliveryDate}T00:00` : '';
    }

    get windowMax(): string {
        return this.nowInput;
    }

    inWindow(a: Allocation): boolean {
        return !!a.stockedAt && a.stockedAt >= this.windowMin && a.stockedAt <= this.windowMax;
    }

    ago(days: number): string {
        return days === 0 ? 'today' : days === 1 ? '1 day ago' : `${days} days ago`;
    }

    densityState(a: Allocation): 'within' | 'below' | 'above' {
        const d = (Number(a.plCount) || 0) / a.pond.areaM2;
        if (a.pond.bandMinPlM2 != null && d < a.pond.bandMinPlM2) {
            return 'below';
        }
        if (a.pond.bandMaxPlM2 != null && d > a.pond.bandMaxPlM2) {
            return 'above';
        }
        return 'within';
    }

    bandText(p: any): string {
        if (p.bandMinPlM2 == null && p.bandMaxPlM2 == null) {
            return 'no band';
        }
        return `band ${p.bandMinPlM2 ?? 0}–${p.bandMaxPlM2 ?? '∞'} PL/m² (${p.bandSource ?? 'standard'})`;
    }

    get delivery(): any {
        return this.options?.deliveries.find((d: any) => d.id === this.deliveryId);
    }

    get selected(): Allocation[] {
        return this.allocations.filter((a) => a.selected);
    }

    get allocated(): number {
        return this.selected.reduce((sum, a) => sum + (Number(a.plCount) || 0), 0);
    }

    get remaining(): number {
        return (this.delivery?.plLeft ?? 0) - this.allocated;
    }

    readonly badCount = (a: Allocation): boolean => !a.plCount || a.plCount < 1 || !Number.isInteger(Number(a.plCount));

    private wizard = new WizardSteps(0, () => this.furthestStep(), (n) => this.showStep(n));
    private started = false;

    ngOnInit(): void {
        this.route.queryParamMap.subscribe(async (q) => {
            if (!this.started || q.get('cycleId') !== this.cycleId) {
                this.started = true;
                await this.init();
            }
            this.wizard.sync(q);
        });
    }

    private furthestStep(): number {
        if (this.result) {
            return 4;
        }
        if (!this.options || !this.delivery) {
            return 0;
        }
        if (!this.selected.length || this.remaining < 0 || this.selected.some(this.badCount)) {
            return 1;
        }
        return this.detailsValid() ? 3 : 2;
    }

    private showStep(step: number): void {
        if (this.result && step < 4) {
            this.init().then(() => this.wizard.sync());
            return;
        }
        this.step = step;
        window.scrollTo({ top: 0 });
    }

    async init(): Promise<void> {
        this.cycleId = this.query('cycleId');
        this.step = 0;
        this.wizard.reset();
        this.result = null;
        this.clientRequestId = newGuid();
        const [options, params] = await Promise.all([
            this.api.get<any>('Cycle', 'GetStockingOptions', { cycleId: this.cycleId }),
            this.api.get<any[]>('Log', 'GetParameters'),
        ]);
        this.stockingParams = params.filter((p) => p.requiredAtStocking && p.isActive);
        this.cycleCode = options.cycleCode;
        this.deliveryId = options.deliveries.length === 1 ? options.deliveries[0].id : null;
        this.confirmDelivery = false;
        this.allocations = options.ponds.map((pond: any) => ({
            pond,
            selected: false,
            plCount: null,
            stockedAt: FarmTime.nowInput(),
            transportMode: 1,
            timeProcessed: '',
            acclimationMinutes: null,
            initialSurvivalEstimatePct: null,
            deadAtStocking: null,
            readings: Object.fromEntries(this.stockingParams.map((p) => [p.code, null])),
        }));
        this.options = options;
    }

    go(step: number): void {
        if (step === 2) {
            for (const a of this.selected) {
                if (!this.inWindow(a)) {
                    a.stockedAt = this.windowMax;
                }
            }
        }
        this.wizard.go(step, step === 4);
    }

    state(p: any, a: Allocation): 'empty' | 'ok' | 'out' | 'invalid' {
        return optimalState(p, a.readings[p.code]);
    }

    advice(p: any, a: Allocation): string {
        const v = Number(a.readings[p.code]);
        return (p.optimalMin != null && v < p.optimalMin ? p.lowRecommendation : p.highRecommendation) ?? '';
    }

    detailsValid(): boolean {
        return this.selected.every(
            (a) =>
                !!a.stockedAt &&
                this.inWindow(a) &&
                this.stockingParams.every((p) => ['ok', 'out'].includes(this.state(p, a))) &&
                (!a.timeProcessed || a.timeProcessed <= a.stockedAt) &&
                (a.deadAtStocking == null || a.deadAtStocking >= 0)
        );
    }

    copyFromFirst(a: Allocation): void {
        const f = this.selected[0];
        a.stockedAt = f.stockedAt;
        a.transportMode = f.transportMode;
        a.timeProcessed = f.timeProcessed;
        a.acclimationMinutes = f.acclimationMinutes;
        a.initialSurvivalEstimatePct = f.initialSurvivalEstimatePct;
        a.deadAtStocking = f.deadAtStocking;
        a.readings = { ...f.readings };
    }

    readingsLine(a: Allocation): string {
        return this.stockingParams.map((p) => `${a.readings[p.code]}${this.state(p, a) === 'out' ? ' ⚠' : ''}`).join(' / ');
    }

    async create(): Promise<void> {
        const body = {
            fryDeliveryId: this.cycleId ? null : this.deliveryId,
            cycleId: this.cycleId,
            clientRequestId: this.clientRequestId,
            batches: this.selected.map((a) => ({
                pondId: a.pond.id,
                plCount: a.plCount,
                stockedAt: FarmTime.fromLocalInput(a.stockedAt),
                transportMode: a.transportMode,
                timeProcessed: a.timeProcessed ? FarmTime.fromLocalInput(a.timeProcessed) : null,
                acclimationMinutes: a.acclimationMinutes,
                initialSurvivalEstimatePct: a.initialSurvivalEstimatePct,
                deadAtStocking: a.deadAtStocking,
                readings: this.stockingParams.map((p) => ({ parameterCode: p.code, value: a.readings[p.code] })),
            })),
        };
        let result: any;
        if (await this.act(async () => (result = await this.api.post<any>('Cycle', 'CreateBatches', body)))) {
            this.result = result;
            this.notifySaved(result.cycleCreated ? `Cycle ${result.cycleCode} started.` : `Batches added to ${result.cycleCode}.`);
            this.go(4);
        }
    }
}
