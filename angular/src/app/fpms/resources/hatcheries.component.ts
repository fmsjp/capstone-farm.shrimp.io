import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { EMPTY_GUID } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';
import { ClientListFilter } from '@shared/fpms/client-list';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Hatchery sources" subtitle="Fry and PL suppliers with accreditation and health certificates.">
            @if (canManage) {
                <button class="btn btn-primary" (click)="startEdit()" [disabled]="!!form"><i class="fas fa-plus mr-1"></i>Add hatchery</button>
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (form) {
                    <div class="fpms-dialog-backdrop"><div class="fpms-dialog" role="dialog" aria-modal="true"><div class="fpms-dialog-body p-0">
                        <div class="card card-outline card-primary">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-industry" aria-hidden="true"></i>{{ form.id === empty ? 'New hatchery source' : 'Edit ' + form.name }}</h3></div>
                            <form #f="ngForm" (ngSubmit)="save()" novalidate>
                                <div class="card-body">
                                    <div class="form-row">
                                        <div class="form-group col-md-5">
                                            <label for="n">Name <span class="text-danger">*</span></label>
                                            <input id="n" class="form-control" name="name" [(ngModel)]="form.name" (blur)="suggestCode()" required maxlength="150" />
                                        </div>
                                        <div class="form-group col-md-2">
                                            <label for="sc">Short code <span class="text-danger">*</span></label>
                                            <input id="sc" class="form-control text-uppercase" name="shortCode" [(ngModel)]="form.shortCode" required maxlength="6" pattern="[A-Za-z]{2,6}" [placeholder]="suggestedCode" [disabled]="form.cycleCount > 0" />
                                            <small class="form-text text-muted">{{ form.cycleCount > 0 ? 'Fixed: used in cycle codes.' : '2–6 letters, used in cycle codes.' }}</small>
                                        </div>
                                        <div class="form-group col-md-5">
                                            <label for="a">Address <span class="text-danger">*</span></label>
                                            <input id="a" class="form-control" name="address" [(ngModel)]="form.address" required maxlength="300" />
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group col-md-4">
                                            <label for="cp">Contact person</label>
                                            <input id="cp" class="form-control" name="contactPerson" [(ngModel)]="form.contactPerson" maxlength="100" />
                                        </div>
                                        <div class="form-group col-md-4">
                                            <label for="ph">Phone <span class="text-danger">*</span></label>
                                            <input id="ph" class="form-control" name="phone" [(ngModel)]="form.phone" required maxlength="50" />
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group col-md-4">
                                            <label for="acc">BFAR accreditation no.</label>
                                            <input id="acc" class="form-control" name="bfarAccreditationNo" [(ngModel)]="form.bfarAccreditationNo" maxlength="100" />
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        @if (form.id === empty) {
                                            <div class="form-group col-md-4">
                                                <label for="hc">Health certificate no.</label>
                                                <input id="hc" class="form-control" name="certificateNo" [(ngModel)]="cert.certificateNo" maxlength="100" />
                                            </div>
                                            <div class="form-group col-md-2">
                                                <label for="hi">Issued</label>
                                                <input id="hi" type="date" class="form-control" name="issueDate" [(ngModel)]="cert.issueDate" [max]="today" />
                                            </div>
                                            <div class="form-group col-md-2">
                                                <label for="he">Expires</label>
                                                <input id="he" type="date" class="form-control" name="expiryDate" [(ngModel)]="cert.expiryDate" />
                                            </div>
                                        }
                                        <div class="form-group col-md-4 d-flex align-items-end">
                                            <div class="custom-control custom-checkbox mb-2">
                                                <input type="checkbox" class="custom-control-input" id="act" name="isActive" [(ngModel)]="form.isActive" />
                                                <label class="custom-control-label" for="act">Active</label>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                                <div class="card-footer">
                                    <button type="submit" class="btn btn-primary mr-2" [disabled]="busy() || !f.form.valid">Save</button>
                                    <button type="button" class="btn btn-default" (click)="form = null">Cancel</button>
                                    <fpms-form-hint [form]="f"></fpms-form-hint>
                                </div>
                            </form>
                        </div>
                        @if (form.id !== empty && detail()) {
                            <div class="card">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-certificate" aria-hidden="true"></i>Certificates</h3></div>
                                <div class="card-body p-0 table-responsive">
                                    <table class="table table-sm mb-0">
                                        <thead><tr><th>Type</th><th>Number</th><th>Issued</th><th>Expires</th><th>Status</th><th>Recorded by</th><th><span class="sr-only">Actions</span></th></tr></thead>
                                        <tbody>
                                            @for (c of detail().certificates; track c.id) {
                                                <tr>
                                                    <td>{{ c.certificateType | label: L.CertificateTypes }}</td>
                                                    <td>{{ c.certificateNo }}</td>
                                                    <td>{{ c.issueDate | farmDate }}</td>
                                                    <td>{{ c.expiryDate | farmDate }}</td>
                                                    <td>
                                                        @if (c.isCurrent) {
                                                            <fpms-badge text="Current" tone="success"></fpms-badge>
                                                        } @else if (c.isExpired) {
                                                            <fpms-badge text="Expired" tone="danger"></fpms-badge>
                                                        }
                                                    </td>
                                                    <td class="small">{{ c.recordedBy }}</td>
                                                    <td class="text-right">
                                                        <button class="btn btn-xs btn-outline-danger" [disabled]="busy()" (click)="removeCertificate(c)" title="Remove a certificate entered by mistake">Remove</button>
                                                    </td>
                                                </tr>
                                            } @empty {
                                                <tr><td colspan="7" class="text-muted text-center py-2">No certificates yet.</td></tr>
                                            }
                                        </tbody>
                                    </table>
                                </div>
                                <form class="card-footer" #cf="ngForm" (ngSubmit)="addCertificate()" novalidate>
                                    <div class="form-row align-items-end">
                                        <div class="form-group col-md-2 mb-0">
                                            <label for="ct">Type</label>
                                            <select id="ct" class="form-control form-control-sm" name="certificateType" [(ngModel)]="cert.certificateType">
                                                @for (o of Options.certificateType; track o.value) {
                                                    <option [ngValue]="o.value">{{ o.label }}</option>
                                                }
                                            </select>
                                        </div>
                                        <div class="form-group col-md-3 mb-0">
                                            <label for="cn">Number <span class="text-danger">*</span></label>
                                            <input id="cn" class="form-control form-control-sm" name="certificateNo" [(ngModel)]="cert.certificateNo" required maxlength="100" />
                                        </div>
                                        <div class="form-group col-md-2 mb-0">
                                            <label for="ci">Issued</label>
                                            <input id="ci" type="date" class="form-control form-control-sm" name="issueDate" [(ngModel)]="cert.issueDate" [max]="today" />
                                        </div>
                                        <div class="form-group col-md-2 mb-0">
                                            <label for="ce">Expires</label>
                                            <input id="ce" type="date" class="form-control form-control-sm" name="expiryDate" [(ngModel)]="cert.expiryDate" />
                                        </div>
                                        <div class="form-group col-md-3 mb-0">
                                            <button type="submit" class="btn btn-sm btn-primary" [disabled]="busy() || !cf.form.valid">Add certificate</button>
                                            <fpms-form-hint [form]="cf"></fpms-form-hint>
                                        </div>
                                    </div>
                                    <small class="form-text text-muted">A renewal is a new certificate; earlier ones stay in the history.</small>
                                </form>
                            </div>
                        }
                    </div></div></div>
                }
                <div class="card">
                    <div class="card-header">
                        <fpms-list-toolbar
                            placeholder="Search by name, contact or accreditation no."
                            [search]="f.search()"
                            [resultText]="f.resultText()"
                            [chips]="f.chips()"
                            [chip]="f.chip()"
                            (searchChange)="f.search.set($event)"
                            (chipChange)="f.chip.set($event)"></fpms-list-toolbar>
                    </div>
                    <div class="card-body p-0 table-responsive">
                        @if (!list()) {
                            <fpms-loading></fpms-loading>
                        } @else if (f.noMatch()) {
                            <fpms-empty text="No hatchery sources match. Clear the search or the filter." icon="fas fa-egg"></fpms-empty>
                        } @else if (!list().length) {
                            <fpms-empty text="No hatchery sources yet. A production cycle needs one." icon="fas fa-egg"></fpms-empty>
                        } @else {
                            <table class="table table-hover mb-0">
                                <thead>
                                    <tr><th>Name</th><th>Contact</th><th>Accreditation</th><th>Health certificate</th><th>Cycles</th><th>Status</th><th><span class="sr-only">Actions</span></th></tr>
                                </thead>
                                <tbody>
                                    @for (h of f.visible(); track h.id) {
                                        <tr [class.text-muted]="!h.isActive">
                                            <td>{{ h.name }} <code class="small">{{ h.shortCode }}</code><div class="small text-muted">{{ h.address }}</div></td>
                                            <td>{{ h.contactPerson }}<div class="small text-muted">{{ h.phone }}</div></td>
                                            <td>
                                                <fpms-badge [text]="h.accreditationStatus | label: L.AccreditationStatuses"></fpms-badge>
                                                <div class="small text-muted">{{ h.bfarAccreditationNo }}</div>
                                            </td>
                                            <td>
                                                {{ h.healthCertificateNo || '—' }}
                                                @if (h.healthCertificateExpiry) {
                                                    <div class="small" [class.text-danger]="h.isCertificateExpired">
                                                        @if (h.isCertificateExpired) {
                                                            <i class="fas fa-exclamation-circle mr-1"></i>Expired
                                                        } @else {
                                                            Expires
                                                        }
                                                        {{ h.healthCertificateExpiry | farmDate }}
                                                    </div>
                                                }
                                            </td>
                                            <td>{{ h.cycleCount }}</td>
                                            <td><fpms-badge [text]="h.isActive ? 'Active' : 'Inactive'" [tone]="h.isActive ? 'success' : 'secondary'"></fpms-badge></td>
                                            <td class="text-right text-nowrap">
                                                @if (canManage) {
                                                    <button class="btn btn-sm btn-outline-primary mr-1" (click)="startEdit(h)">Edit</button>
                                                    <button class="btn btn-sm btn-outline-secondary" (click)="toggle(h)">{{ h.isActive ? 'Deactivate' : 'Reactivate' }}</button>
                                                }
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
    `,
})
export class HatcheriesComponent extends FpmsPage implements OnInit {
    readonly empty = EMPTY_GUID;
    list = signal<any[] | null>(null);
    f = new ClientListFilter<any>(
        () => this.list(),
        (h) => [h.name, h.shortCode, h.contactPerson, h.phone, h.address, h.bfarAccreditationNo],
        (h) => (h.isActive ? 'active' : 'inactive'),
        [{ value: 'active', label: 'Active' }, { value: 'inactive', label: 'Inactive' }],
        'All sources',
        ['hatchery source', 'hatchery sources']
    );
    detail = signal<any | null>(null);
    form: any = null;
    cert: any = this.blankCertificate();

    get suggestedCode(): string {
        const name: string = this.form?.name ?? '';
        let code = (name.match(/(^|[\s(\-/])[A-Za-z]/g) ?? []).map((m) => m.slice(-1).toUpperCase()).join('').slice(0, 6);
        if (code.length < 2) {
            code = (name.replace(/[^A-Za-z]/g, '').toUpperCase() + 'XX').slice(0, 2);
        }
        return name ? code : 'e.g. SHI';
    }

    blankCertificate(): any {
        return { certificateType: 1, certificateNo: '', issueDate: '', expiryDate: '' };
    }

    get canManage(): boolean {
        return this.can('Farm.Hatcheries.Manage');
    }

    ngOnInit(): void {
        this.load();
    }

    async load(): Promise<void> {
        this.list.set(await this.api.get<any[]>('Hatchery', 'GetAll'));
    }

    async startEdit(h?: any): Promise<void> {
        this.cert = this.blankCertificate();
        this.detail.set(null);
        this.form = h ? { ...h } : { id: EMPTY_GUID, name: '', shortCode: '', accreditationStatus: 4, isActive: true, cycleCount: 0 };
        if (h) {
            this.detail.set(await this.api.get<any>('Hatchery', 'Get', { id: h.id }));
        }
    }

    private certificateBody(hatcheryId: string): any {
        return { ...this.cert, hatcheryId, issueDate: this.cert.issueDate || null, expiryDate: this.cert.expiryDate || null };
    }

    suggestCode(): void {
        if (this.form?.id === EMPTY_GUID && !this.form.shortCode?.trim() && this.form.name?.trim()) {
            this.form.shortCode = this.suggestedCode;
        }
    }

    async save(): Promise<void> {
        const isNew = this.form.id === EMPTY_GUID;
        let id: string;
        if (await this.act(async () => {
            id = await this.api.post<string>('Hatchery', 'Save', { ...this.form, shortCode: (this.form.shortCode ?? '').trim().toUpperCase() });
            if (isNew && this.cert.certificateNo?.trim()) {
                await this.api.post('Hatchery', 'AddCertificate', this.certificateBody(id));
            }
        })) {
            this.notifySaved('Hatchery source saved.');
            this.form = null;
            this.load();
        }
    }

    async addCertificate(): Promise<void> {
        if (await this.act(() => this.api.post('Hatchery', 'AddCertificate', this.certificateBody(this.form.id)))) {
            this.notifySaved('Certificate added.');
            this.cert = this.blankCertificate();
            this.detail.set(await this.api.get<any>('Hatchery', 'Get', { id: this.form.id }));
            this.load();
        }
    }

    async removeCertificate(c: any): Promise<void> {
        if (!(await this.confirm(`Remove certificate ${c.certificateNo}? Use this only for a certificate entered by mistake.`))) {
            return;
        }
        if (await this.act(() => this.api.delete('Hatchery', 'DeleteCertificate', { id: c.id }))) {
            this.detail.set(await this.api.get<any>('Hatchery', 'Get', { id: this.form.id }));
            this.load();
        }
    }

    async toggle(h: any): Promise<void> {
        if (await this.act(() => this.api.post('Hatchery', 'SetActive', { id: h.id, isActive: !h.isActive }))) {
            this.load();
        }
    }
}
