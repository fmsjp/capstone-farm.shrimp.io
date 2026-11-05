import { Component, OnInit } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { pondSystemWarning } from '@shared/fpms/labels';
import * as L from '@shared/fpms/labels';
import { FarmTime, newGuid } from '@shared/fpms/farm-time';
import { FormDrafts } from '@shared/fpms/drafts';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header [narrow]="true" [title]="isNew ? 'New pond' : 'Pond ' + (pond?.pondCode ?? '')" [subtitle]="isNew ? 'Add a grow-out pond to this farm. The code and name are made from the construction type and number.' : 'Current state, details, batches and history.'" backLink="/app/ponds" backLabel="Ponds">
            @if (pond && !isNew) {
                <fpms-badge [text]="pond.status | label: L.PondDisplayStatuses"></fpms-badge>
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid fpms-narrow">
                @if (!pond) {
                    <fpms-loading></fpms-loading>
                } @else if (isNew) {
                    <ng-container *ngTemplateOutlet="formTpl"></ng-container>
                } @else {
                    <div class="row">
                        <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ pond.name }}</div><div class="l">{{ pond.pondCode }}</div></div></div>
                        <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ pond.areaM2 | num: 0 }} m²</div><div class="l">Depth {{ pond.averageDepthM | num: 2 }} m avg · {{ pond.maxDepthM | num: 2 }} m max</div></div></div>
                        <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ pond.constructionType | label: L.ConstructionTypes }}</div><div class="l">{{ pond.purpose | label: L.PondPurposes }} · {{ pond.farmingSystem | label: L.FarmingSystems }}</div></div></div>
                        <div class="col-6 col-md-3">
                            <div class="fpms-kpi" [class.fpms-kpi-warn]="pond.preparationWarning">
                                @if (pond.currentBatchId) {
                                    <div class="v"><a [routerLink]="['/app/production/batches', pond.currentBatchId]">{{ pond.currentBatchCode }}</a></div>
                                    <div class="l">Open batch</div>
                                } @else if (pond.preparedDaysAgo != null) {
                                    <div class="v">Prepared</div>
                                    <div class="l">{{ pond.preparedDaysAgo === 0 ? 'today' : pond.preparedDaysAgo + ' days ago' }}</div>
                                } @else {
                                    <div class="v">{{ pond.status | label: L.PondDisplayStatuses }}</div>
                                    <div class="l">No open batch</div>
                                }
                            </div>
                        </div>
                    </div>
                    @if (pond.preparationWarning) {
                        <div class="alert alert-warning py-2 small" role="status"><i class="fas fa-exclamation-triangle mr-1"></i>{{ pond.preparationWarning }}</div>
                    }
                    @if (pond.isArchived) {
                        <div class="alert alert-secondary py-2 small d-flex flex-wrap align-items-center justify-content-between" role="status">
                            <span><i class="fas fa-archive mr-1"></i>This pond is archived. It is hidden from the pond list and cannot be edited, prepared or stocked.</span>
                            @if (canManage) {
                                <button type="button" class="btn btn-sm btn-outline-secondary" [disabled]="busy()" (click)="restore()"><i class="fas fa-undo mr-1"></i>Restore pond</button>
                            }
                        </div>
                    }
                    @if (systemWarning) {
                        <div class="alert alert-warning py-2 small" role="status"><i class="fas fa-exclamation-triangle mr-1"></i>{{ systemWarning }}</div>
                    }

                    <div class="fpms-tabs no-print" role="tablist" aria-label="Pond pages">
                        @for (x of tabs; track x.id) {
                            <button type="button" role="tab" [attr.aria-selected]="tab === x.id" (click)="setTab(x.id)">{{ x.label }}{{ x.count != null ? ' (' + x.count + ')' : '' }}</button>
                        }
                    </div>

                    @if (tab === 'overview') {
                        <div class="row">
                            <div class="col-lg-7">
                                <div class="card" [class.card-outline]="pond.openRound" [class.card-info]="pond.openRound">
                                    <div class="card-header"><h3 class="card-title"><i class="fas fa-broom" aria-hidden="true"></i>Preparation</h3></div>
                                    <div class="card-body">
                                        @if (pond.openRound) {
                                            <p class="small mb-2">
                                                Started {{ pond.openRound.startedAtUtc | farmDateTime }}.
                                                @if (pond.openRound.preparedAtUtc) {
                                                    <strong>Prepared</strong> {{ pond.openRound.preparedAtUtc | farmDateTime }} by {{ pond.openRound.preparedBy }}; ready for Create Batch (Stocking).
                                                } @else {
                                                    In progress.
                                                }
                                            </p>
                                            <ul class="fpms-records">
                                                @for (e of pond.openRound.entries; track e.id) {
                                                    <li>
                                                        @if (editEntry?.id === e.id) {
                                                            <input type="datetime-local" class="form-control form-control-sm mb-1" [(ngModel)]="editEntry.observedAt" [max]="nowInput" aria-label="Date and time" />
                                                            <textarea rows="2" class="form-control form-control-sm mb-1" [(ngModel)]="editEntry.description" maxlength="1000" aria-label="What was done"></textarea>
                                                            <button class="btn btn-xs btn-primary mr-1" [disabled]="busy() || !editEntry.description?.trim() || !editEntry.observedAt" (click)="saveEntry()">Save</button>
                                                            <button class="btn btn-xs btn-default" (click)="editEntry = null">Cancel</button>
                                                        } @else {
                                                            <span class="meta">{{ e.observedAtUtc | farmDateTime }} · {{ e.recordedBy }}</span>
                                                            <span class="what">{{ e.description }}</span>
                                                            @if (canEditEntries) {
                                                                <span class="acts small"><a href="javascript:;" class="mr-3" (click)="startEdit(e)">Edit</a><a href="javascript:;" class="text-danger" (click)="startAction('void', e.id)">Remove</a></span>
                                                            }
                                                        }
                                                    </li>
                                                }
                                            </ul>
                                        } @else {
                                            <p class="small text-muted">No preparation in progress. The first entry starts a preparation round and the pond becomes Under preparation.</p>
                                        }
                                        @if (pond.canAddPreparationEntry) {
                                            @if (prepRestored) {
                                                <div class="small text-info mb-1"><i class="fas fa-history mr-1"></i>Your unsaved entry was restored. <a href="javascript:;" (click)="discardPrep()">Discard</a></div>
                                            }
                                            <div class="form-group mb-2">
                                                <label for="pt" class="small mb-0">Date and time</label>
                                                <input id="pt" type="datetime-local" class="form-control form-control-sm" [(ngModel)]="prep.observedAt" (ngModelChange)="keepPrepDraft()" [max]="nowInput" />
                                            </div>
                                            <div class="form-group mb-2">
                                                <label for="pdx" class="small mb-0">What was done</label>
                                                <textarea id="pdx" rows="2" class="form-control form-control-sm" [(ngModel)]="prep.description" (ngModelChange)="keepPrepDraft()" maxlength="1000" placeholder="e.g. Drained pond, drying started"></textarea>
                                            </div>
                                            <button class="btn btn-sm btn-outline-primary mr-1" [disabled]="busy() || !prep.description?.trim() || !prep.observedAt" (click)="addPrep()">Add entry</button>
                                        }
                                        @if (pond.canMarkPrepared) {
                                            <button class="btn btn-sm btn-primary mr-1" [disabled]="busy()" (click)="markPrepared()"><i class="fas fa-check mr-1"></i>Mark Prepared</button>
                                        }
                                        @if (canManage && pond.openRound) {
                                            <div class="mt-2">
                                                @if (pond.openRound.preparedAtUtc) {
                                                    <button class="btn btn-sm btn-outline-secondary mr-1" [disabled]="busy()" (click)="startAction('reopen')">Reopen preparation</button>
                                                }
                                                <button class="btn btn-sm btn-outline-danger" [disabled]="busy()" (click)="startAction('abandon')">Abandon round</button>
                                            </div>
                                        }
                                        @if (prepAction) {
                                            <div class="border rounded p-2 mt-2 small" role="group" aria-label="Reason">
                                                <label for="pr" class="mb-1">{{ actionTitle }}: reason <span class="text-danger">*</span></label>
                                                <input id="pr" class="form-control form-control-sm mb-2" [(ngModel)]="prepAction.reason" maxlength="500" />
                                                <button class="btn btn-xs btn-primary mr-1" [disabled]="busy() || !prepAction.reason?.trim()" (click)="confirmAction()">Confirm</button>
                                                <button class="btn btn-xs btn-default" (click)="prepAction = null">Cancel</button>
                                            </div>
                                        }
                                        @if (pond.pastRounds.length) {
                                            <details class="mt-2 small">
                                                <summary>Earlier rounds ({{ pond.pastRounds.length }})</summary>
                                                @for (r of pond.pastRounds; track r.id) {
                                                    <div class="mt-1"><strong>{{ r.startedAtUtc | farmDayOf }}</strong>{{ r.usedByBatchCode ? ' → batch ' + r.usedByBatchCode : ' (abandoned)' }}</div>
                                                    <ul class="fpms-records">
                                                        @for (e of r.entries; track e.id) {
                                                            <li><span class="meta">{{ e.observedAtUtc | farmDayOf }}</span><span class="what">{{ e.description }}</span></li>
                                                        }
                                                    </ul>
                                                }
                                            </details>
                                        }
                                    </div>
                                </div>
                            </div>
                            <div class="col-lg-5">
                                <div class="card">
                                    <div class="card-header"><h3 class="card-title"><i class="fas fa-flag" aria-hidden="true"></i>Status</h3></div>
                                    <div class="card-body">
                                        <p class="mb-2">
                                            Displayed status: <fpms-badge [text]="pond.status | label: L.PondDisplayStatuses"></fpms-badge>
                                            @if (pond.currentBatchId) {
                                                <br />Open batch: <a [routerLink]="['/app/production/batches', pond.currentBatchId]">{{ pond.currentBatchCode }}</a>
                                            }
                                        </p>
                                        @if (canManage && !pond.isArchived) {
                                            <div class="form-group mb-2">
                                                <label for="ms">Manual status</label>
                                                <select id="ms" class="form-control" [(ngModel)]="newStatus">
                                                    @for (o of Options.pondManualStatus; track o.value) {
                                                        <option [ngValue]="o.value">{{ o.label }}</option>
                                                    }
                                                </select>
                                            </div>
                                            <div class="form-group mb-2">
                                                <label for="sn">Note</label>
                                                <input id="sn" class="form-control" [(ngModel)]="statusNote" maxlength="500" />
                                            </div>
                                            <button class="btn btn-outline-primary btn-sm" [disabled]="busy() || newStatus === pond.manualStatus" (click)="setStatus()">Change status</button>
                                        }
                                        @if (canManage && pond.canArchive) {
                                            <hr />
                                            <div class="small text-muted mb-2">No longer used, or added by mistake? Archiving keeps the record but hides the pond from the list and from stocking. You can restore it later.</div>
                                            <button type="button" class="btn btn-outline-secondary btn-sm" [disabled]="busy()" (click)="archive()"><i class="fas fa-archive mr-1"></i>Archive pond</button>
                                        } @else if (canManage && !pond.isArchived) {
                                            <hr />
                                            <div class="small text-muted mb-0"><i class="fas fa-archive mr-1"></i>This pond can be archived once its batch is closed and no preparation is in progress.</div>
                                        }
                                    </div>
                                </div>
                            </div>
                        </div>
                    }

                    @if (tab === 'details') {
                        @if (editing) {
                            <ng-container *ngTemplateOutlet="formTpl"></ng-container>
                        } @else {
                            <div class="card">
                                <div class="card-header">
                                    <h3 class="card-title"><i class="fas fa-water" aria-hidden="true"></i>Pond master data</h3>
                                    @if (canManage && !pond.isArchived) {
                                        <div class="card-tools"><button type="button" class="btn btn-sm btn-outline-primary" (click)="editing = true"><i class="fas fa-pen mr-1"></i>Edit</button></div>
                                    }
                                </div>
                                <div class="card-body">
                                    <dl class="row mb-0 fpms-dl">
                                        <dt class="col-sm-4">Pond ID</dt><dd class="col-sm-8">{{ pond.pondCode }}</dd>
                                        <dt class="col-sm-4">Name</dt><dd class="col-sm-8">{{ pond.name }}</dd>
                                        <dt class="col-sm-4">Purpose</dt><dd class="col-sm-8">{{ pond.purpose | label: L.PondPurposes }}</dd>
                                        <dt class="col-sm-4">Construction</dt><dd class="col-sm-8">{{ pond.constructionType | label: L.ConstructionTypes }}</dd>
                                        <dt class="col-sm-4">Area</dt><dd class="col-sm-8">{{ pond.areaM2 | num: 2 }} m²</dd>
                                        <dt class="col-sm-4">Depth</dt><dd class="col-sm-8">{{ pond.averageDepthM | num: 2 }} m average · {{ pond.maxDepthM | num: 2 }} m maximum</dd>
                                        <dt class="col-sm-4">Farming system</dt>
                                        <dd class="col-sm-8">{{ pond.farmingSystem | label: L.FarmingSystems }}{{ pond.farmingSystemOverride == null ? ' (farm setting)' : ' (this pond)' }}</dd>
                                        <dt class="col-sm-4">Inlet</dt><dd class="col-sm-8">@if (pond.inletType) { {{ pond.inletType }} } @else { <span class="fpms-none">—</span> }</dd>
                                        <dt class="col-sm-4">Outlet</dt><dd class="col-sm-8">@if (pond.outletType) { {{ pond.outletType }} } @else { <span class="fpms-none">—</span> }</dd>
                                        <dt class="col-sm-4">Aeration</dt><dd class="col-sm-8">@if (pond.aerationSystem) { {{ pond.aerationSystem }} } @else { <span class="fpms-none">—</span> }</dd>
                                        <dt class="col-sm-4">Location</dt><dd class="col-sm-8">@if (pond.latitude != null && pond.longitude != null) { {{ pond.latitude | num: 6 }}, {{ pond.longitude | num: 6 }} } @else { <span class="fpms-none">—</span> }</dd>
                                        <dt class="col-sm-4">Notes</dt><dd class="col-sm-8 mb-0">@if (pond.notes) { <span class="fpms-notes">{{ pond.notes }}</span> } @else { <span class="fpms-none">—</span> }</dd>
                                    </dl>
                                    @if (lockedCount) {
                                        <p class="small text-muted mt-3 mb-0"><i class="fas fa-lock mr-1"></i>Some fields are locked because of this pond's batches. Open Edit to see which and why.</p>
                                    }
                                </div>
                            </div>
                        }
                    }

                    @if (tab === 'batches') {
                        <div class="card">
                            <div class="card-body p-0 table-responsive">
                                <table class="table table-sm mb-0">
                                    <thead><tr><th>Batch</th><th>Cycle</th><th>Status</th><th>Stocked</th></tr></thead>
                                    <tbody>
                                        @for (b of pond.batches; track b.id) {
                                            <tr [class.table-info]="b.id === pond.currentBatchId">
                                                <td class="text-nowrap"><a [routerLink]="['/app/production/batches', b.id]">{{ b.batchCode }}</a></td>
                                                <td class="small text-muted text-nowrap">{{ b.cycleCode }}</td>
                                                <td><fpms-badge [text]="b.status | label: L.BatchStatuses"></fpms-badge></td>
                                                <td class="text-nowrap small">{{ b.stockingDate | farmDate }}</td>
                                            </tr>
                                        } @empty {
                                            <tr><td colspan="4" class="text-muted text-center py-3">No batches yet.</td></tr>
                                        }
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    }

                    @if (tab === 'history') {
                        <div class="card">
                            <div class="card-header">
                                <h3 class="card-title"><i class="fas fa-history" aria-hidden="true"></i>History</h3>
                                <div class="card-tools">
                                    <select class="form-control form-control-sm" [(ngModel)]="historyType" aria-label="Show">
                                        <option [ngValue]="null">All changes</option>
                                        @for (o of historyTypes; track o.value) {
                                            <option [ngValue]="o.value">{{ o.label }}</option>
                                        }
                                    </select>
                                </div>
                            </div>
                            <div class="card-body p-0">
                                <ul class="list-group list-group-flush">
                                    @for (h of filteredHistory; track $index) {
                                        <li class="list-group-item py-2">
                                            <span class="fpms-records"><span class="meta d-block">{{ h.changeDateUtc | farmDateTime }} · {{ h.changeType | label: L.PondChangeTypes }} · {{ h.recordedBy }}</span>
                                            <span class="what d-block">{{ h.description }}</span></span>
                                        </li>
                                    } @empty {
                                        <li class="list-group-item text-muted text-center py-3">Nothing recorded.</li>
                                    }
                                </ul>
                            </div>
                        </div>
                    }
                }
            </div>
        </section>

        <ng-template #formTpl>
            <form #f="ngForm" (ngSubmit)="save()" novalidate>
                <fieldset [disabled]="!canManage">
                    <div class="card">
                        <div class="card-header"><h3 class="card-title"><i class="fas fa-water" aria-hidden="true"></i>Pond master data</h3></div>
                        <div class="card-body">
                            <div class="form-row">
                                @if (isNew) {
                                    <div class="form-group col-md-4">
                                        <label for="ctype">Construction (pond type) <span class="text-danger">*</span></label>
                                        <select id="ctype" class="form-control" name="constructionType" [(ngModel)]="pond.constructionType" required>
                                            @for (o of Options.construction; track o.value) {
                                                <option [ngValue]="o.value">{{ o.label }}</option>
                                            }
                                        </select>
                                        <small class="form-text text-warning-dark"><i class="fas fa-exclamation-circle mr-1"></i>Check the type before saving. It names the pond and cannot be changed afterwards.</small>
                                    </div>
                                    <div class="form-group col-md-2">
                                        <label for="num">Number</label>
                                        <input id="num" type="number" min="1" max="999" step="1" class="form-control" name="number" [(ngModel)]="pond.number" [placeholder]="nextNumber" />
                                    </div>
                                    <div class="form-group col-md-3">
                                        <label>Pond ID and name</label>
                                        <p class="form-control-plaintext font-weight-bold" aria-live="polite">{{ previewCode }} <span class="font-weight-normal text-muted">{{ previewName }}</span></p>
                                    </div>
                                } @else {
                                    <div class="form-group col-md-3">
                                        <label>Pond ID</label>
                                        <p class="form-control-plaintext font-weight-bold">{{ pond.pondCode }}</p>
                                    </div>
                                    <div class="form-group col-md-5">
                                        <label>Name</label>
                                        <p class="form-control-plaintext">{{ pond.name }}</p>
                                    </div>
                                }
                                <div class="form-group col-md-4">
                                    <label for="purpose">Purpose</label>
                                    <select id="purpose" class="form-control" name="purpose" [(ngModel)]="pond.purpose" [disabled]="isLocked('purpose')">
                                        @for (o of Options.pondPurpose; track o.value) {
                                            <option [ngValue]="o.value">{{ o.label }}</option>
                                        }
                                    </select>
                                    @if (isLocked('purpose')) {
                                        <small class="form-text text-muted"><i class="fas fa-lock mr-1"></i>{{ lockReason('purpose') }}</small>
                                    }
                                </div>
                            </div>
                            <div class="form-row">
                                <div class="form-group col-md-4">
                                    <label for="area">Area <span class="text-danger">*</span></label>
                                    <div class="input-group">
                                        <input id="area" type="number" min="0.01" step="0.01" class="form-control" name="areaM2" [(ngModel)]="pond.areaM2" [disabled]="isLocked('areaM2')" required />
                                        <div class="input-group-append"><span class="input-group-text">m²</span></div>
                                    </div>
                                    @if (isLocked('areaM2')) {
                                        <small class="form-text text-muted"><i class="fas fa-lock mr-1"></i>{{ lockReason('areaM2') }}</small>
                                    }
                                </div>
                                <div class="form-group col-md-4">
                                    <label for="avg">Average depth <span class="text-danger">*</span></label>
                                    <div class="input-group">
                                        <input id="avg" type="number" min="0.01" max="100" step="0.01" class="form-control" name="averageDepthM" [(ngModel)]="pond.averageDepthM" [disabled]="isLocked('averageDepthM')" required />
                                        <div class="input-group-append"><span class="input-group-text">m</span></div>
                                    </div>
                                    @if (isLocked('averageDepthM')) {
                                        <small class="form-text text-muted"><i class="fas fa-lock mr-1"></i>{{ lockReason('averageDepthM') }}</small>
                                    }
                                </div>
                                <div class="form-group col-md-4">
                                    <label for="max">Maximum depth <span class="text-danger">*</span></label>
                                    <div class="input-group">
                                        <input id="max" type="number" min="0.01" max="100" step="0.01" class="form-control" name="maxDepthM" [(ngModel)]="pond.maxDepthM" [disabled]="isLocked('maxDepthM')" required />
                                        <div class="input-group-append"><span class="input-group-text">m</span></div>
                                    </div>
                                    @if (depthError) {
                                        <small class="text-danger">Maximum depth cannot be less than average depth.</small>
                                    }
                                </div>
                            </div>
                            <div class="form-row">
                                @if (!isNew) {
                                    <div class="form-group col-md-4">
                                        <label for="ctype2">Construction (pond type)</label>
                                        <select id="ctype2" class="form-control" name="constructionType2" [(ngModel)]="pond.constructionType" [disabled]="isLocked('constructionType')">
                                            @for (o of Options.construction; track o.value) {
                                                <option [ngValue]="o.value">{{ o.label }}</option>
                                            }
                                        </select>
                                        @if (isLocked('constructionType')) {
                                            <small class="form-text text-muted"><i class="fas fa-lock mr-1"></i>{{ lockReason('constructionType') }}</small>
                                        }
                                    </div>
                                }
                                <div class="form-group col-md-4">
                                    <label for="fs">Farming system</label>
                                    <select id="fs" class="form-control" name="farmingSystemOverride" [(ngModel)]="pond.farmingSystemOverride" [disabled]="isLocked('farmingSystemOverride')">
                                        <option [ngValue]="null">Farm setting ({{ farmSystem | label: L.FarmingSystems }})</option>
                                        @for (o of Options.farmingSystem; track o.value) {
                                            <option [ngValue]="o.value">{{ o.label }}</option>
                                        }
                                    </select>
                                    @if (isLocked('farmingSystemOverride')) {
                                        <small class="form-text text-muted"><i class="fas fa-lock mr-1"></i>{{ lockReason('farmingSystemOverride') }}</small>
                                    }
                                </div>
                            </div>
                            @if (systemWarning) {
                                <div class="alert alert-warning py-2 small" role="status">
                                    <i class="fas fa-exclamation-triangle mr-1"></i>{{ systemWarning }} You can still save the pond.
                                </div>
                            }
                            <div class="form-row">
                                <div class="form-group col-md-4">
                                    <label for="inlet">Inlet type</label>
                                    <input id="inlet" class="form-control" name="inletType" [(ngModel)]="pond.inletType" maxlength="100" placeholder="e.g. PVC pipe with screen" />
                                </div>
                                <div class="form-group col-md-4">
                                    <label for="outlet">Outlet type</label>
                                    <input id="outlet" class="form-control" name="outletType" [(ngModel)]="pond.outletType" maxlength="100" placeholder="e.g. Monk / central drain" />
                                </div>
                                <div class="form-group col-md-4">
                                    <label for="aer">Aeration system</label>
                                    <input id="aer" class="form-control" name="aerationSystem" [(ngModel)]="pond.aerationSystem" maxlength="300" placeholder="Type, count, horsepower" />
                                </div>
                            </div>
                            <div class="form-row">
                                <div class="form-group col-md-3">
                                    <label for="lat">Latitude</label>
                                    <input id="lat" type="number" step="0.000001" min="-90" max="90" class="form-control" name="latitude" [(ngModel)]="pond.latitude" />
                                </div>
                                <div class="form-group col-md-3">
                                    <label for="lng">Longitude</label>
                                    <input id="lng" type="number" step="0.000001" min="-180" max="180" class="form-control" name="longitude" [(ngModel)]="pond.longitude" />
                                </div>
                            </div>
                            <div class="form-group">
                                <label for="notes">Notes</label>
                                <textarea id="notes" rows="2" class="form-control" name="notes" [(ngModel)]="pond.notes" maxlength="1000"></textarea>
                            </div>
                            @if (!isNew) {
                                <div class="form-group mb-0">
                                    <label for="cn">Change note @if (noteRequired) { <span class="text-danger">*</span> } @else { (optional) }</label>
                                    <input id="cn" class="form-control" name="changeNote" [(ngModel)]="pond.changeNote" maxlength="500" [required]="noteRequired" placeholder="Why the infrastructure changed, e.g. added 2 paddlewheels" />
                                    <small class="form-text text-muted">
                                        @if (noteRequired) {
                                            Required because area or depth changed. Earlier batches keep the area they were stocked with.
                                        } @else {
                                            Changes are added to the pond history.
                                        }
                                    </small>
                                </div>
                            }
                        </div>
                        @if (canManage) {
                            <div class="card-footer">
                                <button type="submit" class="btn btn-primary mr-2" [disabled]="busy() || !f.form.valid || depthError">Save pond</button>
                                @if (isNew) {
                                    <a routerLink="/app/ponds" class="btn btn-default">Cancel</a>
                                } @else {
                                    <button type="button" class="btn btn-default" (click)="cancelEdit()">Cancel</button>
                                }
                                <fpms-form-hint [form]="f"></fpms-form-hint>
                            </div>
                        }
                    </div>
                </fieldset>
            </form>
        </ng-template>
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
        `,
    ],
})
export class PondDetailComponent extends FpmsPage implements OnInit {
    protected override keepsDrafts = true;
    pond: any = null;
    isNew = false;
    newStatus = 1;
    statusNote = '';

    get canManage(): boolean {
        return this.can('Farm.Ponds.Manage');
    }

    prefixes: any[] = [];
    farmSystem = 2;

    tab = 'overview';
    editing = false;
    historyType: number | null = null;
    editEntry: { id: string; observedAt: string; description: string } | null = null;
    prepAction: { kind: 'reopen' | 'abandon' | 'void'; entryId?: string; reason: string } | null = null;
    private original: { areaM2: number; averageDepthM: number; maxDepthM: number } | null = null;
    private tabChosen = false;

    readonly historyTypes = L.Options.pondChangeType;

    get tabs(): { id: string; label: string; count: number | null }[] {
        return [
            { id: 'overview', label: 'Overview', count: null },
            { id: 'details', label: 'Details', count: null },
            { id: 'batches', label: 'Batches', count: this.pond?.batches?.length ?? 0 },
            { id: 'history', label: 'History', count: this.pond?.history?.length ?? 0 },
        ];
    }

    setTab(id: string): void {
        this.tab = id;
        this.router.navigate([], { relativeTo: this.route, queryParams: { tab: id }, queryParamsHandling: 'merge', replaceUrl: true });
    }

    get filteredHistory(): any[] {
        const all: any[] = this.pond?.history ?? [];
        return this.historyType == null ? all : all.filter((h) => h.changeType === this.historyType);
    }

    isLocked(field: string): boolean {
        return !this.isNew && !!this.pond?.lockedFields?.[field];
    }

    lockReason(field: string): string {
        return this.pond?.lockedFields?.[field] ?? '';
    }

    get lockedCount(): number {
        return Object.keys(this.pond?.lockedFields ?? {}).length;
    }

    private get sizeChanged(): boolean {
        const o = this.original;
        return !!o && (Number(this.pond.areaM2) !== o.areaM2 || Number(this.pond.averageDepthM) !== o.averageDepthM || Number(this.pond.maxDepthM) !== o.maxDepthM);
    }

    get noteRequired(): boolean {
        return !this.isNew && !!this.pond?.sizeChangeNeedsNote && this.sizeChanged;
    }

    get canEditEntries(): boolean {
        return this.can('Farm.Ponds.Prepare') && !!this.pond?.openRound && !this.pond.openRound.preparedAtUtc;
    }

    cancelEdit(): void {
        this.editing = false;
        this.load(this.pond.id);
    }

    startEdit(e: any): void {
        this.editEntry = { id: e.id, observedAt: FarmTime.toLocalInput(e.observedAtUtc), description: e.description };
    }

    async saveEntry(): Promise<void> {
        const x = this.editEntry!;
        const body = { entryId: x.id, observedAt: FarmTime.fromLocalInput(x.observedAt), description: x.description };
        if (await this.act(() => this.api.post('Pond', 'EditPreparationEntry', body))) {
            this.editEntry = null;
            this.notifySaved('Preparation entry changed.');
            this.load(this.pond.id);
        }
    }

    startAction(kind: 'reopen' | 'abandon' | 'void', entryId?: string): void {
        this.prepAction = { kind, entryId, reason: '' };
    }

    get actionTitle(): string {
        const k = this.prepAction?.kind;
        return k === 'reopen' ? 'Reopen preparation' : k === 'abandon' ? 'Abandon round' : 'Remove entry';
    }

    async confirmAction(): Promise<void> {
        const a = this.prepAction!;
        const call =
            a.kind === 'void'
                ? () => this.api.post('Pond', 'VoidPreparationEntry', { entryId: a.entryId, reason: a.reason })
                : () => this.api.post('Pond', a.kind === 'reopen' ? 'ReopenPreparation' : 'AbandonPreparation', { pondId: this.pond.id, reason: a.reason });
        if (await this.act(call)) {
            this.prepAction = null;
            this.notifySaved(a.kind === 'void' ? 'Entry removed.' : a.kind === 'reopen' ? 'Preparation reopened.' : 'Preparation round abandoned.');
            this.load(this.pond.id);
        }
    }
    prep: any = this.newPrep();
    prepRestored = false;
    readonly nowInput = FarmTime.nowInput();

    private static readonly CONSTRUCTION_PREFIX: Record<number, { code: string; name: string }> = {
        1: { code: 'EP', name: 'Earthen Pond' },
        2: { code: 'CP', name: 'Concrete Pond' },
        3: { code: 'HP', name: 'HDPE-lined Pond' },
    };

    private get typePrefix(): { code: string; name: string } {
        return PondDetailComponent.CONSTRUCTION_PREFIX[Number(this.pond?.constructionType)] ?? PondDetailComponent.CONSTRUCTION_PREFIX[1];
    }

    get nextNumber(): number {
        return this.prefixes.find((x) => x.shortCode === this.typePrefix.code)?.nextNumber ?? 1;
    }

    get previewCode(): string {
        return `${this.typePrefix.code}-${String(this.pond?.number || this.nextNumber).padStart(2, '0')}`;
    }

    get previewName(): string {
        return `${this.typePrefix.name} ${this.pond?.number || this.nextNumber}`;
    }

    get systemWarning(): string | null {
        const system = this.pond?.farmingSystemOverride ?? this.farmSystem;
        return this.pond ? pondSystemWarning(Number(this.pond.constructionType), Number(system)) : null;
    }

    get depthError(): boolean {
        return this.pond?.maxDepthM != null && this.pond?.averageDepthM != null && Number(this.pond.maxDepthM) < Number(this.pond.averageDepthM);
    }

    ngOnInit(): void {
        this.route.paramMap.subscribe(() => this.init());
    }

    async init(): Promise<void> {
        const id = this.param('id');
        this.isNew = !id;
        this.tabChosen = false;
        if (this.isNew) {
            const [prefixes, profile] = await Promise.all([this.api.get<any[]>('Pond', 'GetPrefixes'), this.api.get<any>('FarmProfile', 'Get')]);
            this.prefixes = prefixes;
            this.farmSystem = profile.farmingSystem;
            this.pond = {
                id: '00000000-0000-0000-0000-000000000000',
                number: null,
                areaM2: null,
                averageDepthM: null,
                maxDepthM: null,
                purpose: 1,
                constructionType: 1,
                farmingSystemOverride: null,
                inletType: '',
                outletType: '',
                aerationSystem: '',
                notes: '',
            };
        } else {
            this.load(id);
        }
    }

    async load(id: string): Promise<void> {
        this.pond = await this.api.get<any>('Pond', 'Get', { id });
        this.original = { areaM2: Number(this.pond.areaM2), averageDepthM: Number(this.pond.averageDepthM), maxDepthM: Number(this.pond.maxDepthM) };
        this.editing = false;
        this.editEntry = null;
        this.prepAction = null;
        if (!this.tabChosen) {
            const wanted = this.query('tab');
            this.tab = this.tabs.some((x) => x.id === wanted) ? wanted! : 'overview';
            this.tabChosen = true;
        }
        this.setPageTitle(this.pond ? `Pond ${this.pond.pondCode}` : null);
        this.farmSystem = this.pond.farmFarmingSystem;
        const draft = FormDrafts.load<any>(this.prepDraftKey);
        this.prep = draft ? { ...this.newPrep(), ...draft.data } : this.newPrep();
        this.prepRestored = !!draft;
        this.newStatus = this.pond.manualStatus;
        this.statusNote = '';
    }

    async save(): Promise<void> {
        if (this.isNew) {
            let id: string;
            if (await this.act(async () => (id = await this.api.post<string>('Pond', 'Create', this.pond)))) {
                this.notifySaved('Pond created. Status: Available.');
                this.router.navigate(['/app/ponds', id], { replaceUrl: true });
            }
        } else if (await this.act(() => this.api.put('Pond', 'Update', this.pond))) {
            this.notifySaved('Pond saved.');
            this.load(this.pond.id);
        }
    }

    private newPrep(): any {
        return { observedAt: FarmTime.nowInput(), description: '', clientRequestId: newGuid() };
    }

    private get prepDraftKey(): string {
        return `prep.${this.pond?.id}`;
    }

    keepPrepDraft(): void {
        if (this.pond?.id && this.prep.description?.trim()) {
            FormDrafts.save(this.prepDraftKey, this.prep);
        }
    }

    discardPrep(): void {
        FormDrafts.clear(this.prepDraftKey);
        this.prep = this.newPrep();
        this.prepRestored = false;
    }

    async addPrep(): Promise<void> {
        const body = { pondId: this.pond.id, observedAt: FarmTime.fromLocalInput(this.prep.observedAt), description: this.prep.description, clientRequestId: this.prep.clientRequestId };
        if (await this.act(() => this.api.post('Pond', 'AddPreparationEntry', body))) {
            FormDrafts.clear(this.prepDraftKey);
            this.notifySaved('Preparation entry added.');
            this.load(this.pond.id);
        }
    }

    async markPrepared(): Promise<void> {
        if (!(await this.confirm(`Pond ${this.pond.pondCode} will show as Prepared and can be chosen in Create Batch (Stocking).`, 'Mark Prepared?'))) {
            return;
        }
        if (await this.act(() => this.api.post('Pond', 'MarkPrepared', { id: this.pond.id }))) {
            this.notifySaved('Pond marked Prepared.');
            this.load(this.pond.id);
        }
    }

    async archive(): Promise<void> {
        const message = `Pond ${this.pond.pondCode} will be hidden from the pond list and cannot be edited, prepared or stocked. Its batches and history are kept, and its number and code stay reserved. You can restore it later.`;
        if (!(await this.confirm(message, `Archive pond ${this.pond.pondCode}?`))) {
            return;
        }
        if (await this.act(() => this.api.post('Pond', 'Archive', { id: this.pond.id }))) {
            this.notifySaved(`Pond ${this.pond.pondCode} archived.`);
            this.load(this.pond.id);
        }
    }

    async restore(): Promise<void> {
        if (!(await this.confirm(`Pond ${this.pond.pondCode} will show in the pond list again and can be edited, prepared and stocked.`, `Restore pond ${this.pond.pondCode}?`))) {
            return;
        }
        if (await this.act(() => this.api.post('Pond', 'Restore', { id: this.pond.id }))) {
            this.notifySaved(`Pond ${this.pond.pondCode} restored.`);
            this.load(this.pond.id);
        }
    }

    async setStatus(): Promise<void> {
        if (await this.act(() => this.api.post('Pond', 'SetManualStatus', { id: this.pond.id, manualStatus: this.newStatus, note: this.statusNote }))) {
            this.notifySaved('Status changed.');
            this.load(this.pond.id);
        }
    }
}
