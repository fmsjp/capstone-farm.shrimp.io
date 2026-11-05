import { Component, OnInit } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header [title]="'Cycle ' + (c?.cycleCode ?? '')" [subtitle]="c ? (c.startDate ? 'Started ' + (c.startDate | farmDate) + '. ' : '') + 'Holds the batches stocked from fry delivery ' + c.hatcheryBatchNumber + '.' : ''" backLink="/app/production/cycles" backLabel="Production cycles">
            @if (c) {
                <fpms-badge [text]="c.status | label: L.CycleStatuses"></fpms-badge>
                @if (c.outcome) { <span class="badge ml-1" [class.badge-success]="c.outcome === 1" [class.badge-danger]="c.outcome === 2">{{ c.outcome | label: L.CycleOutcomes }}</span> }
                @if (c.canCreateBatch) {
                    <a class="btn btn-primary" routerLink="/app/production/create-batch" [queryParams]="{ cycleId: c.id }"><i class="fas fa-shrimp mr-1"></i>Add batch to this cycle</a>
                }
                @if (c.canClose) {
                    <button class="btn btn-outline-secondary" (click)="closing = { confirm: false, outcome: c.suggestedOutcome }">Close production cycle</button>
                }
                @if (c.canReopen) {
                    <button class="btn btn-outline-secondary" (click)="reopening = { reason: '' }">Reopen cycle</button>
                }
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (!c) {
                    <fpms-loading></fpms-loading>
                } @else {
                    @if (c.status === 4) {
                        <div class="alert alert-secondary"><i class="fas fa-lock mr-1"></i>This cycle was closed {{ c.closedAtUtc | farmDateTime }} by {{ c.closedBy }} as <strong>{{ c.outcome | label: L.CycleOutcomes }}</strong>. Its records are read-only; sales and payments can still be updated.</div>
                    }
                    @if (reopening) {
                        <div class="fpms-dialog-backdrop"><div class="fpms-dialog fpms-dialog-sm"><div class="fpms-dialog-body p-0"><div class="card card-outline card-warning mb-0 border-0">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-undo" aria-hidden="true"></i>Reopen cycle {{ c.cycleCode }}</h3></div>
                            <div class="card-body">
                                <p class="small">The cycle becomes Active again and its outcome is cleared. Its batches stay Closed; reopen a batch from its page to correct it. The reason is kept in the cycle's history.</p>
                                <label for="rr">Reason <span class="text-danger">*</span></label>
                                <input id="rr" class="form-control" [(ngModel)]="reopening.reason" maxlength="500" placeholder="e.g. a harvest weight was entered wrong" />
                            </div>
                            <div class="card-footer">
                                <button class="btn btn-warning mr-2" [disabled]="busy() || reopening.reason.trim().length < 3" (click)="reopen()">Reopen cycle</button>
                                <button class="btn btn-default" (click)="reopening = null">Cancel</button>
                            </div>
                        </div></div></div></div>
                    }
                    @if (closing) {
                        <div class="fpms-dialog-backdrop"><div class="fpms-dialog fpms-dialog-sm"><div class="fpms-dialog-body p-0"><div class="card card-outline card-warning mb-0 border-0">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-lock" aria-hidden="true"></i>Close production cycle {{ c.cycleCode }}</h3></div>
                            <div class="card-body">
                                <p>Every batch is closed. Closing the cycle makes all its records read-only. Sales and payment status can still be updated.</p>
                                <div class="form-group">
                                    <label class="d-block">Outcome <span class="text-danger">*</span></label>
                                    @for (o of Options.cycleOutcome; track o.value) {
                                        <div class="custom-control custom-radio custom-control-inline">
                                            <input type="radio" class="custom-control-input" [id]="'oc' + o.value" name="outcome" [value]="o.value" [(ngModel)]="closing.outcome" />
                                            <label class="custom-control-label" [for]="'oc' + o.value">{{ o.label }}</label>
                                        </div>
                                    }
                                    <small class="form-text text-muted">
                                        Pre-selected as {{ c.suggestedOutcome | label: L.CycleOutcomes }}{{ c.suggestedOutcome === 2 ? ' because every batch failed' : '' }}. Change it if that is not right.
                                    </small>
                                </div>
                                <div class="custom-control custom-checkbox">
                                    <input type="checkbox" class="custom-control-input" id="cc" [(ngModel)]="closing.confirm" />
                                    <label class="custom-control-label" for="cc">I understand every record of this cycle will become read-only.</label>
                                </div>
                            </div>
                            <div class="card-footer">
                                <button class="btn btn-warning mr-2" [disabled]="busy() || !closing.confirm || !closing.outcome" (click)="close()">Close cycle</button>
                                <button class="btn btn-default" (click)="closing = null">Cancel</button>
                            </div>
                        </div></div></div></div>
                    }
                    <div class="row">
                        <div class="col-lg-4">
                            <div class="card">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-truck" aria-hidden="true"></i>Fry delivery</h3></div>
                                <div class="card-body">
                                    <dl class="row mb-0 fpms-dl">
                                        <dt class="col-6">Hatchery</dt><dd class="col-6">{{ c.hatcheryName }} ({{ c.hatcheryShortCode }})</dd>
                                        <dt class="col-6">Batch number</dt>
                                        <dd class="col-6"><a [routerLink]="['/app/resources/fry-deliveries', c.fryDeliveryId]">{{ c.hatcheryBatchNumber }}</a></dd>
                                        <dt class="col-6">Delivered</dt><dd class="col-6">{{ c.deliveryDate | farmDate }}</dd>
                                        <dt class="col-6">PL age</dt><dd class="col-6">{{ c.plAgeDays != null ? 'PL' + c.plAgeDays : '—' }}</dd>
                                        <dt class="col-6">PL received</dt><dd class="col-6">{{ c.plReceived | num: 0 }}</dd>
                                        <dt class="col-6">PL stocked</dt><dd class="col-6">{{ c.plStocked | num: 0 }}</dd>
                                        @if (c.isStockingFinished) {
                                            <dt class="col-6">PL not stocked</dt><dd class="col-6">{{ c.plNotStocked | num: 0 }} <span class="small text-muted">(stocking finished)</span></dd>
                                        } @else {
                                            <dt class="col-6">PL left</dt><dd class="col-6">{{ c.plLeft | num: 0 }}</dd>
                                        }
                                        <dt class="col-6">Started</dt><dd class="col-6">{{ c.startDate | farmDate }}</dd>
                                    </dl>
                                </div>
                            </div>
                            <div class="card">
                                <div class="card-body">
                                    <div class="fpms-kpi mb-2"><div class="v">{{ c.totalFeedKg | num: 1 }} kg</div><div class="l">Feed to date (all batches)</div></div>
                                    <div class="fpms-kpi"><div class="v">{{ c.totalHarvestKg | num: 1 }} kg</div><div class="l">Harvested (all batches)</div></div>
                                </div>
                            </div>
                        </div>
                        <div class="col-lg-8">
                            <div class="card">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-layer-group" aria-hidden="true"></i>Batches</h3></div>
                                <div class="card-body p-0 table-responsive">
                                    <table class="table table-hover mb-0">
                                        <thead>
                                            <tr><th>Batch</th><th>Pond</th><th>Status</th><th>Stocked</th><th class="text-right">PL</th><th class="text-right">DOC</th><th class="text-right">ABW (g)</th><th class="text-right">FCR</th><th class="text-right">Survival</th></tr>
                                        </thead>
                                        <tbody>
                                            @for (b of c.batches; track b.id) {
                                                <tr>
                                                    <td><a [routerLink]="['/app/production/batches', b.id]">{{ b.batchCode }}</a></td>
                                                    <td>{{ b.pondCode }}</td>
                                                    <td><fpms-badge [text]="b.status | label: L.BatchStatuses"></fpms-badge></td>
                                                    <td class="text-nowrap">{{ b.stockingDate | farmDate }}</td>
                                                    <td class="text-right">{{ b.plCountStocked | num: 0 }}</td>
                                                    <td class="text-right">{{ b.doc ?? '—' }}</td>
                                                    <td class="text-right">{{ b.currentAbwG | num: 2 }}</td>
                                                    <td class="text-right">{{ b.fcrToDate | num: 2 }}</td>
                                                    <td class="text-right">{{ b.estimatedSurvivalPct | num: 1 }}%</td>
                                                </tr>
                                            }
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                            @if (c.status === 2 && c.openBatchCount > 0 && can('Farm.Production.Manage')) {
                                <p class="small text-muted">To close the cycle, close every batch first (each batch must be Harvested or Failed).</p>
                            }
                            @if (c.reopenings?.length) {
                                <div class="card">
                                    <div class="card-header"><h3 class="card-title"><i class="fas fa-history" aria-hidden="true"></i>Reopened</h3></div>
                                    <ul class="list-group list-group-flush small">
                                        @for (r of c.reopenings; track $index) {
                                            <li class="list-group-item py-2">{{ r.reopenedAtUtc | farmDateTime }} by {{ r.reopenedBy }}: {{ r.reason }}</li>
                                        }
                                    </ul>
                                </div>
                            }
                        </div>
                    </div>
                }
            </div>
        </section>
    `,
})
export class CycleDetailComponent extends FpmsPage implements OnInit {
    c: any = null;
    closing: any = null;
    reopening: { reason: string } | null = null;

    ngOnInit(): void {
        this.route.paramMap.subscribe(() => this.load());
    }

    async load(): Promise<void> {
        this.closing = null;
        this.reopening = null;
        this.c = await this.api.get<any>('Cycle', 'Get', { id: this.param('id') });
        this.setPageTitle(this.c ? `Cycle ${this.c.cycleCode}` : null);
    }

    async close(): Promise<void> {
        if (await this.act(() => this.api.post('Cycle', 'Close', { id: this.c.id, outcome: this.closing.outcome, rowVersion: this.c.rowVersion }))) {
            this.notifySaved('Production cycle closed.');
            this.load();
        }
    }

    async reopen(): Promise<void> {
        if (await this.act(() => this.api.post('Cycle', 'Reopen', { id: this.c.id, reason: this.reopening!.reason.trim(), rowVersion: this.c.rowVersion }))) {
            this.notifySaved('Cycle reopened.');
            this.load();
        }
    }
}
