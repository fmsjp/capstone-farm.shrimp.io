import { Component, OnInit } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { PagerState } from '@shared/fpms/pager';
import { FarmTime } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';

type ReportKey = 'cycle' | 'harvest' | 'sales' | 'wq' | 'inventory' | 'failure';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Reports" subtitle="Computed indicators are derived from source records. Owner only.">
            @if (r) {
                @if (can('Farm.Reports')) {
                    <button class="btn btn-outline-secondary mr-2" (click)="exportReport()" [disabled]="busy()"><i class="fas fa-file-csv mr-1"></i>Export CSV</button>
                }
                <button class="btn btn-primary" (click)="print()"><i class="fas fa-print mr-1"></i>Print</button>
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <div class="card no-print">
                    <div class="card-body pb-1">
                        <div class="mb-3" role="group" aria-label="Report">
                            @for (o of reports; track o.key) {
                                <button type="button" class="rp-chip" [attr.aria-pressed]="key === o.key" (click)="key = o.key; r = null">{{ o.label }}</button>
                            }
                        </div>
                        <div class="form-row align-items-end">
                            @if (key === 'cycle' || key === 'harvest' || key === 'failure') {
                                <div class="form-group col-md-2">
                                    <label for="cy">Cycle</label>
                                    <select id="cy" class="form-control" [(ngModel)]="f.cycleId" (ngModelChange)="f.batchId = null">
                                        @if (key !== 'cycle') { <option [ngValue]="null">{{ key === 'failure' ? '—' : 'All cycles' }}</option> }
                                        @for (c of cyclesFor(); track c.id) {
                                            <option [ngValue]="c.id">{{ c.cycleCode }} ({{ c.status | label: L.CycleStatuses }})</option>
                                        }
                                    </select>
                                </div>
                            }
                            @if (key === 'harvest' || key === 'failure' || key === 'wq') {
                                <div class="form-group col-md-2">
                                    <label for="bt">Batch</label>
                                    <select id="bt" class="form-control" [(ngModel)]="f.batchId">
                                        <option [ngValue]="null">{{ key === 'failure' ? '—' : 'All batches' }}</option>
                                        @for (b of batchesFor(); track b.id) {
                                            <option [ngValue]="b.id">{{ b.pondCode }} · {{ b.batchCode }}</option>
                                        }
                                    </select>
                                </div>
                            }
                            @if (key === 'sales') {
                                <div class="form-group col-md-3">
                                    <label for="by">Buyer</label>
                                    <select id="by" class="form-control" [(ngModel)]="f.buyerId">
                                        <option [ngValue]="null">All buyers</option>
                                        @for (b of buyers; track b.id) {
                                            <option [ngValue]="b.id">{{ b.name }}</option>
                                        }
                                    </select>
                                </div>
                            }
                            @if (key === 'inventory') {
                                <div class="form-group col-md-3">
                                    <label for="it">Item</label>
                                    <select id="it" class="form-control" [(ngModel)]="f.itemId">
                                        <option [ngValue]="null">All items</option>
                                        @for (i of items; track i.id) {
                                            <option [ngValue]="i.id">{{ i.displayName }}</option>
                                        }
                                    </select>
                                </div>
                            }
                            @if (key !== 'cycle' && key !== 'failure') {
                                <div class="form-group col-6 col-md-2">
                                    <label for="from">From</label>
                                    <input id="from" type="date" class="form-control" [(ngModel)]="f.from" />
                                </div>
                                <div class="form-group col-6 col-md-2">
                                    <label for="to">To</label>
                                    <input id="to" type="date" class="form-control" [(ngModel)]="f.to" [min]="f.from" />
                                </div>
                            }
                            <div class="form-group col-md-1">
                                <button class="btn btn-primary btn-block" (click)="runReport()" [disabled]="busy() || !canRun()">Run</button>
                            </div>
                        </div>
                        @if (key !== 'cycle' && key !== 'failure') {
                            <p class="small text-muted mb-2">
                                @if (rangeTooLong()) { <span class="text-danger"><i class="fas fa-exclamation-circle mr-1"></i>Choose a range of 24 months or less.</span> }
                                @else { Date range is limited to 24 months{{ key === 'harvest' || key === 'wq' ? ' unless you choose one batch' : '' }}. }
                            </p>
                        }
                        @if (key === 'failure' && !f.cycleId && !f.batchId) {
                            <p class="small text-muted">Choose a failed cycle or a failed batch.</p>
                        }
                    </div>
                </div>

                @if (busy()) {
                    <fpms-loading></fpms-loading>
                }
                @if (r && !busy()) {
                    <div class="fpms-report">
                        @if (r.header) {
                            <div class="fpms-report-header">
                                <div>
                                    <h4 class="mb-0">{{ title() }}</h4>
                                    <div class="small">{{ r.header.farmName }} ({{ r.header.farmCode }}) · {{ r.header.farmAddress }}</div>
                                </div>
                                <div class="text-right small">
                                    <div>{{ r.header.scope }}</div>
                                    <div>Generated {{ r.header.generatedAtUtc | farmDateTime }} by {{ r.header.generatedBy }}</div>
                                    <div>Time zone {{ r.header.timeZone }}</div>
                                </div>
                            </div>
                        } @else {
                            <div class="fpms-report-header">
                                <h4 class="mb-0">{{ title() }}</h4>
                                <div class="text-right small">{{ session.tenant?.name }} · {{ f.from }} → {{ f.to }} · generated {{ generatedAt | farmDateTime }}</div>
                            </div>
                        }

                        @switch (shown) {
                            @case ('cycle') {
                                <div class="card">
                                    <div class="card-body">
                                        <div class="d-flex flex-wrap justify-content-between">
                                            <div>
                                                <strong>Cycle {{ r.cycleCode }}</strong> · <fpms-badge [text]="r.status | label: L.CycleStatuses"></fpms-badge>
                                                <span class="badge ml-1" [class.badge-success]="r.isFinal" [class.badge-warning]="!r.isFinal">{{ r.isFinal ? 'Final' : 'Provisional (cycle not closed)' }}</span>
                                                <div class="small">Planned {{ r.plannedStartDate | farmDate }} → {{ r.plannedEndDate | farmDate }} · Hatchery <span [title]="r.hatcheryName">{{ r.hatcheryShortCode || r.hatcheryName }}</span> · lot {{ r.hatcheryLotNumber || '—' }}</div>
                                                @if (r.objectives) { <div class="small">Objectives: {{ r.objectives }}</div> }

                                                @if (r.failureReason) { <div class="small text-danger">Failure: {{ r.failureReason }}</div> }
                                            </div>
                                        </div>
                                        <div class="row mt-2">
                                            <div class="col-6 col-md-2"><div class="fpms-kpi"><div class="v">{{ r.totalPlStocked | num: 0 }}</div><div class="l">PL stocked</div></div></div>
                                            <div class="col-6 col-md-2"><div class="fpms-kpi"><div class="v">{{ r.totalFeedKg | num: 1 }}</div><div class="l">Feed (kg)</div></div></div>
                                            <div class="col-6 col-md-2"><div class="fpms-kpi"><div class="v">{{ r.totalHarvestKg | num: 1 }}</div><div class="l">Harvest (kg)</div></div></div>
                                            <div class="col-6 col-md-2"><div class="fpms-kpi"><div class="v">{{ r.cycleFcr | num: 2 }}</div><div class="l">Cycle FCR</div></div></div>
                                            <div class="col-6 col-md-2"><div class="fpms-kpi"><div class="v">{{ r.cycleSurvivalPct | num: 1 }}%</div><div class="l">Survival</div></div></div>
                                            <div class="col-6 col-md-2"><div class="fpms-kpi"><div class="v">{{ r.targetAchievementPct | num: 0 }}%</div><div class="l">Of target {{ r.targetProductionKg | num: 0 }} kg</div></div></div>
                                        </div>
                                        <h6 class="mt-2">Financial performance</h6>
                                        <ng-container *ngTemplateOutlet="financeTable; context: { $implicit: r.finance }"></ng-container>
                                    </div>
                                </div>
                                @for (b of r.batches; track b.batchId) {
                                    <div class="card fpms-break">
                                        <div class="card-header">
                                            <h3 class="card-title"><i class="fas fa-layer-group" aria-hidden="true"></i>Batch {{ b.batchCode }} · pond {{ b.pondCode }} ({{ b.pondAreaM2 | num: 0 }} m²) · <fpms-badge [text]="b.status | label: L.BatchStatuses"></fpms-badge></h3>
                                        </div>
                                        <div class="card-body">
                                            <h6>Stocking</h6>
                                            <p class="small">
                                                {{ b.stockingDate ? (b.stockingDate | farmDate) : 'Not stocked' }} · {{ b.plCountStocked | num: 0 }} PL · PL age {{ b.plAgeDays ?? '—' }} · lot {{ b.hatcheryLotNumber || '—' }} ·
                                                density {{ b.indicators?.stockingDensityPerM2 | num: 2 }} PL/m² · {{ b.hatcheryCertificationStatusAtStocking }}
                                                @if (b.failureReason) { <br /><span class="text-danger">Failure: {{ b.failureReason }}</span> }
                                            </p>
                                            <h6>Indicators <span class="small text-muted">({{ b.isFinal ? 'final, stored at closure' : 'live' }})</span></h6>
                                            <div class="table-responsive">
                                            <table class="table table-sm table-bordered">
                                                <tbody>
                                                    <tr>
                                                        <th>Culture days</th><td>{{ b.indicators?.cultureDays ?? b.indicators?.doc ?? '—' }}</td>
                                                        <th>ABW (g)</th><td>{{ (b.indicators?.finalAbwG ?? b.indicators?.currentAbwG) | num: 2 }}</td>
                                                        <th>Feed (kg)</th><td>{{ b.indicators?.cumulativeFeedKg | num: 1 }}</td>
                                                        <th>Harvest (kg)</th><td>{{ b.indicators?.totalHarvestKg | num: 1 }}</td>
                                                    </tr>
                                                    <tr>
                                                        <th>{{ b.isFinal ? 'Final FCR' : 'FCR to date' }}</th><td>{{ (b.isFinal ? b.indicators?.finalFcr : b.indicators?.fcrToDate) | num: 2 }}</td>
                                                        <th>{{ b.isFinal ? 'Survival' : 'Est. survival' }}</th><td>{{ (b.isFinal ? b.indicators?.survivalRatePct : b.indicators?.estimatedSurvivalPct) | num: 1 }}%</td>
                                                        <th>Mortality</th><td>{{ b.indicators?.cumulativeMortality | num: 0 }}</td>
                                                        <th>Pieces harvested</th><td>{{ b.indicators?.harvestedPieces | num: 0 }}</td>
                                                    </tr>
                                                </tbody>
                                            </table>
                                            </div>
                                            <div class="row">
                                                <div class="col-lg-6">
                                                    <h6>Feed consumption</h6>
                                                    <div class="table-responsive">
                                                    <table class="table table-sm">
                                                        <thead><tr><th>Feed</th><th>BAI no.</th><th class="text-right">Kg</th><th class="text-right">Cost</th></tr></thead>
                                                        <tbody>
                                                            @for (x of b.feed; track $index) {
                                                                <tr><td>{{ x.itemName }}</td><td>{{ x.baiRegistrationNo }}</td><td class="text-right">{{ x.quantityKg | num: 1 }}</td><td class="text-right">{{ x.cost | php }}</td></tr>
                                                            } @empty {
                                                                <tr><td colspan="4" class="text-muted">No feeding logs.</td></tr>
                                                            }
                                                        </tbody>
                                                    </table>
                                                    </div>
                                                    <h6>Growth</h6>
                                                    <div class="table-responsive">
                                                    <table class="table table-sm">
                                                        <thead><tr><th>Date</th><th>DOC</th><th class="text-right">Sample</th><th class="text-right">ABW (g)</th><th class="text-right">g/day</th></tr></thead>
                                                        <tbody>
                                                            @for (g of b.growth; track $index) {
                                                                <tr><td>{{ g.date | farmDate }}</td><td>{{ g.doc }}</td><td class="text-right">{{ g.sampleCount }}</td><td class="text-right">{{ g.abwG | num: 2 }}</td><td class="text-right">{{ g.growthRateGPerDay | num: 2 }}</td></tr>
                                                            } @empty {
                                                                <tr><td colspan="5" class="text-muted">No growth samples.</td></tr>
                                                            }
                                                        </tbody>
                                                    </table>
                                                    </div>
                                                </div>
                                                <div class="col-lg-6">
                                                    <h6>Water quality (weekly)</h6>
                                                    <div class="table-responsive">
                                                    <table class="table table-sm">
                                                        <thead><tr><th>Week of</th><th>Parameter</th><th class="text-right">Avg</th><th class="text-right">Min–max</th><th class="text-right">Out of range</th></tr></thead>
                                                        <tbody>
                                                            @for (w of b.waterQuality; track $index) {
                                                                <tr>
                                                                    <td>{{ w.weekStart | farmDate }}</td><td>{{ w.parameterName }}</td>
                                                                    <td class="text-right">{{ w.average | num: 2 }} {{ w.unitCode }}</td>
                                                                    <td class="text-right">{{ w.min | num: 2 }}–{{ w.max | num: 2 }}</td>
                                                                    <td class="text-right" [class.text-danger]="w.outOfRangeCount > 0">{{ w.outOfRangeCount }} / {{ w.readings }}</td>
                                                                </tr>
                                                            } @empty {
                                                                <tr><td colspan="5" class="text-muted">No water-quality readings.</td></tr>
                                                            }
                                                        </tbody>
                                                    </table>
                                                    </div>
                                                </div>
                                            </div>
                                            <h6>Harvests</h6>
                                            <ng-container *ngTemplateOutlet="harvestTable; context: { $implicit: b.harvests }"></ng-container>
                                            <h6>Financial performance</h6>
                                            <ng-container *ngTemplateOutlet="financeTable; context: { $implicit: b.finance }"></ng-container>
                                        </div>
                                    </div>
                                }
                            }
                            @case ('harvest') {
                                <div class="row">
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.totalKg | num: 1 }}</div><div class="l">Harvested kg</div></div></div>
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.totalPieces | num: 0 }}</div><div class="l">Pieces</div></div></div>
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.weightedAbwG | num: 2 }} g</div><div class="l">Weighted ABW</div></div></div>
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.salesAmount | php }}</div><div class="l">Sales ({{ r.soldKg | num: 1 }} kg sold)</div></div></div>
                                </div>
                                <div class="card"><div class="card-header"><h3 class="card-title"><i class="fas fa-layer-group" aria-hidden="true"></i>By batch</h3></div>
                                    <div class="card-body p-0">
                                        <div class="table-responsive">
                                        <table class="table table-sm mb-0">
                                            <thead><tr>
                                                <th fpmsSort="batchCode" [pager]="ps('hb')">Batch</th><th fpmsSort="pondCode" [pager]="ps('hb')">Pond</th><th fpmsSort="cycleCode" [pager]="ps('hb')">Cycle</th>
                                                <th class="text-right" fpmsSort="totalKg" [pager]="ps('hb')">Kg</th><th class="text-right" fpmsSort="pieces" [pager]="ps('hb')">Pieces</th>
                                                <th class="text-right" fpmsSort="weightedAbwG" [pager]="ps('hb')">Weighted ABW</th><th class="text-right" fpmsSort="survivalRatePct" [pager]="ps('hb')">Survival</th>
                                            </tr></thead>
                                            <tbody>
                                                @for (b of view('hb', r.batches); track b.batchId) {
                                                    <tr><td>{{ b.batchCode }}</td><td>{{ b.pondCode }}</td><td>{{ b.cycleCode }}</td><td class="text-right">{{ b.totalKg | num: 1 }}</td><td class="text-right">{{ b.pieces | num: 0 }}</td><td class="text-right">{{ b.weightedAbwG | num: 2 }}</td><td class="text-right">{{ b.survivalRatePct | num: 1 }}%</td></tr>
                                                }
                                            </tbody>
                                        </table>
                                        </div>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col-lg-6">
                                        <div class="card"><div class="card-header"><h3 class="card-title"><i class="fas fa-ruler-horizontal" aria-hidden="true"></i>Size distribution</h3></div>
                                            <div class="card-body p-0">
                                                <div class="table-responsive">
                                                <table class="table table-sm mb-0">
                                                    <thead><tr><th>Size grade</th><th class="text-right">Kg</th><th class="text-right">Share</th></tr></thead>
                                                    <tbody>
                                                        @for (s of r.sizeDistribution; track $index) {
                                                            <tr><td>{{ s.sizeLabel }}</td><td class="text-right">{{ s.kg | num: 1 }}</td><td class="text-right">{{ s.pct | num: 1 }}%</td></tr>
                                                        } @empty {
                                                            <tr><td colspan="3" class="text-muted">No grading lines.</td></tr>
                                                        }
                                                    </tbody>
                                                </table>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                    <div class="col-lg-6">
                                        <div class="card"><div class="card-header"><h3 class="card-title"><i class="fas fa-handshake" aria-hidden="true"></i>Buyer allocation</h3></div>
                                            <div class="card-body p-0">
                                                <div class="table-responsive">
                                                <table class="table table-sm mb-0">
                                                    <thead><tr><th>Buyer</th><th class="text-right">Kg</th><th class="text-right">Amount</th><th class="text-right">Share</th></tr></thead>
                                                    <tbody>
                                                        @for (a of r.buyerAllocation; track $index) {
                                                            <tr><td>{{ a.buyerName }}</td><td class="text-right">{{ a.kg | num: 1 }}</td><td class="text-right">{{ a.amount | php }}</td><td class="text-right">{{ a.pct | num: 1 }}%</td></tr>
                                                        } @empty {
                                                            <tr><td colspan="4" class="text-muted">No sales of these harvests.</td></tr>
                                                        }
                                                    </tbody>
                                                </table>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                                <div class="card"><div class="card-header"><h3 class="card-title"><i class="fas fa-shrimp" aria-hidden="true"></i>Harvests</h3></div>
                                    <div class="card-body"><ng-container *ngTemplateOutlet="harvestTable; context: { $implicit: r.harvests }"></ng-container></div>
                                </div>
                            }
                            @case ('failure') {
                                <div class="card">
                                    <div class="card-body">
                                        @if (r.cycleCode) {
                                            <p class="mb-1"><strong>Cycle {{ r.cycleCode }}</strong> · Hatchery <span [title]="r.hatcheryName">{{ r.hatcheryShortCode || r.hatcheryName }}</span> ({{ r.hatcheryAccreditation }})</p>
                                        }
                                        @if (r.cycleFailureReason) {
                                            <p class="mb-1">Cycle failure on {{ r.cycleFailureDate | farmDate }}: {{ r.cycleFailureReason }} · estimated loss {{ r.cycleEstimatedLossPhp | php }}</p>
                                        }

                                    </div>
                                </div>
                                @for (b of r.batches; track $index) {
                                    <div class="card fpms-break">
                                        <div class="card-header"><h3 class="card-title"><i class="fas fa-layer-group" aria-hidden="true"></i>Batch {{ b.batchCode }} · pond {{ b.pondCode }} ({{ b.pondAreaM2 | num: 0 }} m²)</h3></div>
                                        <div class="card-body">
                                            <dl class="row mb-2 fpms-dl">
                                                <dt class="col-md-3">Stocked</dt><dd class="col-md-3">{{ b.stockingDate | farmDate }} · {{ b.plCountStocked | num: 0 }} PL · lot {{ b.hatcheryLotNumber || '—' }}</dd>
                                                <dt class="col-md-3">Hatchery certification</dt><dd class="col-md-3">{{ b.hatcheryCertificationStatusAtStocking }}</dd>
                                                <dt class="col-md-3">Failure date</dt><dd class="col-md-3">{{ b.failureDate | farmDate }} (DOC {{ b.docAtFailure ?? '—' }})</dd>
                                                <dt class="col-md-3">Estimated loss</dt><dd class="col-md-3">{{ b.estimatedLossPhp | php }}</dd>
                                                <dt class="col-md-3">Reason</dt><dd class="col-md-9">{{ b.failureReason }}</dd>
                                                <dt class="col-md-3">Est. population / biomass</dt><dd class="col-md-3">{{ b.indicators?.estimatedPopulation | num: 0 }} / {{ b.indicators?.estimatedBiomassKg | num: 1 }} kg</dd>
                                                <dt class="col-md-3">Inventory used (cost)</dt><dd class="col-md-3">{{ b.inventoryUsageCost | php }}</dd>
                                            </dl>
                                            <div class="row small">
                                                <div class="col-md-4"><h6>Mortality events</h6><ul class="pl-3">@for (x of b.mortalityEvents; track $index) { <li>{{ x }}</li> } @empty { <li class="text-muted">None recorded</li> }</ul></div>
                                                <div class="col-md-4"><h6>Treatments</h6><ul class="pl-3">@for (x of b.treatments; track $index) { <li>{{ x }}</li> } @empty { <li class="text-muted">None recorded</li> }</ul></div>
                                                <div class="col-md-4"><h6>Recent water quality</h6><ul class="pl-3">@for (x of b.recentWaterQuality; track $index) { <li>{{ x }}</li> } @empty { <li class="text-muted">None recorded</li> }</ul></div>
                                            </div>
                                        </div>
                                    </div>
                                } @empty {
                                    <fpms-empty text="No failed batches for this selection."></fpms-empty>
                                }
                                <p class="small text-center"><strong>{{ r.notice }}</strong></p>
                                <div class="row mt-4 small">
                                    <div class="col-6"><div class="fpms-sign">Prepared by</div></div>
                                    <div class="col-6"><div class="fpms-sign">Farm Owner</div></div>
                                </div>
                            }
                            @case ('sales') {
                                <div class="row">
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.totalAmount | php }}</div><div class="l">Sales ({{ r.saleCount }})</div></div></div>
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.totalKg | num: 1 }}</div><div class="l">Kg sold{{ r.averagePricePerKg != null ? ' · avg ' + (r.averagePricePerKg | php) + '/kg' : '' }}</div></div></div>
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.totalPaid | php }}</div><div class="l">Paid</div></div></div>
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v" [class.text-danger]="r.overdueCount > 0">{{ r.totalBalance | php }}</div><div class="l">Balance{{ r.overdueCount ? ' · ' + r.overdueCount + ' overdue (' + (r.overdueBalance | php) + ')' : '' }}</div></div></div>
                                </div>
                                <div class="card"><div class="card-header"><h3 class="card-title"><i class="fas fa-handshake" aria-hidden="true"></i>By buyer</h3></div>
                                    <div class="card-body p-0 table-responsive">
                                        <table class="table table-sm mb-0">
                                            <thead><tr>
                                                <th fpmsSort="buyerName" [pager]="ps('sb')">Buyer</th><th class="text-right" fpmsSort="saleCount" [pager]="ps('sb')">Sales</th><th class="text-right" fpmsSort="kg" [pager]="ps('sb')">Kg</th>
                                                <th class="text-right" fpmsSort="amount" [pager]="ps('sb')">Amount</th><th class="text-right" fpmsSort="paid" [pager]="ps('sb')">Paid</th><th class="text-right" fpmsSort="balance" [pager]="ps('sb')">Balance</th>
                                                <th class="text-right" fpmsSort="averagePricePerKg" [pager]="ps('sb')">Avg ₱/kg</th><th class="text-right" fpmsSort="overdueCount" [pager]="ps('sb')">Overdue</th>
                                            </tr></thead>
                                            <tbody>
                                                @for (b of view('sb', r.buyers); track b.buyerId) {
                                                    <tr>
                                                        <td>{{ b.buyerName }}</td><td class="text-right">{{ b.saleCount }}</td><td class="text-right">{{ b.kg | num: 2 }}</td>
                                                        <td class="text-right">{{ b.amount | php }}</td><td class="text-right">{{ b.paid | php }}</td><td class="text-right">{{ b.balance | php }}</td>
                                                        <td class="text-right">{{ b.averagePricePerKg != null ? (b.averagePricePerKg | php) : '—' }}</td><td class="text-right">{{ b.overdueCount || '—' }}</td>
                                                    </tr>
                                                } @empty {
                                                    <tr><td colspan="8" class="text-muted text-center py-3">No sales in this period.</td></tr>
                                                }
                                            </tbody>
                                        </table>
                                    </div>
                                </div>
                                <div class="card"><div class="card-header"><h3 class="card-title"><i class="fas fa-cash-register" aria-hidden="true"></i>Sales</h3></div>
                                    <div class="card-body p-0 table-responsive">
                                        <table class="table table-sm mb-0">
                                            <thead><tr>
                                                <th fpmsSort="dispatchReceiptNo" [pager]="ps('ss', 'saleDate desc')">Receipt</th><th fpmsSort="saleDate" [pager]="ps('ss', 'saleDate desc')">Date</th><th fpmsSort="buyerName" [pager]="ps('ss', 'saleDate desc')">Buyer</th>
                                                <th class="text-right" fpmsSort="kg" [pager]="ps('ss', 'saleDate desc')">Kg</th><th class="text-right" fpmsSort="amount" [pager]="ps('ss', 'saleDate desc')">Amount</th>
                                                <th class="text-right" fpmsSort="paid" [pager]="ps('ss', 'saleDate desc')">Paid</th><th class="text-right" fpmsSort="balance" [pager]="ps('ss', 'saleDate desc')">Balance</th>
                                                <th fpmsSort="dueDate" [pager]="ps('ss', 'saleDate desc')">Due</th><th>Payment</th>
                                            </tr></thead>
                                            <tbody>
                                                @for (x of view('ss', r.sales, 'saleDate desc'); track x.saleId) {
                                                    <tr>
                                                        <td class="text-nowrap"><a [routerLink]="['/app/sales', x.saleId]">{{ x.dispatchReceiptNo }}</a></td>
                                                        <td class="text-nowrap">{{ x.saleDate | farmDate }}</td><td>{{ x.buyerName }}</td>
                                                        <td class="text-right">{{ x.kg | num: 2 }}</td><td class="text-right">{{ x.amount | php }}</td><td class="text-right">{{ x.paid | php }}</td><td class="text-right">{{ x.balance | php }}</td>
                                                        <td class="text-nowrap">{{ x.dueDate ? (x.dueDate | farmDate) : '—' }}</td>
                                                        <td class="text-nowrap"><fpms-badge [text]="x.paymentStatus | label: L.PaymentStatuses"></fpms-badge>@if (x.isOverdue) { <span class="badge badge-danger ml-1">Overdue</span> }</td>
                                                    </tr>
                                                } @empty {
                                                    <tr><td colspan="9" class="text-muted text-center py-3">No sales in this period.</td></tr>
                                                }
                                            </tbody>
                                        </table>
                                    </div>
                                    <fpms-pager [state]="ps('ss')"></fpms-pager>
                                </div>
                            }
                            @case ('wq') {
                                <div class="row">
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v" [class.text-danger]="r.exceptionCount > 0">{{ r.exceptionCount }}</div><div class="l">Readings out of range (of {{ r.readingsChecked }})</div></div></div>
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.exceptionRatePct != null ? (r.exceptionRatePct | num: 1) + '%' : '—' }}</div><div class="l">Share out of range</div></div></div>
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.pondsAffected }} / {{ r.batchesAffected }}</div><div class="l">Ponds / batches affected</div></div></div>
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.mostFrequentParameter || '—' }}</div><div class="l">Most frequent parameter</div></div></div>
                                </div>
                                <div class="card">
                                    <div class="card-body p-0 table-responsive">
                                        <table class="table table-sm mb-0">
                                            <thead><tr>
                                                <th fpmsSort="observedAtUtc" [pager]="ps('wq', 'observedAtUtc desc')">Observed</th><th fpmsSort="pondCode" [pager]="ps('wq', 'observedAtUtc desc')">Pond / batch</th>
                                                <th fpmsSort="parameterName" [pager]="ps('wq', 'observedAtUtc desc')">Parameter</th><th class="text-right" fpmsSort="value" [pager]="ps('wq', 'observedAtUtc desc')">Reading</th><th>Optimal range</th><th>By</th>
                                            </tr></thead>
                                            <tbody>
                                                @for (x of view('wq', r.items, 'observedAtUtc desc'); track $index) {
                                                    <tr>
                                                        <td class="text-nowrap">{{ x.observedAtUtc | farmDateTime }}</td>
                                                        <td>{{ x.pondCode }} · {{ x.batchCode }}</td>
                                                        <td>{{ x.parameterName }}</td>
                                                        <td class="text-right text-danger font-weight-bold">{{ x.value }} {{ x.unitCode }}</td>
                                                        <td>{{ x.optimalRange }}</td>
                                                        <td class="small">{{ x.recordedBy }}</td>
                                                    </tr>
                                                } @empty {
                                                    <tr><td colspan="6" class="text-muted text-center py-3">No out-of-range readings in this range.</td></tr>
                                                }
                                            </tbody>
                                        </table>
                                    </div>
                                    <fpms-pager [state]="ps('wq')"></fpms-pager>
                                </div>
                            }
                            @case ('inventory') {
                                <div class="row">
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.feedUsedKg | num: 1 }} kg</div><div class="l">Feed used</div></div></div>
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.totalUsageCost | php }}</div><div class="l">Cost of everything used</div></div></div>
                                    <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ r.items.length }}</div><div class="l">Items with activity</div></div></div>
                                </div>
                                <div class="card"><div class="card-header"><h3 class="card-title"><i class="fas fa-boxes" aria-hidden="true"></i>Summary by item</h3></div>
                                    <div class="card-body p-0 table-responsive">
                                        <table class="table table-sm mb-0">
                                            <thead><tr>
                                                <th fpmsSort="itemName" [pager]="ps('ii')">Item</th><th>Unit</th><th class="text-right" fpmsSort="opening" [pager]="ps('ii')">Opening</th><th class="text-right" fpmsSort="receipts" [pager]="ps('ii')">Receipts</th>
                                                <th class="text-right" fpmsSort="usage" [pager]="ps('ii')">Usage</th><th class="text-right" fpmsSort="adjustments" [pager]="ps('ii')">Adjustments</th><th class="text-right" fpmsSort="closing" [pager]="ps('ii')">Closing</th><th class="text-right" fpmsSort="usageCost" [pager]="ps('ii')">Usage cost</th>
                                            </tr></thead>
                                            <tbody>
                                                @for (i of view('ii', r.items); track $index) {
                                                    <tr>
                                                        <td>{{ i.itemName }}</td><td>{{ i.unit }}</td>
                                                        <td class="text-right">{{ i.opening | num: 3 }}</td><td class="text-right">{{ i.receipts | num: 3 }}</td>
                                                        <td class="text-right">{{ i.usage | num: 3 }}</td><td class="text-right">{{ i.adjustments | num: 3 }}</td>
                                                        <td class="text-right font-weight-bold" [class.text-danger]="i.closing < 0">{{ i.closing | num: 3 }}</td>
                                                        <td class="text-right">{{ i.usageCost | php }}</td>
                                                    </tr>
                                                }
                                            </tbody>
                                        </table>
                                    </div>
                                </div>
                                <div class="card"><div class="card-header"><h3 class="card-title"><i class="fas fa-exchange-alt" aria-hidden="true"></i>Movements</h3></div>
                                    <div class="card-body p-0 table-responsive">
                                        <table class="table table-sm mb-0">
                                            <thead><tr>
                                                <th fpmsSort="movementDateUtc" [pager]="ps('im', 'movementDateUtc desc')">Date</th><th fpmsSort="itemName" [pager]="ps('im', 'movementDateUtc desc')">Item</th><th>Lot</th><th>Type</th>
                                                <th class="text-right" fpmsSort="quantity" [pager]="ps('im', 'movementDateUtc desc')">Qty</th><th class="text-right">Unit cost</th><th>Batch</th><th>Reason</th>
                                            </tr></thead>
                                            <tbody>
                                                @for (m of view('im', r.movements, 'movementDateUtc desc'); track $index) {
                                                    <tr>
                                                        <td class="text-nowrap">{{ m.movementDateUtc | farmDateTime }}</td><td>{{ m.itemName }}</td><td>{{ m.lotNumber }}</td>
                                                        <td>{{ m.type | label: L.MovementTypes }}</td><td class="text-right">{{ m.quantity | num: 3 }} {{ m.unit }}</td>
                                                        <td class="text-right">{{ m.unitCost | php }}</td><td>{{ m.batchCode }}</td><td class="small">{{ m.reason }}</td>
                                                    </tr>
                                                } @empty {
                                                    <tr><td colspan="8" class="text-muted text-center py-3">No movements in this range.</td></tr>
                                                }
                                            </tbody>
                                        </table>
                                    </div>
                                    <fpms-pager [state]="ps('im')"></fpms-pager>
                                </div>
                            }
                        }
                    </div>
                }

                <ng-template #financeTable let-x>
                    <div class="table-responsive">
                    <table class="table table-sm table-bordered w-auto">
                        <tbody>
                            <tr><th>Revenue (sales, buyer's kg when re-weighed)</th><td class="text-right">{{ x?.revenue | php }}</td></tr>
                            <tr><th>Feed and chemicals used (lot cost)</th><td class="text-right">{{ x?.inventoryUsageCost | php }}</td></tr>
                            <tr><th>PL (share of the delivery cost)</th><td class="text-right">{{ x?.plCost | php }}</td></tr>
                            <tr><th>Production cost</th><td class="text-right">{{ x?.productionCost | php }}</td></tr>
                            <tr class="font-weight-bold"><th>Margin over feed, chemicals and PL</th><td class="text-right" [class.text-danger]="x?.margin < 0">{{ x?.margin | php }}</td></tr>
                            <tr><td colspan="2" class="small text-muted">Labor, power and other costs are not recorded, so this is not a profit figure.</td></tr>
                        </tbody>
                    </table>
                    </div>
                </ng-template>
                <ng-template #harvestTable let-rows>
                    <div class="table-responsive">
                    <table class="table table-sm">
                        <thead><tr><th>Date</th><th>Batch</th><th>Type</th><th class="text-right">Kg</th><th class="text-right">Boxes</th><th class="text-right">ABW</th><th class="text-right">Pieces</th><th>Grades</th><th>Quality</th></tr></thead>
                        <tbody>
                            @for (h of rows; track h.harvestId) {
                                <tr>
                                    <td>{{ h.date | farmDate }}</td><td>{{ h.pondCode }} · {{ h.batchCode }}</td><td>{{ h.harvestType | label: L.HarvestTypes }}</td>
                                    <td class="text-right">{{ h.totalKg | num: 1 }}</td><td class="text-right">{{ h.boxes ?? '—' }}</td><td class="text-right">{{ h.abwG | num: 2 }}</td>
                                    <td class="text-right">{{ h.pieces | num: 0 }}</td>
                                    <td class="small">@for (g of h.grades; track $index) { <span class="mr-1">{{ g.sizeLabel }} {{ g.kg | num: 1 }}kg</span> }</td>
                                    <td class="small">{{ h.qualityChecks }}</td>
                                </tr>
                            } @empty {
                                <tr><td colspan="9" class="text-muted">No harvests.</td></tr>
                            }
                        </tbody>
                    </table>
                    </div>
                </ng-template>
            </div>
        </section>
    `,
    styles: [
        `
            .rp-chip {
                min-height: 36px;
                padding: 0 0.9rem;
                margin: 0 0.4rem 0.4rem 0;
                border-radius: 18px;
                border: 1px solid var(--fpms-line-strong);
                background: var(--fpms-surface);
                font-size: 0.9rem;
                cursor: pointer;
            }
            .rp-chip[aria-pressed='true'] {
                background: var(--fpms-primary);
                border-color: var(--fpms-primary);
                color: #fff;
            }
        `,
    ],
})
export class ReportsComponent extends FpmsPage implements OnInit {
    readonly reports: { key: ReportKey; label: string }[] = [
        { key: 'cycle', label: 'Production Cycle Performance' },
        { key: 'harvest', label: 'Batch Harvest and Survival' },
        { key: 'sales', label: 'Sales by Buyer and Period' },
        { key: 'wq', label: 'Water Quality Exceptions' },
        { key: 'inventory', label: 'Feed and Additives Usage' },
        { key: 'failure', label: 'Batch Failure Report' },
    ];
    key: ReportKey = 'cycle';
    shown: ReportKey = 'cycle';
    r: any = null;
    generatedAt = '';
    cycles: any[] = [];
    batches: any[] = [];
    items: any[] = [];
    buyers: any[] = [];
    private pagers: Record<string, PagerState> = {};
    f: any = { cycleId: null, batchId: null, buyerId: null, itemId: null, from: FarmTime.addDays(FarmTime.today(), -30), to: FarmTime.today() };

    async ngOnInit(): Promise<void> {
        const [cycles, batches, items, buyers] = await Promise.all([
            this.api.get<any>('Cycle', 'GetPage', { maxResultCount: 100, sorting: 'startDate desc' }),
            this.api.get<any>('Batch', 'GetPage', { maxResultCount: 100 }),
            this.api.get<any[]>('Inventory', 'GetItems'),
            this.api.get<any[]>('Buyer', 'GetLookup'),
        ]);
        this.cycles = cycles.items;
        this.batches = batches.items;
        this.items = items;
        this.buyers = buyers ?? [];
        const report = this.query('report') as ReportKey;
        if (report && this.reports.some((x) => x.key === report)) {
            this.key = report;
        }
        this.f.cycleId = this.query('cycleId') ?? (this.key === 'cycle' ? this.cycles[0]?.id ?? null : null);
        this.f.batchId = this.query('batchId');
        if (this.query('cycleId') || this.query('batchId')) {
            this.runReport();
        }
    }

    ps(id: string, defaultSort = ''): PagerState {
        return (this.pagers[id] ??= new PagerState(defaultSort));
    }

    view(id: string, rows: any[] | null | undefined, defaultSort = ''): any[] {
        const p = this.ps(id, defaultSort);
        let list = rows ?? [];
        const [key, dir] = p.sort.split(' ');
        if (key) {
            const sign = dir === 'desc' ? -1 : 1;
            list = [...list].sort((a, b) => {
                const x = a[key];
                const y = b[key];
                if (x == null && y == null) { return 0; }
                if (x == null) { return 1; }
                if (y == null) { return -1; }
                return (typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y), undefined, { numeric: true })) * sign;
            });
        }
        p.total = list.length;
        p.page = Math.min(p.page, p.pages);
        return list.slice((p.page - 1) * p.size, p.page * p.size);
    }

    cyclesFor(): any[] {
        return this.key === 'failure' ? this.cycles.filter((c) => c.status === 3 || c.status === 4) : this.cycles;
    }

    batchesFor(): any[] {
        let list = this.f.cycleId ? this.batches.filter((b) => b.cycleId === this.f.cycleId) : this.batches;
        if (this.key === 'failure') {
            list = list.filter((b) => b.status === 4 || b.status === 5);
        }
        return list;
    }

    rangeTooLong(): boolean {
        if (!this.f.from || !this.f.to || ((this.key === 'harvest' || this.key === 'wq') && this.f.batchId)) {
            return false;
        }
        return this.f.to > FarmTime.addMonths(this.f.from, 24);
    }

    canRun(): boolean {
        if (this.key === 'cycle') {
            return !!this.f.cycleId;
        }
        if (this.key === 'failure') {
            return !!this.f.cycleId || !!this.f.batchId;
        }
        return (!this.f.from || !this.f.to || this.f.from <= this.f.to) && !this.rangeTooLong();
    }

    title(): string {
        return this.reports.find((x) => x.key === this.shown)?.label ?? '';
    }

    async runReport(): Promise<void> {
        const f = this.f;
        const range = { from: f.from || null, to: f.to || null };
        const key = this.key;
        let result: any;
        const ok = await this.act(async () => {
            switch (key) {
                case 'cycle':
                    result = await this.api.get('Report', 'GetCyclePerformance', { cycleId: f.cycleId });
                    break;
                case 'harvest':
                    result = await this.api.get('Report', 'GetHarvestSummary', { ...range, cycleId: f.cycleId, batchId: f.batchId });
                    break;
                case 'sales':
                    result = await this.api.get('Report', 'GetSalesByBuyer', { ...range, buyerId: f.buyerId });
                    break;
                case 'wq':
                    result = await this.api.get('Report', 'GetWaterQualityExceptions', { ...range, batchId: f.batchId });
                    break;
                case 'inventory':
                    result = await this.api.get('Report', 'GetInventoryMovements', { ...range, itemId: f.itemId });
                    break;
                case 'failure':
                    result = await this.api.get('Report', 'GetFailureReport', f.batchId ? { batchId: f.batchId } : { cycleId: f.cycleId });
                    break;
            }
        });
        if (ok) {
            this.shown = key;
            this.pagers = {};
            this.generatedAt = new Date().toISOString();
            this.r = result;
        }
    }

    async exportReport(): Promise<void> {
        const f = this.f;
        const kinds: Record<ReportKey, string> = {
            cycle: 'CyclePerformance',
            harvest: 'BatchHarvestAndSurvival',
            sales: 'SalesByBuyerAndPeriod',
            wq: 'WaterQualityExceptions',
            inventory: 'FeedAndAdditivesUsage',
            failure: 'BatchFailure',
        };
        const ranged = this.shown !== 'cycle' && this.shown !== 'failure';
        await this.exportCsv('ExportReport', {
            report: kinds[this.shown],
            cycleId: this.shown === 'cycle' || this.shown === 'harvest' || this.shown === 'failure' ? f.cycleId : null,
            batchId: this.shown === 'harvest' || this.shown === 'wq' || this.shown === 'failure' ? f.batchId : null,
            buyerId: this.shown === 'sales' ? f.buyerId : null,
            itemId: this.shown === 'inventory' ? f.itemId : null,
            from: ranged ? f.from || null : null,
            to: ranged ? f.to || null : null,
        });
    }
}
