import { Component, OnInit, effect, untracked } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { PagerState } from '@shared/fpms/pager';
import { logSummary } from '@shared/fpms/log-summary';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header [title]="b ? b.pondCode + ' · ' + (b.letter ? 'Batch ' + b.letter : b.batchCode) : 'Batch'" [subtitle]="b ? 'Cycle ' + b.cycleCode + ' · ' + (b.pondAreaM2AtStocking | num: 0) + ' m²' + (b.stockingDate ? ' · stocked ' + (b.stockingDate | farmDate) : '') + '.' : ''" backLink="/app/production/batches" backLabel="Batches">
            @if (b) {
                <fpms-badge [text]="b.status | label: L.BatchStatuses"></fpms-badge>
                @if (b.allowedLogTypes.length && can('Farm.Logs.Create')) {
                    <button type="button" class="btn btn-primary" (click)="drawer.open({ batchId: b.id })"><i class="fas fa-plus mr-1"></i>Record operations</button>
                }
                @if (can('Farm.Reports')) {
                    <a class="btn btn-outline-secondary" [routerLink]="['/app/reports/trace', b.id]"><i class="fas fa-project-diagram mr-1"></i>Trace report</a>
                }
                @if (b.canAddHarvest && can('Farm.Harvests.Create')) {
                    <a class="btn btn-outline-primary" routerLink="/app/harvests/new" [queryParams]="{ batchId: b.id }"><i class="fas fa-shrimp mr-1"></i>Record harvest</a>
                }
                @if (canManage && b.canDeclareFailure) {
                    <button class="btn btn-outline-danger" (click)="startFailure()">Declare failure</button>
                }
                @if (canManage && b.canClose) {
                    <button class="btn btn-outline-secondary" (click)="startClose()">Close batch</button>
                }
                @if (canManage && b.canReopen) {
                    <button class="btn btn-outline-secondary" (click)="reopening = { reason: '' }">Reopen batch</button>
                }
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (!b) {
                    <fpms-loading></fpms-loading>
                } @else {
                    @if (b.isReadOnly) {
                        <div class="alert alert-secondary">
                            <i class="fas fa-lock mr-1"></i>This batch is Closed{{ b.closedAtUtc ? ' (' + (b.closedAtUtc | farmDateTime) + ' by ' + b.closedBy + ')' : '' }}. Its logs and harvests are read-only. Sales payments can still be recorded.
                            @if (canManage && !b.canReopen && b.reopenBlockedReason) { <div class="small mt-1">To reopen it: {{ b.reopenBlockedReason }}</div> }
                        </div>
                    }
                    @if (reopening) {
                        <div class="fpms-dialog-backdrop"><div class="fpms-dialog fpms-dialog-sm"><div class="fpms-dialog-body p-0"><div class="card card-outline card-warning mb-0 border-0">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-undo" aria-hidden="true"></i>Reopen batch {{ b.batchCode }}</h3></div>
                            <div class="card-body">
                                <p class="small">
                                    The batch returns to {{ b.failureReason ? 'Failed' : 'Harvested' }} so its records can be corrected. Its final indicators are worked out again when you close it.
                                    The reason is kept in the batch's history and the activity log.
                                </p>
                                <label for="br">Reason <span class="text-danger">*</span></label>
                                <input id="br" class="form-control" [(ngModel)]="reopening.reason" maxlength="500" placeholder="e.g. the final harvest kg was entered wrong" />
                            </div>
                            <div class="card-footer">
                                <button class="btn btn-warning mr-2" [disabled]="busy() || reopening.reason.trim().length < 3" (click)="reopen()">Reopen batch</button>
                                <button class="btn btn-default" (click)="reopening = null">Cancel</button>
                            </div>
                        </div></div></div></div>
                    }
                    @if (b.earliestSafeHarvestDate && !b.isReadOnly) {
                        <div class="alert" [class.alert-warning]="b.earliestSafeHarvestDate > today" [class.alert-light]="b.earliestSafeHarvestDate <= today">
                            <i class="fas fa-prescription-bottle-alt mr-1"></i>Earliest safe harvest date from treatment withdrawal periods: <strong>{{ b.earliestSafeHarvestDate | farmDate }}</strong>
                            {{ b.earliestSafeHarvestDate > today ? '(a harvest before then saves with a warning)' : '(passed)' }}.
                        </div>
                    }
                    @if (b.status === 1 && docHint) {
                        <p class="small text-muted"><i class="fas fa-vial mr-1"></i>{{ docHint }}</p>
                    }
                    @if (b.failureReason) {
                        <div class="alert alert-danger">
                            <strong>Failed</strong> on {{ b.failureDate | farmDate }}: {{ b.failureReason }}
                            @if (b.estimatedLossPhp) { · estimated loss {{ b.estimatedLossPhp | php }} }
                            @if (can('Farm.Reports')) {
                                <a class="alert-link ml-2" [routerLink]="['/app/reports']" [queryParams]="{ report: 'failure', batchId: b.id }">Failure report</a>
                            }
                        </div>
                    }

                    @if (failure) {
                        <div class="fpms-dialog-backdrop"><div class="fpms-dialog fpms-dialog-sm"><div class="fpms-dialog-body p-0"><div class="card card-outline card-danger mb-0 border-0">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-ban" aria-hidden="true"></i>Declare batch failed</h3></div>
                            <form #ff="ngForm" (ngSubmit)="saveFailure()" novalidate>
                                <div class="card-body">
                                    <div class="form-row">
                                        <div class="form-group col-md-3">
                                            <label for="fd">Failure date <span class="text-danger">*</span></label>
                                            <input id="fd" type="date" class="form-control" name="fd" [(ngModel)]="failure.failureDate" required [min]="b.stockingDate" [max]="today" />
                                        </div>
                                        <div class="form-group col-md-3">
                                            <label for="fl">Estimated loss (₱)</label>
                                            <input id="fl" type="number" min="0" step="0.01" class="form-control" name="fl" [(ngModel)]="failure.estimatedLossPhp" />
                                        </div>
                                        <div class="form-group col-md-6">
                                            <label for="fr">Reason <span class="text-danger">*</span></label>
                                            <input id="fr" class="form-control" name="fr" [(ngModel)]="failure.reason" required maxlength="1000" />
                                        </div>
                                    </div>
                                    <p class="small text-muted mb-0">The pond becomes Post-harvest. A salvage harvest, notes and sales can still be recorded until the batch is closed.</p>
                                </div>
                                <div class="card-footer">
                                    <button class="btn btn-danger mr-2" type="submit" [disabled]="busy() || !ff.form.valid">Declare failed</button>
                                    <button class="btn btn-default" type="button" (click)="failure = null">Cancel</button>
                                    <fpms-form-hint [form]="ff"></fpms-form-hint>
                                </div>
                            </form>
                        </div></div></div></div>
                    }

                    @if (closing) {
                        <div class="fpms-dialog-backdrop"><div class="fpms-dialog fpms-dialog-sm"><div class="fpms-dialog-body p-0"><div class="card card-outline card-warning mb-0 border-0">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-lock" aria-hidden="true"></i>Close batch {{ b.batchCode }}</h3></div>
                            <div class="card-body">
                                <p>Final FCR, survival rate, ABW and culture days are fixed. Logs, feeding and harvests become read-only for everyone, and pond {{ b.pondCode }} becomes Available (or stays in preparation if a new round has started). The Owner can reopen the batch later, with a reason, to correct a mistake.</p>
                                @if (!closeCheck) {
                                    <p class="small text-muted"><i class="fas fa-spinner fa-spin mr-1"></i>Checking daily records…</p>
                                } @else if (closeCheck.blankDaysTotal) {
                                    <div class="alert alert-warning small">
                                        <strong>{{ closeCheck.blankDaysTotal }} day{{ closeCheck.blankDaysTotal === 1 ? '' : 's' }} with blank daily records</strong>
                                        between {{ closeCheck.from | farmDate }} and {{ closeCheck.to | farmDate }}:
                                        {{ closeCheck.blankFeedingSlots }} feeding session{{ closeCheck.blankFeedingSlots === 1 ? '' : 's' }} and {{ closeCheck.blankWaterDays }} water-quality day{{ closeCheck.blankWaterDays === 1 ? '' : 's' }} not recorded
                                        ({{ closeCheck.notFedSlots }} session{{ closeCheck.notFedSlots === 1 ? '' : 's' }} marked Not fed).
                                        Review them before closing; the final indicators use only what was recorded.
                                        <ul class="mb-0 mt-1 fpms-scroll-list">
                                            @for (d of closeCheck.blankDays; track d.date) {
                                                <li>{{ d.date | farmDate }}: {{ d.missing }}</li>
                                            }
                                            @if (closeCheck.blankDaysTotal > closeCheck.blankDays.length) {
                                                <li class="text-muted">…and {{ closeCheck.blankDaysTotal - closeCheck.blankDays.length }} more</li>
                                            }
                                        </ul>
                                    </div>
                                } @else {
                                    <p class="small text-success"><i class="fas fa-check mr-1"></i>Every day has its feeding and water-quality records{{ closeCheck.notFedSlots ? ' (' + closeCheck.notFedSlots + ' sessions marked Not fed)' : '' }}.</p>
                                }
                                @if (closeCheck?.flaggedLots?.length) {
                                    <div class="alert alert-warning small">
                                        <strong>{{ closeCheck.flaggedLots.length }} stock lot{{ closeCheck.flaggedLots.length === 1 ? '' : 's' }} used by this batch {{ closeCheck.flaggedLots.length === 1 ? 'is' : 'are' }} flagged for reconciliation:</strong>
                                        @for (l of closeCheck.flaggedLots; track $index) { <span class="d-block">{{ l.product }}, lot {{ l.lotNumber }}</span> }
                                        The feed or chemical amounts for this batch may be off until the stock is reconciled (Stock page). You can still close.
                                    </div>
                                }
                                @if (closeCheck?.unaccountedKg > 0.0005) {
                                    <div class="alert alert-warning small">
                                        <strong>{{ closeCheck.unaccountedKg | num: 3 }} kg harvested but not accounted for:</strong>
                                        harvested {{ closeCheck.harvestedKg | num: 3 }} kg, sold {{ closeCheck.soldKg | num: 3 }} kg, not sold (home use, given away, spoiled) {{ closeCheck.notSoldKg | num: 3 }} kg.
                                        Record the remaining sales or not-sold kg first, or close anyway.
                                    </div>
                                } @else if (closeCheck && closeCheck.harvestedKg > 0) {
                                    <p class="small text-success"><i class="fas fa-check mr-1"></i>All {{ closeCheck.harvestedKg | num: 3 }} harvested kg are sold or recorded as not sold.</p>
                                }
                                <div class="custom-control custom-checkbox">
                                    <input type="checkbox" class="custom-control-input" id="ro" [(ngModel)]="closing.confirm" />
                                    <label class="custom-control-label" for="ro">I understand this batch's records will become read-only.</label>
                                </div>
                            </div>
                            <div class="card-footer">
                                <button class="btn btn-warning mr-2" [disabled]="busy() || !closing.confirm" (click)="close()">Close batch</button>
                                <button class="btn btn-default" (click)="closing = null">Cancel</button>
                            </div>
                        </div></div></div></div>
                    }

                    @let i = b.indicators;
                    @if (i) {
                        <div class="row">
                            <div class="col-6 col-md-3 col-xl"><div class="fpms-kpi"><div class="v">{{ i.doc ?? '—' }}</div><div class="l">DOC</div></div></div>
                            <div class="col-6 col-md-3 col-xl"><div class="fpms-kpi"><div class="v">{{ i.currentAbwG | num: 2 }} g</div><div class="l">ABW</div></div></div>
                            <div class="col-6 col-md-3 col-xl">
                                <div class="fpms-kpi" [title]="i.populationBasis === 'Estimate' ? 'PL stocked × the latest survival estimate, less mortality and harvests after it' : 'PL stocked less recorded mortality and harvested pieces'">
                                    <div class="v">{{ i.estimatedPopulation | num: 0 }}</div>
                                    <div class="l">Est. population · {{ i.populationBasis === 'Estimate' ? 'from survival estimate' : 'from mortality' }}</div>
                                </div>
                            </div>
                            <div class="col-6 col-md-3 col-xl"><div class="fpms-kpi"><div class="v">{{ i.estimatedBiomassKg | num: 1 }} kg</div><div class="l">Est. biomass</div></div></div>
                            <div class="col-6 col-md-3 col-xl"><div class="fpms-kpi"><div class="v">{{ i.cumulativeFeedKg | num: 1 }} kg</div><div class="l">Feed to date</div></div></div>
                            <div class="col-6 col-md-3 col-xl"><div class="fpms-kpi"><div class="v">{{ (b.status === 5 ? i.finalFcr : i.fcrToDate) | num: 2 }}</div><div class="l">{{ b.status === 5 ? 'Final FCR' : 'FCR to date' }}</div></div></div>
                            <div class="col-6 col-md-3 col-xl">
                                <div class="fpms-kpi">
                                    <div class="v">{{ (b.status === 5 ? i.survivalRatePct : i.estimatedSurvivalPct) | num: 1 }}%</div>
                                    <div class="l">{{ b.status === 5 ? 'Survival rate' : 'Est. survival' }}</div>
                                </div>
                            </div>
                        </div>
                        <p class="small text-muted mt-n2">Computed from source records. Mortality to date: {{ i.cumulativeMortality | num: 0 }} · harvested {{ i.totalHarvestKg | num: 1 }} kg ({{ i.harvestedPieces | num: 0 }} pcs){{ i.cultureDays != null ? ' · culture days ' + i.cultureDays : '' }}.</p>
                    }

                    <div class="fpms-tabs no-print" role="tablist" aria-label="Batch pages">
                        @for (x of sections; track x.id) {
                            @if (x.id !== 'sec-calendar' || weeks.length) {
                                <button type="button" role="tab" [attr.aria-selected]="tab === x.id" (click)="tab = x.id">{{ x.label }}</button>
                            }
                        }
                    </div>

                    <div class="row">
                        <div class="col-12">
                            @if (tab === 'sec-stocking') {
                            <div class="card" id="sec-stocking">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-shrimp" aria-hidden="true"></i>Stocking</h3></div>
                                <div class="card-body">
                                    <dl class="row mb-0 fpms-dl">
                                        <dt class="col-6">Pond</dt><dd class="col-6"><a [routerLink]="['/app/ponds', b.pondId]">{{ b.pondCode }}</a> · {{ b.pondAreaM2AtStocking | num: 0 }} m²</dd>
                                        <dt class="col-6">Fry delivery</dt>
                                        <dd class="col-6"><a [routerLink]="['/app/resources/fry-deliveries', b.fryDeliveryId]">{{ b.hatcheryBatchNumber }}</a> · {{ b.hatcheryName }}</dd>
                                        <dt class="col-6">PL age</dt><dd class="col-6">{{ b.plAgeDays != null ? 'PL' + b.plAgeDays : '—' }}</dd>
                                        <dt class="col-6">Stocked</dt><dd class="col-6">{{ b.stockedAtUtc | farmDateTime }}</dd>
                                        <dt class="col-6">Stocking count</dt><dd class="col-6">{{ b.plCountStocked | num: 0 }} PL</dd>
                                        <dt class="col-6">Density</dt>
                                        <dd class="col-6">
                                            {{ b.stockingDensityPerM2 | num: 2 }} PL/m²
                                            @if (b.densityFlag) {
                                                <span class="badge ml-1" [class.badge-success]="b.densityFlag === 1" [class.badge-warning]="b.densityFlag !== 1">{{ b.densityFlag | label: L.DensityFlags }}</span>
                                            }
                                        </dd>
                                        <dt class="col-6">Farming system</dt><dd class="col-6">{{ b.farmingSystemAtStocking ? (b.farmingSystemAtStocking | label: L.FarmingSystems) : '—' }}</dd>
                                        <dt class="col-6">Pond construction</dt><dd class="col-6">{{ b.constructionTypeAtStocking ? (b.constructionTypeAtStocking | label: L.ConstructionTypes) : '—' }}</dd>
                                        <dt class="col-6">Transport</dt><dd class="col-6">{{ b.transportMode | label: L.TransportModes }}</dd>
                                        <dt class="col-6">Time processed</dt><dd class="col-6">{{ b.timeProcessedUtc ? (b.timeProcessedUtc | farmDateTime) : '—' }}</dd>
                                        <dt class="col-6">Acclimation</dt><dd class="col-6">{{ b.acclimationMinutes ?? '—' }} min</dd>
                                        <dt class="col-6">Initial survival est.</dt><dd class="col-6">{{ b.initialSurvivalEstimatePct ?? '—' }}%</dd>
                                        <dt class="col-6">Dead at stocking</dt><dd class="col-6">{{ b.deadAtStocking != null ? (b.deadAtStocking | num: 0) + ' PL' : '—' }}</dd>
                                        <dt class="col-6">PL health certificate</dt><dd class="col-6">{{ b.plHealthCertificateNo ?? '—' }}</dd>
                                        <dt class="col-6">SFSC no.</dt><dd class="col-6">{{ b.sfscNo ?? '—' }}</dd>
                                        <dt class="col-6">Hatchery certification</dt><dd class="col-6 small">{{ b.hatcheryCertificationStatusAtStocking }}</dd>
                                    </dl>
                                </div>
                            </div>
                            @if (b.reopenings?.length) {
                                <div class="card">
                                    <div class="card-header"><h3 class="card-title"><i class="fas fa-history" aria-hidden="true"></i>Reopened</h3></div>
                                    <ul class="list-group list-group-flush small">
                                        @for (r of b.reopenings; track $index) {
                                            <li class="list-group-item py-2">{{ r.reopenedAtUtc | farmDateTime }} by {{ r.reopenedBy }}: {{ r.reason }}</li>
                                        }
                                    </ul>
                                </div>
                            }
                            <div class="card">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-book" aria-hidden="true"></i>Log counts</h3></div>
                                <div class="card-body py-2">
                                    <div class="d-flex justify-content-between small py-1 border-bottom">
                                        <span>Feeding sessions (AM/PM)</span><span>{{ feedPager.total }}</span>
                                    </div>
                                    @for (t of logTypeKeys; track t.key) {
                                        @if (L.NewLogTypes.includes(t.value) || (b.logCounts[t.key] ?? b.logCounts[t.camel] ?? 0) > 0) {
                                        <div class="d-flex justify-content-between small py-1 border-bottom">
                                            <span>{{ t.label }}</span><span>{{ b.logCounts[t.key] ?? b.logCounts[t.camel] ?? 0 }}</span>
                                        </div>
                                        }
                                    }
                                </div>
                            </div>
                            }
                        </div>
                        <div class="col-12">
                            @if (tab === 'sec-calendar' && weeks.length) {
                                <div class="card" id="sec-calendar">
                                    <div class="card-header d-flex align-items-center">
                                        <h3 class="card-title mr-auto"><i class="far fa-calendar-alt" aria-hidden="true"></i>Log calendar</h3>
                                        <span class="small text-muted">{{ calSummary }}</span>
                                    </div>
                                    <div class="card-body">
                                        <div class="cal-scroll">
                                            <div class="cal">
                                                <div class="cal-side">
                                                    <span class="cal-month">&nbsp;</span>
                                                    @for (n of dayNames; track $index) { <span class="cal-label">{{ n }}</span> }
                                                </div>
                                                @for (w of weeks; track $index) {
                                                    <div class="cal-week">
                                                        <span class="cal-month">{{ w.label }}</span>
                                                        @for (c of w.days; track $index) {
                                                            @if (!c) {
                                                                <span class="cal-cell cal-none"></span>
                                                            } @else if (b.isReadOnly) {
                                                                <span [class]="'cal-cell ' + calClass(c)" [title]="calTip(c)">
                                                                    @if (c.hasMortality) { <i class="cal-dot cal-m"></i> }
                                                                    @if (c.hasGrowthSampling) { <i class="cal-dot cal-g"></i> }
                                                                    @if (c.hasChemical) { <i class="cal-dot cal-c"></i> }
                                                                </span>
                                                            } @else {
                                                                <a [class]="'cal-cell ' + calClass(c)" [title]="calTip(c)" role="button" tabindex="0" (click)="drawer.open({ batchId: b.id, date: c.date.substring(0, 10) })" (keydown.enter)="drawer.open({ batchId: b.id, date: c.date.substring(0, 10) })">
                                                                    @if (c.hasMortality) { <i class="cal-dot cal-m"></i> }
                                                                    @if (c.hasGrowthSampling) { <i class="cal-dot cal-g"></i> }
                                                                    @if (c.hasChemical) { <i class="cal-dot cal-c"></i> }
                                                                </a>
                                                            }
                                                        }
                                                    </div>
                                                }
                                            </div>
                                        </div>
                                        <div class="cal-legend small text-muted mt-2">
                                            <span class="cal-cell cal-s0"></span> Nothing logged
                                            <span class="cal-cell cal-s1 ml-2"></span> 1 of 3
                                            <span class="cal-cell cal-s2 ml-2"></span> 2 of 3
                                            <span class="cal-cell cal-s3 ml-2"></span> All 3
                                            <span class="ml-3"><i class="cal-dot cal-m"></i> Mortality</span>
                                            <span class="ml-2"><i class="cal-dot cal-g"></i> Growth sample</span>
                                            <span class="ml-2"><i class="cal-dot cal-c"></i> Chemical</span>
                                        </div>
                                        <div class="small text-muted mt-1">The three daily records are AM feeding, PM feeding and water quality; "Not fed" counts as logged.{{ b.isReadOnly ? '' : ' Click a day to open the feeding sheet for it.' }}</div>
                                    </div>
                                </div>
                            }
                            @if (tab === 'sec-feeding') {
                            <div class="card" id="sec-feeding">
                                <div class="card-header d-flex align-items-center">
                                    <h3 class="card-title mr-auto"><i class="fas fa-utensils" aria-hidden="true"></i>Feeding</h3>
                                    <span class="small text-muted">{{ b.indicators.cumulativeFeedKg | num: 1 }} kg in {{ feedPager.total }} session{{ feedPager.total === 1 ? '' : 's' }}</span>
                                </div>
                                <div class="card-body p-0 table-responsive fpms-scroll">
                                    <table class="table table-sm mb-0">
                                        <thead><tr><th fpmsSort="feedDate" [pager]="feedPager" (sorted)="loadFeeding()">Date</th><th fpmsSort="session" [pager]="feedPager" (sorted)="loadFeeding()">Session</th><th class="text-right">Kg</th><th>Feed lots</th><th>By</th></tr></thead>
                                        <tbody>
                                            @for (s of sessions; track s.id) {
                                                <tr>
                                                    <td class="text-nowrap"><a href="javascript:;" (click)="drawer.open({ kind: 'feeding', batchId: b.id, date: String(s.feedDate).substring(0, 10) })">{{ s.feedDate | farmDate }}</a></td>
                                                    <td>{{ s.session | label: L.FeedingSessions }}</td>
                                                    <td class="text-right">@if (s.status === 2) { <span class="text-muted">Not fed</span> } @else { {{ s.kg | num: 3 }} }</td>
                                                    <td class="small">
                                                        @for (l of s.lines; track l.lotId) { <div>{{ l.label }}{{ s.lines.length > 1 ? ': ' + (l.kg | num: 3) + ' kg' : '' }}</div> }
                                                    </td>
                                                    <td class="small text-muted">{{ s.recordedBy }}<div>recorded {{ s.creationTime | farmDateTime }}</div></td>
                                                </tr>
                                            } @empty {
                                                <tr><td colspan="5" class="text-center text-muted py-3">No feeding recorded yet.</td></tr>
                                            }
                                        </tbody>
                                    </table>
                                </div>
                                <fpms-pager [state]="feedPager" (changed)="loadFeeding()"></fpms-pager>
                            </div>
                            }
                            @if (tab === 'sec-logs') {
                            <div class="card" id="sec-logs">
                                <div class="card-header d-flex align-items-center">
                                    <h3 class="card-title mr-auto"><i class="fas fa-book" aria-hidden="true"></i>Logs</h3>
                                    <select class="form-control form-control-sm w-auto" aria-label="Filter log type" [(ngModel)]="logType" (ngModelChange)="logPager.reset(); loadLogs()">
                                        <option [ngValue]="null">All types</option>
                                        @for (o of Options.logType; track o.value) {
                                            <option [ngValue]="o.value">{{ o.label }}</option>
                                        }
                                    </select>
                                </div>
                                <div class="card-body p-0 table-responsive fpms-scroll">
                                    <table class="table table-sm mb-0">
                                        <thead><tr><th fpmsSort="observedAt" [pager]="logPager" (sorted)="loadLogs()">Observed</th><th fpmsSort="logType" [pager]="logPager" (sorted)="loadLogs()">Type</th><th>Details</th><th>By</th></tr></thead>
                                        <tbody>
                                            @for (l of logs; track l.id) {
                                                <tr>
                                                    <td class="text-nowrap"><a [routerLink]="['/app/logbook', l.id]">{{ l.observedAtUtc | farmDateTime }}</a></td>
                                                    <td>
                                                        {{ l.logType | label: L.LogTypes }}
                                                        @if (l.hasOutOfRange) { <i class="fas fa-exclamation-triangle text-warning ml-1" title="Out of range"></i> }
                                                    </td>
                                                    <td class="small">{{ summary(l) }}</td>
                                                    <td class="small text-muted">{{ l.recordedBy }}<div>recorded {{ l.creationTime | farmDateTime }}</div></td>
                                                </tr>
                                            } @empty {
                                                <tr><td colspan="4" class="text-center text-muted py-3">No log entries yet.</td></tr>
                                            }
                                        </tbody>
                                    </table>
                                </div>
                                <fpms-pager [state]="logPager" (changed)="loadLogs()"></fpms-pager>
                            </div>
                            }
                            @if (tab === 'sec-harvests') {
                            <div class="card" id="sec-harvests">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-shrimp" aria-hidden="true"></i>Harvests</h3></div>
                                <div class="card-body p-0 table-responsive">
                                    <table class="table table-sm mb-0">
                                        <thead><tr><th>Date</th><th>Type</th><th class="text-right">Kg</th><th class="text-right">ABW (g)</th><th class="text-right">Sold kg</th><th class="text-right">Unsold kg</th></tr></thead>
                                        <tbody>
                                            @for (h of harvests; track h.id) {
                                                <tr>
                                                    <td><a [routerLink]="['/app/harvests', h.id]">{{ h.harvestedAtUtc | farmDateTime }}</a></td>
                                                    <td>{{ h.harvestType | label: L.HarvestTypes }}</td>
                                                    <td class="text-right">{{ h.totalKg | num: 2 }}</td>
                                                    <td class="text-right">{{ h.abwG | num: 2 }}</td>
                                                    <td class="text-right">{{ h.soldKg | num: 2 }}</td>
                                                    <td class="text-right">{{ h.unsoldKg | num: 2 }}</td>
                                                </tr>
                                            } @empty {
                                                <tr><td colspan="6" class="text-center text-muted py-3">No harvests yet.</td></tr>
                                            }
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                            }
                        </div>
                    </div>
                }
            </div>
        </section>
    `,
    styles: [
        `
            .fpms-tabs {
                display: flex;
                flex-wrap: wrap;
                gap: 4px;
                margin: 0.25rem 0 1rem;
                border-bottom: 1px solid var(--fpms-line);
            }
            .fpms-tabs button {
                padding: 0.6rem 0.9rem;
                border: 0;
                border-bottom: 3px solid transparent;
                margin-bottom: -1px;
                background: transparent;
                color: var(--fpms-text);
                font-size: 0.92rem;
                cursor: pointer;
            }
            .fpms-tabs button[aria-selected='true'] {
                border-bottom-color: var(--fpms-primary);
                color: var(--fpms-primary);
                font-weight: 600;
            }
            .card[id^='sec-'] {
                scroll-margin-top: 1rem;
            }
        `,
        `
            .cal-scroll { overflow-x: auto; }
            .cal { display: flex; gap: 3px; }
            .cal-side, .cal-week { display: flex; flex-direction: column; gap: 3px; }
            .cal-month { height: 14px; font-size: 10px; line-height: 14px; white-space: nowrap; overflow: visible; color: #6c757d; }
            .cal-label { height: 14px; font-size: 10px; line-height: 14px; padding-right: 4px; color: #6c757d; }
            .cal-cell { position: relative; display: inline-block; width: 14px; height: 14px; border-radius: 3px; vertical-align: middle; }
            .cal-none { background: transparent; border: 1px dashed #e4e7ea; }
            .cal-s0 { background: #ffc107; }
            .cal-s1 { background: #b7e4c0; }
            .cal-s2 { background: #5fbf7a; }
            .cal-s3 { background: #2b7a46; }
            a.cal-cell:hover { outline: 2px solid #343a40; }
            .cal-dot { position: absolute; bottom: 1px; width: 4px; height: 4px; border-radius: 50%; border: 1px solid #fff; box-sizing: content-box; }
            .cal-cell > .cal-dot:nth-child(1) { left: 1px; }
            .cal-cell > .cal-dot:nth-child(2) { left: 5px; }
            .cal-cell > .cal-dot:nth-child(3) { left: 9px; }
            .cal-legend .cal-dot { position: static; display: inline-block; vertical-align: middle; }
            .cal-m { background: #dc3545; }
            .cal-g { background: #007bff; }
            .cal-c { background: #6f42c1; }
        `,
    ],
})
export class BatchDetailComponent extends FpmsPage implements OnInit {
    readonly sections = [
        { id: 'sec-stocking', label: 'Stocking details' },
        { id: 'sec-calendar', label: 'Log calendar' },
        { id: 'sec-feeding', label: 'Feeding' },
        { id: 'sec-logs', label: 'Logs' },
        { id: 'sec-harvests', label: 'Harvests' },
    ];

    tab = 'sec-stocking';

    b: any = null;
    logs: any[] = [];
    sessions: any[] = [];
    harvests: any[] = [];
    feedPager = new PagerState('feedDate desc');
    logPager = new PagerState('observedAt desc');
    logType: number | null = null;
    failure: any = null;
    closing: any = null;
    closeCheck: any = null;
    reopening: { reason: string } | null = null;
    calendar: any = null;
    weeks: { label: string; days: any[] }[] = [];
    calSummary = '';
    readonly dayNames = ['Mon', '', 'Wed', '', 'Fri', '', ''];

    get docHint(): string | null {
        const doc = this.b?.indicators?.doc;
        if (doc == null) {
            return null;
        }
        const next = [30, 45, 60, 75].find((d) => d >= doc - 2);
        return next == null
            ? null
            : `Health check: a PCR test is suggested around DOC 30, 45, 60 and 75. Next: DOC ${next}${next - doc > 0 ? ` (in ${next - doc} day${next - doc === 1 ? '' : 's'})` : ' (now)'}.`;
    }

    async reopen(): Promise<void> {
        if (await this.act(() => this.api.post('Batch', 'Reopen', { id: this.b.id, reason: this.reopening!.reason.trim(), rowVersion: this.b.rowVersion }))) {
            this.notifySaved('Batch reopened.');
            this.load();
        }
    }
    readonly logTypeKeys = Object.keys(this.L.LogTypeNames).map((key) => ({ key, value: this.L.LogTypeNames[key], camel: key[0].toLowerCase() + key.slice(1), label: this.L.LogTypes[this.L.LogTypeNames[key]] }));

    get canManage(): boolean {
        return this.can('Farm.Production.Manage');
    }

    private readonly reloadOnDrawerSave = (() => {
        let first = true;
        return effect(() => {
            this.drawer.saved();
            if (first) {
                first = false;
                return;
            }
            untracked(() => {
                if (this.b) {
                    void this.load();
                }
            });
        });
    })();

    protected readonly String = String;

    ngOnInit(): void {
        this.route.paramMap.subscribe(() => this.init());
    }

    async init(): Promise<void> {
        this.load();
    }

    async load(): Promise<void> {
        const id = this.param('id');
        this.b = await this.api.get<any>('Batch', 'Get', { id });
        this.setPageTitle(this.b ? `Batch ${this.b.batchCode}` : null);
        this.failure = this.closing = null;
        this.reopening = null;
        this.loadLogs();
        this.loadFeeding();
        this.loadCalendar();
        if (this.can('Farm.Harvests')) {
            this.harvests = (await this.api.get<any>('Harvest', 'GetList', { batchId: id, maxResultCount: 100, sorting: 'harvestedAt desc' })).items;
        }
    }

    async loadCalendar(): Promise<void> {
        const cal = await this.run(() => this.api.get<any>('Batch', 'GetLogCalendar', { id: this.b.id }));
        this.calendar = cal;
        const days: any[] = cal?.days ?? [];
        const weeks: { label: string; days: any[] }[] = [];
        const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        let current: any[] = new Array(7).fill(null);
        let lastMonth = -1;
        const flush = (): void => {
            const first = current.find((c) => c);
            if (first) {
                const month = new Date(String(first.date).substring(0, 10) + 'T00:00:00Z').getUTCMonth();
                weeks.push({ label: month !== lastMonth ? months[month] : '', days: current });
                lastMonth = month;
            }
            current = new Array(7).fill(null);
        };
        for (const d of days) {
            const row = (new Date(String(d.date).substring(0, 10) + 'T00:00:00Z').getUTCDay() + 6) % 7;
            if (row === 0) {
                flush();
            }
            current[row] = d;
        }
        flush();
        this.weeks = weeks;
        const complete = days.filter((d) => this.calScore(d) === 3).length;
        const empty = days.filter((d) => this.calScore(d) === 0).length;
        this.calSummary = days.length ? `${days.length} days · ${complete} fully logged · ${empty} with nothing logged` : '';
    }

    calScore(d: any): number {
        return [d.am, d.pm, d.water].filter((x) => x !== 0).length;
    }

    calClass(d: any): string {
        return 'cal-s' + this.calScore(d);
    }

    calTip(d: any): string {
        const missing = [d.am === 0 ? 'AM feeding' : '', d.pm === 0 ? 'PM feeding' : '', d.water === 0 ? 'water quality' : ''].filter((x) => x);
        const notFed = [d.am === 2 ? 'AM' : '', d.pm === 2 ? 'PM' : ''].filter((x) => x);
        const events = [d.hasMortality ? 'mortality' : '', d.hasGrowthSampling ? 'growth sample' : '', d.hasChemical ? 'chemical' : ''].filter((x) => x);
        return [
            String(d.date).substring(0, 10),
            missing.length ? 'Missing: ' + missing.join(', ') : 'All daily records in',
            notFed.length ? 'Not fed: ' + notFed.join(', ') : '',
            events.length ? 'Also: ' + events.join(', ') : '',
        ].filter((x) => x).join(' · ');
    }

    async loadLogs(): Promise<void> {
        const r = await this.api.get<any>('Log', 'GetList', { batchId: this.b.id, logType: this.logType, ...this.logPager.params });
        this.logs = r.items;
        if (this.logPager.update(r.totalCount)) {
            await this.loadLogs();
        }
    }

    async loadFeeding(): Promise<void> {
        const r = await this.api.get<any>('Feeding', 'GetList', { batchId: this.b.id, ...this.feedPager.params });
        this.sessions = r.items;
        if (this.feedPager.update(r.totalCount)) {
            await this.loadFeeding();
        }
    }

    summary(l: any): string {
        return logSummary(l);
    }

    feedKg(sessions: any[]): number {
        return sessions.reduce((sum, x) => sum + (x.kg ?? 0), 0);
    }

    startFailure(): void {
        this.failure = { id: this.b.id, reason: '', failureDate: this.today, estimatedLossPhp: null, rowVersion: this.b.rowVersion };
    }

    async saveFailure(): Promise<void> {
        if (await this.act(() => this.api.post('Batch', 'DeclareFailure', this.failure))) {
            this.notifySaved('Batch declared failed.');
            this.load();
        }
    }

    async startClose(): Promise<void> {
        this.closing = { confirm: false };
        this.closeCheck = null;
        this.closeCheck = (await this.run(() => this.api.get<any>('Batch', 'GetCloseCheck', { id: this.b.id }))) ?? { blankDaysTotal: 0, notFedSlots: 0, blankDays: [] };
    }

    async close(): Promise<void> {
        let result: any;
        if (await this.act(async () => (result = await this.api.post('Batch', 'Close', { id: this.b.id, confirmReadOnly: true, rowVersion: this.b.rowVersion })))) {
            this.notifySaved(`Batch closed. Final FCR ${result?.finalFcr?.toFixed?.(2) ?? '—'}, survival ${result?.survivalRatePct?.toFixed?.(1) ?? '—'}%.`);
            this.load();
        }
    }
}
