import { Component, HostListener, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
const CUSTOM = 'custom';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Permits and licenses" subtitle="Tracked by reference number and expiry date, soonest-expiring first.">
            <button class="btn btn-primary mr-1" (click)="startCreate()"><i class="fas fa-plus mr-1"></i>Add permit</button>
            <button class="btn btn-outline-secondary" [class.active]="showConfig" (click)="showConfig = !showConfig"><i class="fas fa-tags mr-1"></i>Presets</button>
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (status(); as s) {
                    <div class="row">
                        @for (t of tiles; track t.value) {
                            <div class="col-6 col-md-3">
                                <button type="button" class="fpms-status-tile t-{{ t.tone }}" [class.active]="filterStatus === t.value" [attr.aria-pressed]="filterStatus === t.value" (click)="toggleStatus(t.value)">
                                    <div class="v">{{ s[t.count] }}</div>
                                    <div class="l">{{ t.label }}</div>
                                </button>
                            </div>
                        }
                    </div>

                    <div class="card">
                        <div class="card-header"><h3 class="card-title"><i class="far fa-file-alt" aria-hidden="true"></i>Required permits</h3></div>
                        <div class="card-body py-2">
                            @if (!s.requiredPermits.length) {
                                <span class="text-muted small">No preset is marked "Required". Mark one under Categories &amp; presets to get reminders.</span>
                            }
                            <div class="d-flex flex-wrap">
                                @for (r of s.requiredPermits; track r.presetId) {
                                    <div class="mr-4 mb-1">
                                        <i class="fas mr-1" [class.fa-check-circle]="r.isSatisfied" [class.text-success]="r.isSatisfied" [class.fa-exclamation-circle]="!r.isSatisfied" [class.text-warning]="!r.isSatisfied" aria-hidden="true"></i>
                                        {{ r.name }}
                                        <span class="small text-muted">
                                            @if (r.isSatisfied) {
                                                {{ r.validUntil ? 'valid until ' + (r.validUntil | farmDate) : 'no expiry' }}
                                            } @else {
                                                no active record ·
                                                <a href="javascript:;" (click)="startCreate(r.presetId)">add</a>
                                            }
                                        </span>
                                    </div>
                                }
                            </div>
                            @if (s.missingPermits.length) {
                                <p class="small text-muted mb-0 mt-1">
                                    <i class="fas fa-info-circle mr-1"></i>Reminder only: production is never blocked. Stocking the first batch of a cycle repeats this reminder.
                                </p>
                            }
                        </div>
                    </div>
                }

                @if (showConfig) {
                    <div class="card card-outline card-primary">
                        <div class="card-header"><h3 class="card-title"><i class="far fa-file-alt" aria-hidden="true"></i>Categories and preset titles</h3></div>
                        <div class="card-body p-0">
                            @for (c of categories(); track c.id) {
                                <div class="border-bottom p-2">
                                    <div class="form-row align-items-center">
                                        <div class="col-md-5 mb-1">
                                            <input class="form-control form-control-sm font-weight-bold" [(ngModel)]="c.name" maxlength="100" [attr.aria-label]="'Category name'" />
                                        </div>
                                        <div class="col-auto mb-1">
                                            <div class="custom-control custom-checkbox">
                                                <input type="checkbox" class="custom-control-input" [id]="'rx-' + c.id" [(ngModel)]="c.requiresExpiryDate" />
                                                <label class="custom-control-label small" [attr.for]="'rx-' + c.id">Requires expiry date</label>
                                            </div>
                                        </div>
                                        <div class="col-auto mb-1">
                                            <div class="custom-control custom-checkbox">
                                                <input type="checkbox" class="custom-control-input" [id]="'ca-' + c.id" [(ngModel)]="c.isActive" />
                                                <label class="custom-control-label small" [attr.for]="'ca-' + c.id">Active</label>
                                            </div>
                                        </div>
                                        <div class="col-auto mb-1 small text-muted">{{ c.documentCount }} current</div>
                                        <div class="col text-right mb-1">
                                            <button class="btn btn-sm btn-outline-primary" [disabled]="busy() || !c.name" (click)="saveCategory(c)">Save category</button>
                                        </div>
                                    </div>
                                    <div class="table-responsive">
                                    <table class="table table-sm mb-0 mt-1">
                                        <thead class="small text-muted">
                                            <tr><th>Preset title</th><th>Default issuing agency</th><th title="Show a reminder when there is no active record">Required</th><th>Active</th><th><span class="sr-only">Actions</span></th></tr>
                                        </thead>
                                        <tbody>
                                            @for (p of c.presets; track p.id) {
                                                <tr>
                                                    <td><input class="form-control form-control-sm" [(ngModel)]="p.name" maxlength="200" aria-label="Preset title" /></td>
                                                    <td><input class="form-control form-control-sm" [(ngModel)]="p.defaultIssuingAgency" maxlength="200" aria-label="Default issuing agency" /></td>
                                                    <td><input type="checkbox" [(ngModel)]="p.isRequired" [attr.aria-label]="'Required: ' + p.name" /></td>
                                                    <td><input type="checkbox" [(ngModel)]="p.isActive" [attr.aria-label]="'Active: ' + p.name" /></td>
                                                    <td class="text-right"><button class="btn btn-sm btn-outline-primary" [disabled]="busy() || !p.name" (click)="savePreset(p)">Save</button></td>
                                                </tr>
                                            }
                                            <tr>
                                                <td><input class="form-control form-control-sm" placeholder="New preset title" [(ngModel)]="newPreset[c.id].name" maxlength="200" aria-label="New preset title" /></td>
                                                <td><input class="form-control form-control-sm" placeholder="Agency (optional)" [(ngModel)]="newPreset[c.id].defaultIssuingAgency" maxlength="200" aria-label="New preset agency" /></td>
                                                <td><input type="checkbox" [(ngModel)]="newPreset[c.id].isRequired" aria-label="New preset required" /></td>
                                                <td></td>
                                                <td class="text-right"><button class="btn btn-sm btn-primary" [disabled]="busy() || !newPreset[c.id].name" (click)="savePreset(newPreset[c.id], true)">Add</button></td>
                                            </tr>
                                        </tbody>
                                    </table>
                                    </div>
                                </div>
                            }
                            <div class="p-2 form-row align-items-center">
                                <div class="col-md-5 mb-1"><input class="form-control form-control-sm" placeholder="New category name" aria-label="New category name" [(ngModel)]="newCategory.name" maxlength="100" /></div>
                                <div class="col-auto mb-1">
                                    <div class="custom-control custom-checkbox">
                                        <input type="checkbox" class="custom-control-input" id="rx-new" [(ngModel)]="newCategory.requiresExpiryDate" />
                                        <label class="custom-control-label small" for="rx-new">Requires expiry date</label>
                                    </div>
                                </div>
                                <div class="col text-right mb-1"><button class="btn btn-sm btn-primary" [disabled]="busy() || !newCategory.name" (click)="saveCategory(newCategory, true)">Add category</button></div>
                            </div>
                        </div>
                        <div class="card-footer small text-muted">"Requires expiry date" stops records in that category from being marked perpetual. "Required" presets show a reminder when no active record exists. Changes are recorded in the activity log.</div>
                    </div>
                }

                <div class="card">
                    <div class="card-header">
                        <div class="form-row align-items-center">
                            <div class="col-md-4 mb-1">
                                <input type="search" class="form-control form-control-sm" placeholder="Search title, reference number or agency" aria-label="Search" [(ngModel)]="search" (ngModelChange)="searchChanged()" />
                            </div>
                            <div class="col-6 col-md-3 mb-1">
                                <select class="form-control form-control-sm" aria-label="Filter by category" [(ngModel)]="filterCategory" (ngModelChange)="loadDocuments()">
                                    <option [ngValue]="null">All categories</option>
                                    @for (c of categories(); track c.id) {
                                        <option [ngValue]="c.id">{{ c.name }}</option>
                                    }
                                </select>
                            </div>
                            <div class="col-6 col-md-2 mb-1">
                                <select class="form-control form-control-sm" aria-label="Filter by status" [(ngModel)]="filterStatus" (ngModelChange)="loadDocuments()">
                                    <option [ngValue]="null">All statuses</option>
                                    @for (t of tiles; track t.value) {
                                        <option [ngValue]="t.value">{{ t.label }}</option>
                                    }
                                </select>
                            </div>
                            <div class="col-auto mb-1">
                                <div class="custom-control custom-checkbox">
                                    <input type="checkbox" class="custom-control-input" id="arch" [(ngModel)]="includeArchived" (ngModelChange)="loadDocuments()" />
                                    <label class="custom-control-label small" for="arch">Show archived</label>
                                </div>
                            </div>
                            @if (filtered) {
                                <div class="col-auto mb-1"><button class="btn btn-sm btn-link" (click)="clearFilters()">Clear filters</button></div>
                            }
                        </div>
                    </div>
                    <div class="card-body p-0 table-responsive">
                        @if (!documents()) {
                            <fpms-loading></fpms-loading>
                        } @else if (!documents()!.length) {
                            @if (filtered) {
                                <fpms-empty text="No records match these filters." icon="fas fa-filter"></fpms-empty>
                            } @else {
                                <fpms-empty text="No permits or licenses recorded yet." icon="far fa-file-alt">
                                    <button class="btn btn-sm btn-primary" (click)="startCreate()">Add the first permit</button>
                                </fpms-empty>
                            }
                        } @else {
                            <table class="table table-hover mb-0">
                                <thead>
                                    <tr><th>Document</th><th>Reference no.</th><th>Issuing agency</th><th>Issued</th><th>Expires</th><th>Status</th><th class="text-right">Actions</th></tr>
                                </thead>
                                <tbody>
                                    @for (d of documents(); track d.id) {
                                        <tr [class.text-muted]="d.isArchived">
                                            <td>
                                                <a href="javascript:;" class="font-weight-bold" (click)="view(d)">{{ d.title }}</a>
                                                <div class="small text-muted">{{ d.categoryName }}</div>
                                            </td>
                                            <td class="text-nowrap">{{ d.documentNumber }}</td>
                                            <td class="small">{{ d.issuingAgency }}</td>
                                            <td class="text-nowrap">{{ d.issueDate | farmDate }}</td>
                                            <td class="text-nowrap">
                                                @if (d.isPerpetual) {
                                                    <span class="text-muted">Perpetual</span>
                                                } @else {
                                                    {{ d.expiryDate | farmDate }}
                                                    <div class="small text-muted">{{ relative(d.daysToExpiry) }}</div>
                                                }
                                            </td>
                                            <td>
                                                <fpms-badge [text]="d.expiryStatus | label: L.ExpiryStatuses"></fpms-badge>
                                                @if (d.isArchived) { <fpms-badge text="Archived" tone="secondary"></fpms-badge> }
                                            </td>
                                            <td class="text-right text-nowrap">
                                                <button class="btn btn-sm btn-outline-primary mr-1" (click)="view(d)"><i class="far fa-eye mr-1"></i>View</button>
                                                @if (!d.isArchived) {
                                                    <button class="btn btn-sm btn-outline-secondary mr-1" title="Edit" aria-label="Edit" (click)="startEdit(d)"><i class="fas fa-pen"></i></button>
                                                    @if (!d.isPerpetual) {
                                                        <button class="btn btn-sm mr-1" [class.btn-warning]="d.expiryStatus >= 2" [class.btn-outline-secondary]="d.expiryStatus < 2" title="Record a renewal" (click)="startRenew(d)"><i class="fas fa-redo mr-1"></i>Renew</button>
                                                    }
                                                }
                                                <button class="btn btn-sm btn-outline-secondary" [title]="d.isArchived ? 'Restore' : 'Archive'" [attr.aria-label]="d.isArchived ? 'Restore' : 'Archive'" (click)="archive(d)">
                                                    <i class="fas" [class.fa-archive]="!d.isArchived" [class.fa-undo]="d.isArchived"></i>
                                                </button>
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        }
                    </div>
                </div>
            </div>
        </section>

        @if (viewing(); as v) {
            <div class="fpms-dialog-backdrop" (click)="closeView()">
                <div class="fpms-dialog fpms-dialog-sm" role="dialog" aria-modal="true" aria-labelledby="viewTitle" (click)="$event.stopPropagation()">
                    <div class="fpms-dialog-head">
                        <h2 id="viewTitle" class="mr-auto">{{ v.title }}</h2>
                        <button type="button" class="fpms-dialog-close" aria-label="Close" (click)="closeView()"><span aria-hidden="true">&times;</span></button>
                    </div>
                    <div class="fpms-dialog-body">
                        <div class="mb-3">
                            <fpms-badge [text]="v.expiryStatus | label: L.ExpiryStatuses"></fpms-badge>
                            @if (v.isArchived) { <fpms-badge text="Archived" tone="secondary"></fpms-badge> }
                            @if (!v.isPerpetual) { <span class="small text-muted ml-1">{{ relative(v.daysToExpiry) }}</span> }
                        </div>
                        <dl class="row fpms-dl mb-0">
                            <dt class="col-sm-4">Category</dt><dd class="col-sm-8">{{ v.categoryName }}</dd>
                            <dt class="col-sm-4">Reference no.</dt><dd class="col-sm-8"><strong>{{ v.documentNumber }}</strong></dd>
                            <dt class="col-sm-4">Issuing agency</dt><dd class="col-sm-8">{{ v.issuingAgency || '—' }}</dd>
                            <dt class="col-sm-4">Issue date</dt><dd class="col-sm-8">{{ (v.issueDate | farmDate) || '—' }}</dd>
                            <dt class="col-sm-4">Expiry date</dt><dd class="col-sm-8">{{ v.isPerpetual ? 'Perpetual / no expiry date' : (v.expiryDate | farmDate) }}</dd>
                            <dt class="col-sm-4">Notes / location</dt><dd class="col-sm-8" style="white-space: pre-line">{{ v.notes || '—' }}</dd>
                            @if (v.renewedFromId) {
                                <dt class="col-sm-4">Renewal of</dt><dd class="col-sm-8"><a href="javascript:;" (click)="openById(v.renewedFromId)">earlier record</a></dd>
                            }
                            <dt class="col-sm-4">Recorded</dt><dd class="col-sm-8 small">{{ v.creationTime | farmDateTime }} by {{ v.recordedBy || '—' }}</dd>
                            @if (v.lastModificationTime) {
                                <dt class="col-sm-4">Last changed</dt><dd class="col-sm-8 small">{{ v.lastModificationTime | farmDateTime }} by {{ v.lastModifiedBy || '—' }}</dd>
                            }
                        </dl>
                    </div>
                    <div class="fpms-dialog-foot">
                        @if (!v.isArchived) {
                            <button class="btn btn-outline-secondary mr-auto" (click)="closeView(); startEdit(v)"><i class="fas fa-pen mr-1"></i>Edit</button>
                            @if (!v.isPerpetual) {
                                <button class="btn btn-outline-primary" (click)="closeView(); startRenew(v)"><i class="fas fa-redo mr-1"></i>Renew</button>
                            }
                        }
                        <button class="btn btn-primary" (click)="closeView()">Close</button>
                    </div>
                </div>
            </div>
        }

        @if (form) {
            <div class="fpms-dialog-backdrop">
                <div class="fpms-dialog" role="dialog" aria-modal="true" aria-labelledby="formTitle">
                    <div class="fpms-dialog-head">
                        <h2 id="formTitle" class="mr-auto">{{ formTitle }}</h2>
                        <button type="button" class="fpms-dialog-close" aria-label="Close" (click)="closeForm()"><span aria-hidden="true">&times;</span></button>
                    </div>
                    <form #df="ngForm" (ngSubmit)="saveForm()" novalidate autocomplete="off">
                        <div class="fpms-dialog-body">
                            @if (form.mode === 'renew') {
                                <div class="alert alert-info py-2 small">
                                    <i class="fas fa-info-circle mr-1"></i>Enter the renewed permit's details. The current record ({{ form.renewedFromNumber }}) will be archived when you save, so it stops sending expiry reminders.
                                </div>
                            }
                            <div class="form-row">
                                <div class="form-group col-md-6">
                                    <label for="f-cat">Category <span class="text-danger">*</span></label>
                                    <select id="f-cat" class="form-control" name="categoryId" [(ngModel)]="form.categoryId" (ngModelChange)="categoryChanged()" required>
                                        <option [ngValue]="null" disabled>Select a category</option>
                                        @for (c of formCategories; track c.id) {
                                            <option [ngValue]="c.id">{{ c.name }}</option>
                                        }
                                    </select>
                                </div>
                                <div class="form-group col-md-6">
                                    <label for="f-preset">Document title <span class="text-danger">*</span></label>
                                    <select id="f-preset" class="form-control" name="presetChoice" [(ngModel)]="form.presetChoice" (ngModelChange)="presetChanged()" [disabled]="!form.categoryId" required>
                                        <option [ngValue]="null" disabled>{{ form.categoryId ? 'Select a title' : 'Choose a category first' }}</option>
                                        @for (p of formPresets; track p.id) {
                                            <option [ngValue]="p.id">{{ p.name }}</option>
                                        }
                                        @if (form.categoryId) {
                                            <option [ngValue]="CUSTOM">Other – type a title</option>
                                        }
                                    </select>
                                </div>
                            </div>
                            @if (form.presetChoice === CUSTOM) {
                                <div class="form-group">
                                    <label for="f-title">Title <span class="text-danger">*</span></label>
                                    <input id="f-title" class="form-control" name="title" [(ngModel)]="form.title" required maxlength="200" />
                                </div>
                            }
                            <div class="form-row">
                                <div class="form-group col-md-6">
                                    <label for="f-num">Reference number <span class="text-danger">*</span></label>
                                    <input id="f-num" class="form-control" name="documentNumber" [(ngModel)]="form.documentNumber" required maxlength="100" autocomplete="off" placeholder="e.g. ECC-R12-2026-8891" />
                                    @if (form.mode === 'renew') {
                                        <small class="form-text text-muted">Carried over from the current record. Change it if the agency issued a new number.</small>
                                    }
                                </div>
                                <div class="form-group col-md-6">
                                    <label for="f-agency">Issuing agency <span class="text-muted small">(optional)</span></label>
                                    <input id="f-agency" class="form-control" name="issuingAgency" [(ngModel)]="form.issuingAgency" maxlength="200" autocomplete="off" />
                                </div>
                            </div>
                            <div class="custom-control custom-checkbox mb-3">
                                <input type="checkbox" class="custom-control-input" id="f-perp" name="isPerpetual" [(ngModel)]="form.isPerpetual" [disabled]="requiresExpiry" />
                                <label class="custom-control-label" for="f-perp">Perpetual, no expiry date</label>
                                @if (requiresExpiry) {
                                    <small class="form-text text-muted mt-0">Records in this category must have an expiry date.</small>
                                }
                            </div>
                            <div class="form-row">
                                <div class="form-group col-md-6">
                                    <label for="f-issue">Issue date</label>
                                    <input id="f-issue" type="date" class="form-control" name="issueDate" [(ngModel)]="form.issueDate" [max]="today" />
                                </div>
                                <div class="form-group col-md-6">
                                    <label for="f-exp">Expiry date @if (!form.isPerpetual) { <span class="text-danger">*</span> }</label>
                                    <input id="f-exp" type="date" class="form-control" name="expiryDate" [(ngModel)]="form.expiryDate" [disabled]="form.isPerpetual" [required]="!form.isPerpetual" [min]="form.issueDate || ''" />
                                    @if (dateError) {
                                        <small class="text-danger">{{ dateError }}</small>
                                    } @else if (form.expiryHint && form.expiryDate === form.suggestedExpiry) {
                                        <small class="form-text text-muted">{{ form.expiryHint }}</small>
                                    }
                                </div>
                            </div>
                            <div class="form-group mb-0">
                                <label for="f-notes">Notes / location <span class="text-muted small">(optional)</span></label>
                                <textarea id="f-notes" rows="2" class="form-control" name="notes" [(ngModel)]="form.notes" maxlength="500" placeholder="e.g. Hard copy stored in Binder #2 at Farm Office"></textarea>
                            </div>
                        </div>
                        <div class="fpms-dialog-foot">
                            <fpms-form-hint [form]="df" class="mr-auto"></fpms-form-hint>
                            <button type="button" class="btn btn-default" (click)="closeForm()">Cancel</button>
                            <button type="submit" class="btn btn-primary" [disabled]="busy() || !df.form.valid || !!dateError">
                                @if (busy()) {
                                    <i class="fas fa-spinner fa-spin mr-1"></i>
                                }
                                {{ form.mode === 'renew' ? 'Save renewal' : 'Save' }}
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        }
    `,
})
export class DocumentsComponent extends FpmsPage implements OnInit {
    readonly CUSTOM = CUSTOM;
    readonly tiles = [
        { value: 1, label: 'Active', count: 'activeCount', tone: 'success' },
        { value: 2, label: 'Expiring soon', count: 'expiringSoonCount', tone: 'warning' },
        { value: 3, label: 'Expired', count: 'expiredCount', tone: 'danger' },
        { value: 0, label: 'No expiry', count: 'noExpiryCount', tone: 'info' },
    ];

    categories = signal<any[]>([]);
    documents = signal<any[] | null>(null);
    status = signal<any>(null);
    viewing = signal<any>(null);

    showConfig = false;
    search = '';
    filterCategory: string | null = null;
    filterStatus: number | null = null;
    includeArchived = false;
    form: any = null;
    newCategory: any = { name: '', requiresExpiryDate: false, isActive: true };
    newPreset: Record<string, any> = {};
    today = FarmTime.today();
    private searchTimer: any;

    ngOnInit(): void {
        this.loadCategories();
        this.loadDocuments();
        this.loadStatus();
    }

    async loadCategories(): Promise<void> {
        const list = await this.api.get<any[]>('Compliance', 'GetCategories');
        list.forEach((c) => (this.newPreset[c.id] ??= this.blankPreset(c.id)));
        this.categories.set(list);
    }

    async loadDocuments(): Promise<void> {
        this.documents.set(
            await this.api.get<any[]>('Compliance', 'GetDocuments', {
                categoryId: this.filterCategory,
                status: this.filterStatus,
                includeArchived: this.includeArchived,
                search: this.search?.trim() || null,
            })
        );
    }

    async loadStatus(): Promise<void> {
        this.status.set(await this.api.get<any>('Compliance', 'GetStatus'));
    }

    get filtered(): boolean {
        return !!this.search?.trim() || this.filterCategory !== null || this.filterStatus !== null || this.includeArchived;
    }

    toggleStatus(value: number): void {
        this.filterStatus = this.filterStatus === value ? null : value;
        this.loadDocuments();
    }

    searchChanged(): void {
        clearTimeout(this.searchTimer);
        this.searchTimer = setTimeout(() => this.loadDocuments(), 300);
    }

    clearFilters(): void {
        this.search = '';
        this.filterCategory = null;
        this.filterStatus = null;
        this.includeArchived = false;
        this.loadDocuments();
    }

    relative(days: number | null): string {
        if (days === null || days === undefined) {
            return '';
        }
        if (days === 0) {
            return 'expires today';
        }
        return days > 0 ? `in ${days} day${days === 1 ? '' : 's'}` : `${-days} day${days === -1 ? '' : 's'} ago`;
    }

    view(d: any): void {
        this.viewing.set(d);
    }

    async openById(id: string): Promise<void> {
        this.viewing.set(await this.api.get<any>('Compliance', 'GetDocument', { id }));
    }

    get formTitle(): string {
        return this.form?.mode === 'edit' ? 'Edit compliance reference' : this.form?.mode === 'renew' ? 'Renew: ' + this.form.renewedFromTitle : 'Add compliance reference';
    }

    get formCategories(): any[] {
        return this.categories().filter((c) => c.isActive || c.id === this.form?.categoryId);
    }

    get formCategory(): any {
        return this.categories().find((c) => c.id === this.form?.categoryId);
    }

    get formPresets(): any[] {
        return (this.formCategory?.presets ?? []).filter((p: any) => p.isActive || p.id === this.form?.presetChoice);
    }

    get requiresExpiry(): boolean {
        return !!this.formCategory?.requiresExpiryDate;
    }

    get dateError(): string {
        const f = this.form;
        if (!f) {
            return '';
        }
        if (f.issueDate && f.issueDate > this.today) {
            return 'The issue date cannot be in the future.';
        }
        if (!f.isPerpetual && f.issueDate && f.expiryDate && f.expiryDate < f.issueDate) {
            return 'The expiry date cannot be before the issue date.';
        }
        return '';
    }

    startCreate(presetId?: string): void {
        const preset = presetId ? this.findPreset(presetId) : null;
        const category = preset ? this.categories().find((c) => c.id === preset.categoryId) : null;
        this.form = {
            mode: 'create',
            categoryId: category?.id ?? null,
            presetChoice: null,
            title: '',
            documentNumber: '',
            issuingAgency: '',
            isPerpetual: false,
            issueDate: '',
            expiryDate: '',
            notes: '',
        };
        this.lastDefaultAgency = '';
        if (preset) {
            this.categoryChanged(preset.id);
        }
        this.opened();
    }

    startEdit(d: any): void {
        this.form = {
            mode: 'edit',
            id: d.id,
            categoryId: d.categoryId,
            presetChoice: d.presetId ?? CUSTOM,
            title: d.title,
            documentNumber: d.documentNumber,
            issuingAgency: d.issuingAgency ?? '',
            isPerpetual: d.isPerpetual,
            issueDate: d.issueDate ?? '',
            expiryDate: d.expiryDate ?? '',
            notes: d.notes ?? '',
        };
        this.lastDefaultAgency = this.findPreset(d.presetId)?.defaultIssuingAgency ?? '';
        this.opened();
    }

    startRenew(d: any): void {
        this.form = {
            mode: 'renew',
            renewedFromId: d.id,
            renewedFromTitle: d.title,
            renewedFromNumber: d.documentNumber,
            categoryId: d.categoryId,
            presetChoice: d.presetId ?? CUSTOM,
            title: d.title,
            documentNumber: d.documentNumber,
            issuingAgency: d.issuingAgency ?? '',
            isPerpetual: false,
            issueDate: this.today,
            expiryDate: '',
            notes: d.notes ?? '',
        };
        const days = this.daysBetween(d.issueDate, d.expiryDate);
        if (days && days > 0) {
            this.form.expiryDate = this.addDays(this.today, days);
            this.form.suggestedExpiry = this.form.expiryDate;
            this.form.expiryHint = `Suggested from the last permit's validity (${days} days). Check it against the new document.`;
        }
        this.lastDefaultAgency = this.findPreset(d.presetId)?.defaultIssuingAgency ?? '';
        this.opened();
    }

    categoryChanged(presetId?: string): void {
        const presets = this.formPresets;
        this.form.presetChoice = presetId ?? (!this.form.categoryId ? null : presets.length ? null : CUSTOM);
        if (this.requiresExpiry) {
            this.form.isPerpetual = false;
        }
        this.presetChanged();
    }

    private lastDefaultAgency = '';

    presetChanged(): void {
        const preset = this.findPreset(this.form.presetChoice);
        const next = preset?.defaultIssuingAgency ?? '';
        if (!this.form.issuingAgency || this.form.issuingAgency === this.lastDefaultAgency) {
            this.form.issuingAgency = next;
        }
        this.lastDefaultAgency = next;
    }

    async saveForm(): Promise<void> {
        const f = this.form;
        const isCustom = f.presetChoice === CUSTOM;
        const body: any = {
            id: f.mode === 'edit' ? f.id : EMPTY_GUID,
            categoryId: f.categoryId,
            presetId: isCustom ? null : f.presetChoice,
            title: isCustom ? f.title : null,
            documentNumber: f.documentNumber,
            issuingAgency: f.issuingAgency || null,
            issueDate: f.issueDate || null,
            isPerpetual: !!f.isPerpetual,
            expiryDate: f.isPerpetual ? null : f.expiryDate || null,
            notes: f.notes || null,
        };
        let ok: boolean;
        if (f.mode === 'edit') {
            ok = await this.act(() => this.api.put('Compliance', 'UpdateDocument', body));
        } else if (f.mode === 'renew') {
            ok = await this.act(() => this.api.post('Compliance', 'RenewDocument', { ...body, renewedFromId: f.renewedFromId }));
        } else {
            ok = await this.act(() => this.api.post('Compliance', 'CreateDocument', body));
        }
        if (ok) {
            this.notifySaved(f.mode === 'renew' ? 'Renewal saved. The earlier record was archived.' : 'Compliance reference saved.');
            this.form = null;
            this.restoreFocus();
            this.refresh();
        }
    }

    private formSnapshot = '';
    private confirming = false;
    private returnFocus: HTMLElement | null = null;

    private opened(): void {
        this.formSnapshot = JSON.stringify(this.form);
        this.returnFocus = document.activeElement as HTMLElement | null;
        setTimeout(() => document.getElementById(this.form?.categoryId ? 'f-num' : 'f-cat')?.focus());
    }

    get formDirty(): boolean {
        return !!this.form && JSON.stringify(this.form) !== this.formSnapshot;
    }

    async closeForm(): Promise<void> {
        if (this.busy() || this.confirming) {
            return;
        }
        if (this.formDirty) {
            this.confirming = true;
            const discard = await this.confirm('Your changes have not been saved.', 'Discard changes?');
            this.confirming = false;
            if (!discard) {
                return;
            }
        }
        this.form = null;
        this.restoreFocus();
    }

    closeView(): void {
        this.viewing.set(null);
    }

    private restoreFocus(): void {
        const el = this.returnFocus;
        this.returnFocus = null;
        setTimeout(() => el?.focus?.());
    }

    @HostListener('document:keydown.escape')
    onEscape(): void {
        if (this.form) {
            void this.closeForm();
        } else if (this.viewing()) {
            this.closeView();
        }
    }

    private daysBetween(from: string | null, to: string | null): number | null {
        if (!from || !to) {
            return null;
        }
        const ms = Date.parse(to + 'T00:00:00Z') - Date.parse(from + 'T00:00:00Z');
        return Number.isFinite(ms) ? Math.round(ms / 86400000) : null;
    }

    private addDays(date: string, days: number): string {
        const d = new Date(Date.parse(date + 'T00:00:00Z') + days * 86400000);
        return d.toISOString().slice(0, 10);
    }

    async archive(d: any): Promise<void> {
        if (!d.isArchived && !(await this.confirm('The record is kept as history but no longer counts as current or sends reminders.', 'Archive record?'))) {
            return;
        }
        if (await this.act(() => this.api.post('Compliance', 'SetArchived', { id: d.id, isArchived: !d.isArchived }))) {
            this.refresh();
        }
    }

    async saveCategory(c: any, isNew = false): Promise<void> {
        const body = { id: isNew ? EMPTY_GUID : c.id, name: c.name, requiresExpiryDate: c.requiresExpiryDate, isActive: c.isActive };
        if (await this.act(() => this.api.post('Compliance', 'SaveCategory', body))) {
            this.notifySaved('Category saved.');
            if (isNew) {
                this.newCategory = { name: '', requiresExpiryDate: false, isActive: true };
            }
            this.loadCategories();
            this.loadStatus();
        }
    }

    async savePreset(p: any, isNew = false): Promise<void> {
        const body = { ...p, id: isNew ? EMPTY_GUID : p.id };
        if (await this.act(() => this.api.post('Compliance', 'SavePreset', body))) {
            this.notifySaved('Preset saved.');
            if (isNew) {
                this.newPreset[p.categoryId] = this.blankPreset(p.categoryId);
            }
            this.loadCategories();
            this.loadStatus();
        }
    }

    private blankPreset(categoryId: string): any {
        return { categoryId, name: '', defaultIssuingAgency: '', isRequired: false, isActive: true };
    }

    private findPreset(id: string | null | undefined): any {
        if (!id || id === CUSTOM) {
            return null;
        }
        for (const c of this.categories()) {
            const p = (c.presets ?? []).find((x: any) => x.id === id);
            if (p) {
                return p;
            }
        }
        return null;
    }

    private refresh(): void {
        this.loadDocuments();
        this.loadStatus();
        this.loadCategories();
    }
}
