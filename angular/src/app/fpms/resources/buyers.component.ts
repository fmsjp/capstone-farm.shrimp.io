import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { EMPTY_GUID } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';
import { ClientListFilter } from '@shared/fpms/client-list';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Buyers" subtitle="Harvest buyers and their sales history.">
            <button class="btn btn-primary" (click)="startEdit()" [disabled]="!!form"><i class="fas fa-plus mr-1"></i>Add buyer</button>
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (form) {
                    <div class="fpms-dialog-backdrop"><div class="fpms-dialog" role="dialog" aria-modal="true"><div class="fpms-dialog-body p-0">
                        <div class="card card-outline card-primary">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-handshake" aria-hidden="true"></i>{{ form.id === empty ? 'New buyer' : 'Edit ' + form.name }}</h3></div>
                            <form #f="ngForm" (ngSubmit)="save()" novalidate>
                                <div class="card-body">
                                    <div class="form-group">
                                        <span class="d-block mb-1">Buyer type <span class="text-danger">*</span></span>
                                        @for (o of Options.buyerType; track o.value) {
                                            <div class="custom-control custom-radio custom-control-inline">
                                                <input type="radio" class="custom-control-input" [id]="'bt' + o.value" name="buyerType" [value]="o.value" [(ngModel)]="form.buyerType" />
                                                <label class="custom-control-label" [for]="'bt' + o.value">{{ o.label }}</label>
                                            </div>
                                        }
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group col-md-5">
                                            <label for="cn">{{ form.buyerType === 1 ? 'Full name' : 'Company name' }} <span class="text-danger">*</span></label>
                                            <input id="cn" class="form-control" name="name" [(ngModel)]="form.name" required maxlength="150" />
                                        </div>
                                        @if (form.buyerType === 2) {
                                            <div class="form-group col-md-4">
                                                <label for="cp">Contact person</label>
                                                <input id="cp" class="form-control" name="contactPerson" [(ngModel)]="form.contactPerson" maxlength="100" />
                                            </div>
                                        }
                                        <div class="form-group col-md-3">
                                            <label for="ph">Phone</label>
                                            <input id="ph" class="form-control" name="phone" [(ngModel)]="form.phone" maxlength="50" />
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group col-md-9">
                                            <label for="ad">Address</label>
                                            <input id="ad" class="form-control" name="address" [(ngModel)]="form.address" maxlength="300" />
                                        </div>
                                        <div class="form-group col-md-3 d-flex align-items-end">
                                            <div class="custom-control custom-checkbox mb-2">
                                                <input type="checkbox" class="custom-control-input" id="act" name="isActive" [(ngModel)]="form.isActive" />
                                                <label class="custom-control-label" for="act">Active</label>
                                            </div>
                                        </div>
                                    </div>
                                    @if (form.buyerType === 1) {
                                        <div class="custom-control custom-checkbox">
                                            <input type="checkbox" class="custom-control-input" id="pn" name="privacyNoticeAcknowledged" [(ngModel)]="form.privacyNoticeAcknowledged" [disabled]="!!form.privacyNoticeAcknowledgedAtUtc" required />
                                            <label class="custom-control-label" for="pn">
                                                I told this buyer that their name and contact details are kept for the farm's sales records (Data Privacy Act of 2012). <span class="text-danger">*</span>
                                            </label>
                                            @if (form.privacyNoticeAcknowledgedAtUtc) {
                                                <small class="form-text text-muted">Confirmed {{ form.privacyNoticeAcknowledgedAtUtc | farmDateTime }}.</small>
                                            }
                                        </div>
                                    }
                                </div>
                                <div class="card-footer">
                                    <button type="submit" class="btn btn-primary mr-2" [disabled]="busy() || !f.form.valid">Save</button>
                                    <button type="button" class="btn btn-default" (click)="form = null">Cancel</button>
                                    <fpms-form-hint [form]="f"></fpms-form-hint>
                                </div>
                            </form>
                        </div>
                    </div></div></div>
                }
                @if (detail(); as d) {
                    <div class="card card-outline card-primary">
                        <div class="card-header d-flex">
                            <h3 class="card-title mr-auto"><i class="fas fa-cash-register" aria-hidden="true"></i>Sales to {{ d.name }}</h3>
                            <button class="btn btn-sm btn-default" (click)="detail.set(null)">Close</button>
                        </div>
                        <div class="card-body p-0">
                            <div class="table-responsive">
                            <table class="table table-sm mb-0">
                                <thead><tr><th>Date</th><th>Receipt</th><th class="text-right">Kg</th><th class="text-right">Amount</th><th>Payment</th></tr></thead>
                                <tbody>
                                    @for (s of d.sales; track s.id) {
                                        <tr>
                                            <td><a [routerLink]="['/app/sales', s.id]">{{ s.saleDate | farmDate }}</a></td>
                                            <td>{{ s.dispatchReceiptNo }}</td>
                                            <td class="text-right">{{ s.totalKg | num: 2 }}</td>
                                            <td class="text-right">{{ s.totalAmount | php }}</td>
                                            <td><fpms-badge [text]="s.paymentStatus | label: L.PaymentStatuses"></fpms-badge>@if (s.isOverdue) { <span class="badge badge-danger ml-1">Overdue</span> }</td>
                                        </tr>
                                    } @empty {
                                        <tr><td colspan="5" class="text-center text-muted py-3">No sales to this buyer yet.</td></tr>
                                    }
                                </tbody>
                            </table>
                            </div>
                        </div>
                    </div>
                }
                <div class="card">
                    <div class="card-header">
                        <fpms-list-toolbar
                            placeholder="Search by buyer, contact or phone"
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
                            <fpms-empty text="No buyers match. Clear the search or the filter." icon="fas fa-handshake"></fpms-empty>
                        } @else if (!list().length) {
                            <fpms-empty text="No buyers yet. Add buyers before recording sales." icon="fas fa-handshake"></fpms-empty>
                        } @else {
                            <table class="table table-hover mb-0">
                                <thead>
                                    <tr><th>Buyer</th><th>Contact</th><th>Phone</th><th class="text-right">Sales</th><th class="text-right">Total</th><th>Status</th><th><span class="sr-only">Actions</span></th></tr>
                                </thead>
                                <tbody>
                                    @for (b of f.visible(); track b.id) {
                                        <tr [class.text-muted]="!b.isActive">
                                            <td>{{ b.name }} <span class="small text-muted">({{ b.buyerType | label: L.BuyerTypes }})</span><div class="small text-muted">{{ b.address }}</div></td>
                                            <td>{{ b.contactPerson }}</td>
                                            <td>{{ b.phone }}</td>
                                            <td class="text-right">{{ b.saleCount }}</td>
                                            <td class="text-right">{{ b.totalSales | php }}</td>
                                            <td><fpms-badge [text]="b.isActive ? 'Active' : 'Inactive'" [tone]="b.isActive ? 'success' : 'secondary'"></fpms-badge></td>
                                            <td class="text-right text-nowrap">
                                                <button class="btn btn-sm btn-outline-secondary mr-1" (click)="showSales(b)">Sales</button>
                                                <button class="btn btn-sm btn-outline-primary mr-1" (click)="startEdit(b)">Edit</button>
                                                <button class="btn btn-sm btn-outline-secondary" (click)="toggle(b)">{{ b.isActive ? 'Deactivate' : 'Reactivate' }}</button>
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
export class BuyersComponent extends FpmsPage implements OnInit {
    readonly empty = EMPTY_GUID;
    list = signal<any[] | null>(null);
    f = new ClientListFilter<any>(
        () => this.list(),
        (b) => [b.name, b.contactPerson, b.phone, b.address],
        (b) => (b.isActive ? 'active' : 'inactive'),
        [{ value: 'active', label: 'Active' }, { value: 'inactive', label: 'Inactive' }],
        'All buyers',
        ['buyer', 'buyers']
    );
    detail = signal<any>(null);
    form: any = null;

    ngOnInit(): void {
        this.load();
    }

    async load(): Promise<void> {
        this.list.set(await this.api.get<any[]>('Buyer', 'GetAll'));
    }

    async showSales(b: any): Promise<void> {
        this.detail.set(await this.api.get<any>('Buyer', 'Get', { id: b.id }));
    }

    startEdit(b?: any): void {
        this.form = b ? { ...b } : { id: EMPTY_GUID, buyerType: 2, name: '', contactPerson: '', phone: '', address: '', isActive: true, privacyNoticeAcknowledged: false };
    }

    async save(): Promise<void> {
        if (await this.act(() => this.api.post('Buyer', 'Save', this.form))) {
            this.notifySaved('Buyer saved.');
            this.form = null;
            this.load();
        }
    }

    async toggle(b: any): Promise<void> {
        if (await this.act(() => this.api.post('Buyer', 'SetActive', { id: b.id, isActive: !b.isActive }))) {
            this.load();
        }
    }
}
