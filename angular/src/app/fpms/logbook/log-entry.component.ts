import { Component, OnInit } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime, newGuid } from '@shared/fpms/farm-time';
import { FormDrafts, OpsMemory } from '@shared/fpms/drafts';
import { logSummary } from '@shared/fpms/log-summary';
import { FPMS_IMPORTS } from '../shared-imports';
import { optimalState } from '../production/water-quality';

const DRAFT = 'log-entry';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header [narrow]="true" [title]="isNew ? 'Record operations' : (log ? (log.logType | label: L.LogTypes) + ' log' : 'Log entry')" [subtitle]="subtitle" [backLink]="isNew && !cameFromBatch ? undefined : batch ? ['/app/production/batches', batch.id] : '/app/logbook'" [backLabel]="batch ? 'Batch ' + batch.batchCode : 'Logbooks'"></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid fpms-narrow">
                @if (loading) {
                    <fpms-loading></fpms-loading>
                } @else {
                    @if (restoredAt) {
                        <div class="alert alert-info d-flex align-items-center" role="status">
                            <i class="fas fa-history mr-2"></i>
                            <span class="mr-auto">Your unsaved entry from {{ restoredAt | farmDateTime }} was restored.</span>
                            <button type="button" class="btn btn-sm btn-outline-secondary" (click)="discardDraft()">Discard it</button>
                        </div>
                    }

                    @if (result) {
                        <div class="alert" [class.alert-success]="!result.warnings.length" [class.alert-warning]="result.warnings.length" role="status">
                            <strong>{{ result.wasDuplicate ? 'Already saved' : 'Saved' }}.</strong>
                            @if (result.wasDuplicate) { This entry was already recorded; no duplicate was created. }
                            @for (w of result.warnings; track $index) {
                                <div><i class="fas fa-exclamation-triangle mr-1"></i>{{ w.message }}</div>
                            }
                            @if (result.indicators; as i) {
                                <div class="small mt-1">
                                    DOC {{ i.doc ?? '—' }} · ABW {{ i.currentAbwG | num: 2 }} g · feed {{ i.cumulativeFeedKg | num: 1 }} kg · FCR to date {{ i.fcrToDate | num: 2 }} · est. population {{ i.estimatedPopulation | num: 0 }}
                                </div>
                            }
                        </div>
                        @if (isNew) {
                            <div class="mb-3">
                                <button class="btn btn-primary mr-2" (click)="another(true)"><i class="fas fa-plus mr-1"></i>Another {{ form.logType | label: L.LogTypes }} log</button>
                                <button class="btn btn-outline-primary mr-2" (click)="another(false)">Different record</button>
                                <a class="btn btn-default" [routerLink]="['/app/production/batches', form.batchId]">Back to batch</a>
                            </div>
                        }
                    }

                    @if (log?.isVoided) {
                        <div class="alert alert-secondary small"><i class="fas fa-ban mr-1"></i>This entry was voided: {{ log.voidReason }}</div>
                    } @else if (readOnly && !isNew) {
                        <div class="alert alert-secondary small">
                            <i class="fas fa-lock mr-1"></i>
                            @if (batch?.isReadOnly) { The batch is Closed; its logs are read-only. }
                            @else if (historical) { This is a historical {{ log.logType | label: L.LogTypes }} entry kept for the record; it can no longer be edited. }
                            @else { You can only edit entries you recorded. Ask a Farm Owner to correct it. }
                        </div>
                    }

                    @if (!result || !isNew) {
                        <form #f="ngForm" (ngSubmit)="save()" (input)="keepDraft()" (change)="keepDraft()" novalidate>
                            <fieldset [disabled]="readOnly">
                                <div class="card">
                                    <div class="card-body">
                                        @if (isNew) {
                                            @if (inWizard) {
                                                <p class="mb-3"><strong>Pond (batch):</strong> {{ batch?.pondCode }} · {{ batch?.batchCode }}</p>
                                            } @else {
                                            <div class="form-row">
                                                    <div class="form-group col-md-6">
                                                        <label for="batch">Pond (batch) <span class="text-danger">*</span></label>
                                                        <select id="batch" class="form-control" name="batchId" [(ngModel)]="form.batchId" (ngModelChange)="onBatch()" required>
                                                            @for (x of openBatches; track x.id) {
                                                                <option [value]="x.id">{{ x.pondCode }} · {{ x.letter ? 'Batch ' + x.letter : x.batchCode }} ({{ x.status | label: L.BatchStatuses }})</option>
                                                            }
                                                        </select>
                                                        @if (!openBatches.length) {
                                                            <small class="form-text text-muted">No pond is stocked. Stock a batch first (Production → Create Batch).</small>
                                                        }
                                                    </div>
                                                </div>
                                            }
                                            @if (batch && !(inWizard && form.logType)) {
                                                <label class="d-block">What are you recording? <span class="text-danger">*</span></label>
                                                <div class="fpms-choice mb-3" role="radiogroup" aria-label="Record type">
                                                    @if (!inWizard) {
                                                        <a class="btn btn-outline-primary mr-1 mb-1" [routerLink]="['/app/logbook/feeding']" [queryParams]="{ batchId: batch.id }">
                                                            <i class="fas fa-utensils mr-1"></i>Feeding (AM/PM)
                                                        </a>
                                                    }
                                                    @for (o of allowedTypes; track o.value) {
                                                        <button type="button" class="btn mr-1 mb-1" role="radio" [attr.aria-checked]="form.logType === o.value"
                                                            [class.btn-primary]="form.logType === o.value" [class.btn-outline-primary]="form.logType !== o.value" (click)="pickType(o.value)">
                                                            {{ o.label }}
                                                        </button>
                                                    }
                                                </div>
                                            }
                                        } @else {
                                            <p class="mb-2"><strong>Pond (batch):</strong> {{ batch?.pondCode }} · {{ batch?.batchCode }} · cycle {{ batch?.cycleCode }}</p>
                                        }
                                        @if (form.logType) {
                                            <div class="form-row">
                                                <div class="form-group col-md-6 mb-0">
                                                    <label for="obs">Observed at <span class="text-danger">*</span></label>
                                                    <input id="obs" type="datetime-local" class="form-control" name="observedAt" [(ngModel)]="form.observedAt" required [max]="nowInput" />
                                                    <small class="form-text text-muted">The actual time (farm time). For paper sheets, enter the time written on the sheet.</small>
                                                </div>
                                            </div>
                                        }
                                    </div>
                                </div>

                                @if (historical && log) {
                                    <div class="card">
                                        <div class="card-header"><h3 class="card-title"><i class="fas fa-book" aria-hidden="true"></i>{{ log.logType | label: L.LogTypes }}</h3></div>
                                        <div class="card-body">{{ summary(log) || '—' }}</div>
                                    </div>
                                }

                                @if (form.logType === 2 || form.logType === 4) {
                                    <div class="card">
                                        <div class="card-header"><h3 class="card-title"><i class="fas fa-tint" aria-hidden="true"></i>Water quality readings</h3></div>
                                        <div class="card-body">
                                            <p class="small text-muted">Enter the parameters you measured (at least one{{ form.logType === 2 ? '; DO, temperature, pH and salinity are required before stocking' : '' }}). Out-of-range readings are saved and flagged.</p>
                                            <div class="form-row">
                                                @for (p of params; track p.code) {
                                                    <div class="form-group col-md-6 col-xl-3">
                                                        <label [for]="'r' + p.code">{{ p.name }} @if (form.logType === 2 && p.requiredAtStocking) { <span class="text-danger">*</span> }</label>
                                                        <div class="input-group">
                                                            <input [id]="'r' + p.code" type="number" step="any" inputmode="decimal" class="form-control" [name]="'r' + p.code" [(ngModel)]="readings[p.code]"
                                                                [min]="p.physicalMin" [max]="p.physicalMax" [required]="form.logType === 2 && p.requiredAtStocking" />
                                                            <div class="input-group-append"><span class="input-group-text">{{ p.unitCode }}</span></div>
                                                        </div>
                                                        <small class="form-text" [class.text-muted]="wq(p) === 'ok' || wq(p) === 'empty'" [class.text-warning-dark]="wq(p) === 'out'" [class.text-danger]="wq(p) === 'invalid'">
                                                            @switch (wq(p)) {
                                                                @case ('out') { <i class="fas fa-exclamation-triangle mr-1"></i>Out of range ({{ p.optimalRange }}) }
                                                                @case ('invalid') { Not physically possible ({{ p.physicalMin }}–{{ p.physicalMax }}) }
                                                                @default { {{ p.optimalRange }} · {{ p.guidanceFrequency }} }
                                                            }
                                                        </small>
                                                        @if (wq(p) === 'out' && advice(p); as a) {
                                                            <small class="form-text text-warning-dark">{{ a }}</small>
                                                        }
                                                    </div>
                                                }
                                            </div>
                                        </div>
                                    </div>
                                }

                                @if (form.logType === 5) {
                                    <div class="card">
                                        <div class="card-header"><h3 class="card-title"><i class="fas fa-chart-line" aria-hidden="true"></i>Growth sampling</h3></div>
                                        <div class="card-body">
                                            <div class="form-row">
                                                <div class="form-group col-md-4">
                                                    <label for="sc">Sample size (pcs) <span class="text-danger">*</span></label>
                                                    <input id="sc" type="number" min="1" step="1" inputmode="numeric" class="form-control" name="sc" [(ngModel)]="growth.sampleCount" required />
                                                </div>
                                                <div class="form-group col-md-4">
                                                    <label for="sw2">Total sample weight (g) <span class="text-danger">*</span></label>
                                                    <input id="sw2" type="number" min="0.01" step="0.01" inputmode="decimal" class="form-control" name="sw2" [(ngModel)]="growth.totalSampleWeightG" required />
                                                </div>
                                                <div class="form-group col-md-4">
                                                    <label>ABW (computed)</label>
                                                    <div class="form-control-plaintext font-weight-bold" [class.text-danger]="abw() > 60">
                                                        {{ abw() != null ? (abw() | num: 2) + ' g' : '—' }}
                                                    </div>
                                                    @if (abw() > 60) { <small class="text-danger">More than 60 g is not possible. Check the count and weight.</small> }
                                                </div>
                                            </div>
                                            <div class="form-row">
                                                <div class="form-group col-md-4 mb-2">
                                                    <label for="esp">Estimated survival (%)</label>
                                                    <input id="esp" type="number" min="0" max="100" step="0.1" inputmode="decimal" class="form-control" name="esp" [(ngModel)]="growth.estimatedSurvivalPct" placeholder="optional" />
                                                </div>
                                                <div class="col-md-8 d-flex align-items-center">
                                                    <small class="text-muted">From a cast-net or feeding-tray check. When given, the estimated population becomes PL stocked × this % (mortality and harvests after this sample are still taken off).</small>
                                                </div>
                                            </div>
                                            @if (growth.estimatedSurvivalPct != null && growth.estimatedSurvivalPct !== '' && (growth.estimatedSurvivalPct < 0 || growth.estimatedSurvivalPct > 100)) {
                                                <small class="d-block text-danger mb-1">Enter a percentage from 0 to 100.</small>
                                            }
                                            @if (log?.growthRateGPerDay != null) {
                                                <p class="small text-muted mb-0">Growth since previous sample: {{ log.growthRateGPerDay | num: 2 }} g/day</p>
                                            }
                                        </div>
                                    </div>
                                }

                                @if (form.logType === 6) {
                                    <div class="card">
                                        <div class="card-header"><h3 class="card-title"><i class="fas fa-flask" aria-hidden="true"></i>Chemical and supplement application</h3></div>
                                        <div class="card-body">
                                            @if (!productLots.length) {
                                                <div class="alert alert-info small">No supplement or chemical lots in stock records. Ask a Farm Owner to record a receipt under Stock.</div>
                                            }
                                            <label class="d-block">Products used <span class="text-danger">*</span></label>
                                            @for (u of chem.usages; track u.key) {
                                                <div class="form-row align-items-start mb-1">
                                                    <div class="col-7">
                                                        <select class="form-control" [name]="'ul' + u.key" [(ngModel)]="u.lotId" (ngModelChange)="prefillWithdrawal()" aria-label="Product lot" required>
                                                            @for (o of productLots; track o.lotId) {
                                                                <option [value]="o.lotId">{{ o.label }}</option>
                                                            }
                                                        </select>
                                                    </div>
                                                    <div class="col-3">
                                                        <div class="input-group">
                                                            <input type="number" min="0.001" step="0.001" inputmode="decimal" class="form-control" [name]="'uq' + u.key" [(ngModel)]="u.quantity" placeholder="Qty" aria-label="Quantity" required />
                                                            <div class="input-group-append"><span class="input-group-text">{{ lotUnit(u.lotId) }}</span></div>
                                                        </div>
                                                    </div>
                                                    <div class="col-2">
                                                        @if (chem.usages.length > 1) {
                                                            <button type="button" class="btn btn-outline-danger" (click)="removeUsage(u)" aria-label="Remove product"><i class="fas fa-times"></i></button>
                                                        }
                                                    </div>
                                                    @if (lotShort(u.lotId, u.quantity)) {
                                                        <div class="col-12"><small class="text-warning-dark"><i class="fas fa-exclamation-triangle mr-1"></i>More than this lot's stock on hand. It will save and the lot will be flagged for reconciliation.</small></div>
                                                    }
                                                    @if (lotExpired(u.lotId)) {
                                                        <div class="col-12"><small class="text-warning-dark"><i class="fas fa-exclamation-triangle mr-1"></i>This lot is past its expiry date.</small></div>
                                                    }
                                                </div>
                                            }
                                            <button type="button" class="btn btn-sm btn-outline-secondary mb-3" (click)="addUsage()" [disabled]="!productLots.length">
                                                <i class="fas fa-plus mr-1"></i>Add another product
                                            </button>
                                            <div class="form-group">
                                                <label for="rs">Reason</label>
                                                <input id="rs" class="form-control" name="rs" [(ngModel)]="chem.reason" maxlength="500" placeholder="e.g. low alkalinity, probiotic schedule, disinfection" />
                                            </div>
                                            <details class="mb-1" [open]="chemHasTreatment()">
                                                <summary class="small font-weight-bold mb-2">Treatment record (dosage, withdrawal period): optional</summary>
                                                <div class="form-row">
                                                    <div class="form-group col-6 col-md-3">
                                                        <label for="dz">Dosage</label>
                                                        <input id="dz" type="number" min="0" step="any" class="form-control" name="dz" [(ngModel)]="chem.dosage" />
                                                    </div>
                                                    <div class="form-group col-6 col-md-3">
                                                        <label for="du">Dosage unit</label>
                                                        <select id="du" class="form-control" name="du" [(ngModel)]="chem.dosageUnit">
                                                            <option value="">Select unit</option>
                                                            @for (u of dosageUnitOptions(); track u) {
                                                                <option [value]="u">{{ u }}</option>
                                                            }
                                                        </select>
                                                    </div>
                                                    <div class="form-group col-md-6">
                                                        <label for="me">Method</label>
                                                        <input id="me" class="form-control" name="me" [(ngModel)]="chem.method" maxlength="100" placeholder="broadcast, mixed in feed, bath" />
                                                    </div>
                                                </div>
                                                <div class="form-row">
                                                    <div class="form-group col-6 col-md-3">
                                                        <label for="ed">Last treatment day</label>
                                                        <input id="ed" type="date" class="form-control" name="ed" [(ngModel)]="chem.endDate" [min]="observedDate()" />
                                                    </div>
                                                    <div class="form-group col-6 col-md-3">
                                                        <label for="wd">Withdrawal days</label>
                                                        <input id="wd" type="number" min="0" max="365" step="1" class="form-control" name="wd" [(ngModel)]="chem.withdrawalDays" />
                                                        <small class="form-text text-muted">Pre-filled from the product when set.</small>
                                                    </div>
                                                    <div class="form-group col-md-6">
                                                        <label for="rx">Prescription / reference</label>
                                                        <input id="rx" class="form-control" name="rx" [(ngModel)]="chem.prescriptionRef" maxlength="100" placeholder="optional" />
                                                    </div>
                                                </div>
                                                @if (safeHarvestDate()) {
                                                    <p class="small mb-0"><i class="fas fa-prescription-bottle-alt mr-1"></i>Earliest safe harvest date: <strong>{{ safeHarvestDate() | farmDate }}</strong>. A harvest before then saves with a warning.</p>
                                                }
                                            </details>
                                        </div>
                                    </div>
                                }

                                @if (form.logType === 7) {
                                    <div class="card">
                                        <div class="card-header"><h3 class="card-title"><i class="fas fa-heartbeat" aria-hidden="true"></i>Mortality</h3></div>
                                        <div class="card-body">
                                            <div class="form-row">
                                                <div class="form-group col-md-4">
                                                    <label for="mc">Dead shrimp counted <span class="text-danger">*</span></label>
                                                    <input id="mc" type="number" min="0" step="1" inputmode="numeric" class="form-control" name="mc" [(ngModel)]="mortality.count" required />
                                                </div>
                                                <div class="form-group col-md-8">
                                                    <label class="d-block">Cause <span class="text-danger">*</span></label>
                                                    <div role="radiogroup" aria-label="Cause">
                                                        @for (o of Options.mortalityCause; track o.value) {
                                                            <button type="button" class="btn mr-1 mb-1" role="radio" [attr.aria-checked]="mortality.cause === o.value"
                                                                [class.btn-primary]="mortality.cause === o.value" [class.btn-outline-primary]="mortality.cause !== o.value" (click)="mortality.cause = o.value; keepDraft()">
                                                                {{ o.label }}
                                                            </button>
                                                        }
                                                    </div>
                                                </div>
                                            </div>
                                            @if (mortality.cause === 1) {
                                                <div class="form-group">
                                                    <label for="dn">Disease name <span class="text-danger">*</span></label>
                                                    <input id="dn" class="form-control" name="dn" [(ngModel)]="mortality.diseaseName" maxlength="100" required list="diseases" placeholder="e.g. WSSV, AHPND, EHP" />
                                                    <datalist id="diseases"><option value="WSSV"></option><option value="AHPND"></option><option value="EHP"></option><option value="IMNV"></option><option value="Vibriosis"></option></datalist>
                                                </div>
                                            }
                                            @if (mortality.cause === 7) {
                                                <div class="form-group">
                                                    <label for="oc">Describe the cause <span class="text-danger">*</span></label>
                                                    <input id="oc" class="form-control" name="oc" [(ngModel)]="mortality.otherCause" maxlength="500" required placeholder="e.g. predation by birds, molting stress" />
                                                </div>
                                            }
                                            <div class="form-group">
                                                <label for="sy">Observed symptoms</label>
                                                <input id="sy" class="form-control" name="sy" [(ngModel)]="mortality.symptoms" maxlength="500" placeholder="e.g. red discoloration, soft shell, empty gut" />
                                            </div>
                                            <div class="form-group mb-0">
                                                <label for="ra">Response action</label>
                                                <input id="ra" class="form-control" name="ra" [(ngModel)]="mortality.responseAction" maxlength="500" />
                                            </div>
                                        </div>
                                    </div>
                                }

                                @if (form.logType) {
                                    <div class="card">
                                        <div class="card-body">
                                            <div class="form-group mb-0">
                                                <label for="notes">{{ form.logType === 9 ? 'Note' : 'Notes' }} @if (form.logType === 9) { <span class="text-danger">*</span> }</label>
                                                <textarea id="notes" rows="3" class="form-control" name="notes" [(ngModel)]="form.notes" maxlength="2000" [required]="form.logType === 9"></textarea>
                                            </div>
                                        </div>
                                    </div>
                                }

                                @if (log && !isNew) {
                                    <p class="small text-muted">
                                        Recorded by {{ log.recordedBy }} on {{ log.creationTime | farmDateTime }}{{ log.lastModificationTime ? ', last edited ' + (log.lastModificationTime | farmDateTime) : '' }}.
                                        @for (u of log.usages; track $index) {
                                            <br />Stock used: {{ u.label }} {{ u.quantity | num: 3 }} {{ u.unit }}
                                        }
                                    </p>
                                }
                            </fieldset>
                            @if (!readOnly && form.logType) {
                                <button type="submit" class="btn btn-primary btn-lg mr-2" [disabled]="busy() || !f.form.valid || !canSubmit()">
                                    @if (busy()) {
                                        <i class="fas fa-spinner fa-spin mr-1"></i>
                                    }
                                    {{ isNew ? 'Save' : 'Save changes' }}
                                </button>
                                <fpms-form-hint [form]="f"></fpms-form-hint>
                            }
                        </form>

                        @if (!isNew && log?.canVoid && !log?.isVoided) {
                            <div class="card card-outline card-danger mt-4">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-ban" aria-hidden="true"></i>Void this entry</h3></div>
                                <div class="card-body">
                                    <p class="small text-muted">Voiding hides the entry from records and indicators{{ log.usages?.length ? ' and returns its products to stock with reversing movements' : '' }}. It is kept in the audit history with your reason. You can then record it again correctly.</p>
                                    <div class="form-group">
                                        <label for="vr">Reason <span class="text-danger">*</span></label>
                                        <input id="vr" class="form-control" name="vr" [(ngModel)]="voidReason" maxlength="500" placeholder="e.g. entered on the wrong pond" />
                                    </div>
                                    <button type="button" class="btn btn-outline-danger" (click)="voidEntry()" [disabled]="busy() || !voidReason.trim()">Void entry</button>
                                </div>
                            </div>
                        }
                    }
                }
            </div>
        </section>
    `,
})
export class LogEntryComponent extends FpmsPage implements OnInit {
    protected override keepsDrafts = true;
    loading = true;
    isNew = true;
    readOnly = false;
    historical = false;
    log: any = null;
    batch: any = null;
    openBatches: any[] = [];
    params: any[] = [];
    productLots: any[] = [];
    allowedTypes: { value: number; label: string }[] = [];
    result: any = null;
    restoredAt: string | null = null;
    voidReason = '';
    readonly nowInput = FarmTime.nowInput();

    form: any = {};
    readings: Record<string, number | null> = {};
    growth: any = {};
    chem: any = {};
    mortality: any = {};
    private usageKey = 0;

    readonly cameFromBatch = !!this.query('batchId');

    readonly inWizard = !!this.embedded;

    get subtitle(): string {
        if (this.isNew) {
            return 'Water quality, growth sampling, chemicals, mortality and notes for one pond.';
        }
        return this.batch ? `Pond ${this.batch.pondCode} · ${this.batch.batchCode}` : 'Choose the pond';
    }

    ngOnInit(): void {
        FormDrafts.purgeExpired();
        this.route.paramMap.subscribe(() => this.init());
    }

    async init(): Promise<void> {
        this.loading = true;
        const id = this.param('id');
        this.isNew = !id;
        this.result = null;
        this.restoredAt = null;
        this.log = null;
        this.historical = false;
        const [params, suppLots, chemLots] = await Promise.all([
            this.api.get<any[]>('Log', 'GetParameters'),
            this.api.get<any[]>('Inventory', 'GetLotOptions', { category: 2 }),
            this.api.get<any[]>('Inventory', 'GetLotOptions', { category: 3 }),
        ]);
        this.params = params.filter((p) => p.isActive);
        this.productLots = this.sortLots([...suppLots, ...chemLots]);
        this.resetDetails();

        if (this.isNew) {
            await this.initNew();
        } else {
            await this.loadExisting(id);
        }
        this.loading = false;
    }

    private async initNew(): Promise<void> {
        const batches = await this.api.get<any[]>('Batch', 'GetAll', { openOnly: true });
        this.openBatches = batches.sort((x, y) => String(x.pondCode).localeCompare(String(y.pondCode), undefined, { numeric: true }));

        const draft = this.query('batchId') ? null : FormDrafts.load<any>(DRAFT);
        const remembered = OpsMemory.get();
        const batchId = this.query('batchId') ?? draft?.data.form.batchId ?? remembered.batchId ?? null;
        const fromBatch = this.openBatches.find((b) => b.id === batchId);
        const type = Number(this.query('logType')) || null;
        const day = this.query('date');
        const observedAt = day && /^\d{4}-\d{2}-\d{2}$/.test(day) && day < this.today ? `${day}T${FarmTime.nowInput().substring(11)}` : FarmTime.nowInput();
        this.form = { batchId: fromBatch?.id ?? null, logType: type, observedAt, notes: '', clientRequestId: newGuid() };
        if (!this.form.batchId) {
            this.form.batchId = this.openBatches[0]?.id ?? null;
        }
        await this.onBatch(type);

        if (draft && draft.data.form.batchId === this.form.batchId) {
            this.applyDraft(draft.data);
            this.restoredAt = new Date(draft.savedAt).toISOString();
        }
    }

    private readonly baseDosageUnits = ['ppm', 'mg/L', 'g/m³', 'kg/ha', 'g/kg feed', 'mL/m³', 'L/ha'];

    dosageUnitOptions(): string[] {
        const cur = this.chem?.dosageUnit;
        return cur && !this.baseDosageUnits.includes(cur) ? [...this.baseDosageUnits, cur] : this.baseDosageUnits;
    }

    private resetDetails(): void {
        this.readings = {};
        this.params.forEach((p) => (this.readings[p.code] = null));
        this.growth = { sampleCount: null, totalSampleWeightG: null, estimatedSurvivalPct: null };
        this.chem = { reason: '', usages: [this.newUsage()], dosage: null, dosageUnit: '', method: '', endDate: '', withdrawalDays: null, prescriptionRef: '' };
        this.prefillWithdrawal();
        this.mortality = { count: null, cause: null, diseaseName: '', otherCause: '', symptoms: '', responseAction: '' };
    }

    private sortLots(lots: any[]): any[] {
        return [...lots].sort((a, b) => (a.quantityOnHand > 0 ? 0 : 1) - (b.quantityOnHand > 0 ? 0 : 1) || String(a.expiryDate ?? '9999').localeCompare(String(b.expiryDate ?? '9999')));
    }

    private newUsage(lotId?: string, quantity?: number | null): any {
        return { key: ++this.usageKey, lotId: lotId ?? this.productLots[0]?.lotId ?? '', quantity: quantity ?? null };
    }

    prefillWithdrawal(): void {
        if (this.chem.withdrawalDays != null && this.chem.withdrawalDays !== '') {
            return;
        }
        const days = (this.chem.usages ?? [])
            .map((u: any) => this.productLots.find((o) => o.lotId === u.lotId)?.defaultWithdrawalDays)
            .filter((d: any) => d != null);
        if (days.length) {
            this.chem.withdrawalDays = Math.max(...days);
        }
    }

    chemHasTreatment(): boolean {
        const c = this.chem ?? {};
        return c.dosage != null || !!c.method || !!c.endDate || (c.withdrawalDays != null && c.withdrawalDays !== '') || !!c.prescriptionRef;
    }

    observedDate(): string {
        return this.form?.observedAt ? String(this.form.observedAt).substring(0, 10) : '';
    }

    safeHarvestDate(): string | null {
        const days = Number(this.chem?.withdrawalDays);
        if (!days || days <= 0) {
            return null;
        }
        const base = this.chem.endDate || this.observedDate();
        return base ? FarmTime.addDays(base, days) : null;
    }

    addUsage(): void {
        this.chem.usages.push(this.newUsage());
        this.keepDraft();
    }

    removeUsage(u: any): void {
        this.chem.usages = this.chem.usages.filter((x: any) => x !== u);
        this.keepDraft();
    }

    async onBatch(preferredType?: number | null): Promise<void> {
        if (!this.form.batchId) {
            this.batch = null;
            this.allowedTypes = [];
            return;
        }
        this.batch = await this.api.get<any>('Batch', 'Get', { id: this.form.batchId });
        if (this.isNew) {
            OpsMemory.set({ cycleId: this.batch.cycleId, batchId: this.batch.id });
        }
        const allowed = (this.batch.allowedLogTypes as string[]).map((n) => this.L.LogTypeNames[n]);
        this.allowedTypes = this.Options.newLogType.filter((o) => allowed.includes(o.value));
        const wanted = preferredType ?? this.form.logType;
        this.form.logType = this.allowedTypes.some((t) => t.value === wanted) ? wanted : null;
        this.keepDraft();
    }

    pickType(type: number): void {
        this.form.logType = type;
        this.keepDraft();
    }

    private async loadExisting(id: string): Promise<void> {
        const l = await this.api.get<any>('Log', 'Get', { id });
        this.log = l;
        this.batch = await this.api.get<any>('Batch', 'Get', { id: l.batchId });
        this.historical = [1, 3, 8].includes(l.logType);
        this.readOnly = !l.canEdit || this.batch.isReadOnly || this.historical || !!l.isVoided;
        this.voidReason = '';
        this.form = {
            id: l.id,
            batchId: l.batchId,
            logType: this.historical ? null : l.logType,
            observedAt: FarmTime.toLocalInput(l.observedAtUtc),
            notes: l.notes ?? '',
            clientRequestId: l.clientRequestId,
        };
        (l.measurements ?? []).forEach((m: any) => (this.readings[m.parameterCode] = m.value));
        if (l.growth) {
            this.growth = { ...l.growth };
        }
        if (l.chemicalApplication) {
            this.chem = {
                reason: l.chemicalApplication.reason ?? '',
                dosage: l.chemicalApplication.dosage ?? null,
                dosageUnit: l.chemicalApplication.dosageUnit ?? '',
                method: l.chemicalApplication.method ?? '',
                endDate: l.chemicalApplication.endDate ?? '',
                withdrawalDays: l.chemicalApplication.withdrawalDays ?? null,
                prescriptionRef: l.chemicalApplication.prescriptionRef ?? '',
                usages: (l.chemicalApplication.usages ?? []).map((u: any) => this.newUsage(u.lotId, u.quantity)),
            };
            for (const u of l.usages ?? []) {
                if (!this.productLots.some((o) => o.lotId === u.lotId)) {
                    this.productLots = [...this.productLots, { lotId: u.lotId, label: u.label, unit: u.unit, quantityOnHand: 0 }];
                }
            }
        }
        if (l.mortality) {
            this.mortality = { ...l.mortality, diseaseName: l.mortality.diseaseName ?? '', otherCause: l.mortality.otherCause ?? '' };
        }
    }

    summary(l: any): string {
        return logSummary(l);
    }

    wq(p: any): 'empty' | 'ok' | 'out' | 'invalid' {
        return optimalState(p, this.readings[p.code]);
    }

    advice(p: any): string | null {
        const v = Number(this.readings[p.code]);
        if (p.optimalMin != null && (p.optimalMinExclusive ? v <= p.optimalMin : v < p.optimalMin)) {
            return p.lowRecommendation || null;
        }
        if (p.optimalMax != null && (p.optimalMaxExclusive ? v >= p.optimalMax : v > p.optimalMax)) {
            return p.highRecommendation || null;
        }
        return null;
    }

    abw(): number | null {
        const n = Number(this.growth.sampleCount);
        const w = Number(this.growth.totalSampleWeightG);
        return n > 0 && w > 0 ? w / n : null;
    }

    private lot(lotId: string): any {
        return this.productLots.find((o) => o.lotId === lotId);
    }

    lotUnit(lotId: string): string {
        return this.lot(lotId)?.unit ?? '';
    }

    lotShort(lotId: string, qty: number | null): boolean {
        const lot = this.lot(lotId);
        return this.isNew && !!lot && !!qty && Number(qty) > lot.quantityOnHand;
    }

    lotExpired(lotId: string): boolean {
        const expiry = this.lot(lotId)?.expiryDate;
        return !!expiry && expiry < FarmTime.today();
    }

    canSubmit(): boolean {
        switch (this.form.logType) {
            case 2:
            case 4: {
                const entered = this.params.filter((p) => this.wq(p) !== 'empty');
                return entered.length > 0 && !entered.some((p) => this.wq(p) === 'invalid');
            }
            case 5:
                return (this.abw() ?? 0) > 0 && (this.abw() ?? 0) <= 60;
            case 6:
                return this.chem.usages.length > 0 && this.chem.usages.every((u: any) => u.lotId && Number(u.quantity) > 0);
            case 7:
                return !!this.mortality.cause && Number(this.mortality.count) >= 0;
            default:
                return !!this.form.logType;
        }
    }

    private buildBody(): any {
        const body: any = {
            id: this.form.id ?? null,
            batchId: this.form.batchId,
            logType: this.form.logType,
            observedAt: FarmTime.fromLocalInput(this.form.observedAt),
            notes: this.form.notes,
            clientRequestId: this.form.clientRequestId,
        };
        switch (this.form.logType) {
            case 2:
            case 4:
                body.readings = this.params.filter((p) => this.wq(p) !== 'empty').map((p) => ({ parameterCode: p.code, value: Number(this.readings[p.code]) }));
                break;
            case 5:
                body.growth = {
                    ...this.growth,
                    estimatedSurvivalPct: this.growth.estimatedSurvivalPct === '' || this.growth.estimatedSurvivalPct == null ? null : Number(this.growth.estimatedSurvivalPct),
                };
                break;
            case 6:
                body.chemicalApplication = {
                    reason: this.chem.reason,
                    dosage: this.chem.dosage === '' || this.chem.dosage == null ? null : Number(this.chem.dosage),
                    dosageUnit: this.chem.dosageUnit,
                    method: this.chem.method,
                    endDate: this.chem.endDate || null,
                    withdrawalDays: this.chem.withdrawalDays === '' || this.chem.withdrawalDays == null ? null : Number(this.chem.withdrawalDays),
                    prescriptionRef: this.chem.prescriptionRef,
                    usages: this.chem.usages.map((u: any) => ({ lotId: u.lotId, quantity: Number(u.quantity) })),
                };
                break;
            case 7:
                body.mortality = {
                    count: Number(this.mortality.count),
                    cause: this.mortality.cause,
                    diseaseName: this.mortality.cause === 1 ? this.mortality.diseaseName : null,
                    otherCause: this.mortality.cause === 7 ? this.mortality.otherCause : null,
                    symptoms: this.mortality.symptoms,
                    responseAction: this.mortality.responseAction,
                };
                break;
        }
        return body;
    }

    async save(): Promise<void> {
        const body = this.buildBody();
        let res: any;
        const ok = await this.act(async () => (res = this.isNew ? await this.api.post<any>('Log', 'Create', body) : await this.api.put<any>('Log', 'Update', body)));
        if (!ok) {
            return;
        }
        this.result = res;
        if (this.isNew) {
            FormDrafts.clear(DRAFT);
            this.restoredAt = null;
            window.scrollTo({ top: 0 });
        } else {
            this.log = res.log;
            this.notifySaved('Log entry updated.');
        }
    }

    async voidEntry(): Promise<void> {
        if (!(await this.confirm(`Void this ${this.L.LogTypes[this.log.logType]} entry? Reason: "${this.voidReason.trim()}".`, 'Void entry?'))) {
            return;
        }
        if (await this.act(() => this.api.post('Log', 'Void', { id: this.log.id, reason: this.voidReason.trim() }))) {
            this.notifySaved('Entry voided.');
            this.router.navigate(['/app/production/batches', this.log.batchId], { replaceUrl: true });
        }
    }

    keepDraft(): void {
        if (!this.isNew || this.result || !this.form.batchId) {
            return;
        }
        FormDrafts.save(DRAFT, {
            form: this.form,
            readings: this.readings,
            growth: this.growth,
            chem: { reason: this.chem.reason, usages: this.chem.usages.map((u: any) => ({ lotId: u.lotId, quantity: u.quantity })) },
            mortality: this.mortality,
        });
    }

    private applyDraft(d: any): void {
        this.form = { ...this.form, ...d.form };
        this.readings = { ...this.readings, ...(d.readings ?? {}) };
        this.growth = { ...this.growth, ...(d.growth ?? {}) };
        this.mortality = { ...this.mortality, ...(d.mortality ?? {}) };
        const usages = (d.chem?.usages ?? []).filter((u: any) => this.productLots.some((o) => o.lotId === u.lotId));
        this.chem = { reason: d.chem?.reason ?? '', usages: usages.length ? usages.map((u: any) => this.newUsage(u.lotId, u.quantity)) : [this.newUsage()] };
        if (!this.allowedTypes.some((t) => t.value === this.form.logType)) {
            this.form.logType = null;
        }
    }

    discardDraft(): void {
        FormDrafts.clear(DRAFT);
        this.restoredAt = null;
        this.resetDetails();
        this.form = { ...this.form, logType: null, observedAt: FarmTime.nowInput(), notes: '', clientRequestId: newGuid() };
    }

    async another(sameType: boolean): Promise<void> {
        const type = sameType ? this.form.logType : null;
        this.result = null;
        const [suppLots, chemLots] = await Promise.all([
            this.api.get<any[]>('Inventory', 'GetLotOptions', { category: 2 }),
            this.api.get<any[]>('Inventory', 'GetLotOptions', { category: 3 }),
        ]);
        this.productLots = this.sortLots([...suppLots, ...chemLots]);
        this.resetDetails();
        this.form = { ...this.form, observedAt: FarmTime.nowInput(), notes: '', clientRequestId: newGuid(), logType: type };
        await this.onBatch(type);
    }
}
