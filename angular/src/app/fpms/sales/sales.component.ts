import { Component, OnInit, computed, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime } from '@shared/fpms/farm-time';
import { PagerState } from '@shared/fpms/pager';
import { FPMS_IMPORTS } from '../shared-imports';
import { ToolbarChip } from '@shared/fpms/ui';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Sales" subtitle="Dispatch receipts, payments and balances.">
            @if (can('Farm.Reports')) {
                <button
                    class="btn btn-outline-secondary mr-2"
                    (click)="exportCsv('ExportList', exportBody())"
                    [disabled]="busy()">
                    <i class="fas fa-file-csv mr-1"></i>
                    Export CSV
                </button>
            }
            <a routerLink="/app/sales/new" class="btn btn-primary">
                <i class="fas fa-plus mr-1"></i>
                Record sale
            </a>
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <div class="card">
                    <div class="card-header">
                        <fpms-list-toolbar
                            placeholder="Search by receipt, buyer or batch"
                            [search]="f.search"
                            [resultText]="resultText()"
                            [chips]="chips()"
                            [chip]="f.paymentStatus"
                            (searchChange)="onSearch($event)"
                            (chipChange)="onPayment($event)">
                            <label class="sl-field">
                                <span>Buyer</span>
                                <select
                                    class="form-control form-control-sm"
                                    [(ngModel)]="f.buyerId"
                                    (ngModelChange)="apply()">
                                    <option [ngValue]="null">All buyers</option>
                                    @for (b of buyers(); track b.id) {
                                        <option [ngValue]="b.id">{{ b.name }}</option>
                                    }
                                </select>
                            </label>
                            <label class="sl-field">
                                <span>From</span>
                                <input
                                    type="date"
                                    class="form-control form-control-sm"
                                    [(ngModel)]="f.from"
                                    (ngModelChange)="apply()" />
                            </label>
                            <label class="sl-field">
                                <span>To</span>
                                <input
                                    type="date"
                                    class="form-control form-control-sm"
                                    [(ngModel)]="f.to"
                                    (ngModelChange)="apply()" />
                            </label>
                        </fpms-list-toolbar>
                        <div class="custom-control custom-checkbox mt-1">
                            <input
                                type="checkbox"
                                class="custom-control-input"
                                id="od"
                                [(ngModel)]="f.overdueOnly"
                                (ngModelChange)="apply()" />
                            <label class="custom-control-label small" for="od">Overdue only</label>
                        </div>
                    </div>
                </div>
                @if (payment) {
                    <div class="card card-outline card-primary">
                        <div class="card-header">
                            <h3 class="card-title">
                                <i class="fas fa-money-bill-wave" aria-hidden="true"></i>
                                Add payment: {{ payment.sale.dispatchReceiptNo }}, {{ payment.sale.buyerName }} (balance
                                {{ payment.sale.balance | php }})
                            </h3>
                        </div>
                        <div class="card-body">
                            <div class="form-row align-items-end">
                                <div class="form-group col-md-3 mb-0">
                                    <label for="pon">Paid on</label>
                                    <input
                                        id="pon"
                                        type="date"
                                        class="form-control"
                                        [(ngModel)]="payment.paidOn"
                                        [min]="payment.sale.saleDate"
                                        [max]="today" />
                                </div>
                                <div class="form-group col-md-3 mb-0">
                                    <label for="amt">Amount (₱)</label>
                                    <input
                                        id="amt"
                                        type="number"
                                        min="0.01"
                                        step="0.01"
                                        class="form-control"
                                        [(ngModel)]="payment.amount" />
                                </div>
                                <div class="form-group col-md-3 mb-0">
                                    <label for="mth">Method</label>
                                    <input
                                        id="mth"
                                        class="form-control"
                                        [(ngModel)]="payment.method"
                                        maxlength="50"
                                        placeholder="cash, bank transfer, GCash" />
                                </div>
                                <div class="form-group col-md-3 mb-0">
                                    <button
                                        class="btn btn-outline-secondary btn-sm"
                                        (click)="payment.amount = payment.sale.balance">
                                        Pay the balance
                                    </button>
                                </div>
                            </div>
                        </div>
                        <div class="card-footer">
                            <button
                                class="btn btn-primary mr-2"
                                [disabled]="busy() || !(payment.amount > 0) || !payment.paidOn"
                                (click)="savePayment()">
                                Save payment
                            </button>
                            <button class="btn btn-default" (click)="payment = null">Cancel</button>
                        </div>
                    </div>
                }
                <div class="card">
                    <div class="card-body p-0 table-responsive">
                        @if (!sales()) {
                            <fpms-loading></fpms-loading>
                        } @else if (!sales().length) {
                            <fpms-empty text="No sales match." icon="fas fa-cash-register"></fpms-empty>
                        } @else {
                            <table class="table table-hover mb-0">
                                <thead>
                                    <tr>
                                        <th fpmsSort="dispatchReceiptNo" [pager]="pager" (sorted)="load()">Receipt</th>
                                        <th fpmsSort="saleDate" [pager]="pager" (sorted)="load()">Date</th>
                                        <th fpmsSort="buyerName" [pager]="pager" (sorted)="load()">Buyer</th>
                                        <th>Batch</th>
                                        <th class="text-right">Kg</th>
                                        <th class="text-right" fpmsSort="total" [pager]="pager" (sorted)="load()">
                                            Total
                                        </th>
                                        <th class="text-right" fpmsSort="paid" [pager]="pager" (sorted)="load()">
                                            Paid
                                        </th>
                                        <th class="text-right" fpmsSort="balance" [pager]="pager" (sorted)="load()">
                                            Balance
                                        </th>
                                        <th fpmsSort="paymentStatus" [pager]="pager" (sorted)="load()">Payment</th>
                                        <th><span class="sr-only">Actions</span></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    @for (s of sales(); track s.id) {
                                        <tr>
                                            <td class="text-nowrap">
                                                <a [routerLink]="['/app/sales', s.id]">{{ s.dispatchReceiptNo }}</a>
                                            </td>
                                            <td class="text-nowrap">{{ s.saleDate | farmDate }}</td>
                                            <td>{{ s.buyerName }}</td>
                                            <td>{{ s.batches }}</td>
                                            <td class="text-right">{{ s.totalKg | num: 2 }}</td>
                                            <td class="text-right">{{ s.total | php }}</td>
                                            <td class="text-right">{{ s.paid | php }}</td>
                                            <td class="text-right">{{ s.balance | php }}</td>
                                            <td class="text-nowrap">
                                                <fpms-badge
                                                    [text]="s.paymentStatus | label: L.PaymentStatuses"></fpms-badge>
                                                @if (s.isOverdue) {
                                                    <span
                                                        class="badge badge-danger ml-1"
                                                        [title]="'Due ' + (s.dueDate | farmDate)">
                                                        Overdue
                                                    </span>
                                                }
                                                @if (s.overpaid > 0) {
                                                    <span class="badge badge-warning ml-1">Overpaid</span>
                                                }
                                            </td>
                                            <td class="text-right text-nowrap">
                                                @if (s.balance > 0) {
                                                    <button
                                                        class="btn btn-sm btn-outline-primary mr-1"
                                                        (click)="startPayment(s)">
                                                        Payment
                                                    </button>
                                                }
                                                <a
                                                    class="btn btn-sm btn-outline-secondary"
                                                    [routerLink]="['/app/sales', s.id]"
                                                    title="Dispatch receipt"
                                                    aria-label="Dispatch receipt">
                                                    <i class="fas fa-print"></i>
                                                </a>
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                                <tfoot>
                                    <tr class="font-weight-bold">
                                        <td colspan="4">All {{ pager.total }} matching sales</td>
                                        <td class="text-right">{{ totals().totalKg | num: 2 }}</td>
                                        <td class="text-right">{{ totals().totalAmount | php }}</td>
                                        <td class="text-right">{{ totals().totalPaid | php }}</td>
                                        <td class="text-right">{{ totals().totalBalance | php }}</td>
                                        <td colspan="2">
                                            @if (totals().overdueCount) {
                                                <span class="badge badge-danger">
                                                    {{ totals().overdueCount }} overdue
                                                </span>
                                            }
                                        </td>
                                    </tr>
                                </tfoot>
                            </table>
                        }
                    </div>
                    <fpms-pager [state]="pager" (changed)="load()"></fpms-pager>
                </div>
            </div>
        </section>
    `,
    styles: [
        `
            .sl-field {
                display: flex;
                align-items: center;
                gap: 0.5rem;
                margin: 0;
                font-weight: 400;
                font-size: 0.9rem;
                color: var(--fpms-muted);
            }
            .sl-field .form-control {
                height: 38px;
                width: auto;
            }
        `,
    ],
})
export class SalesComponent extends FpmsPage implements OnInit {
    sales = signal<any[] | null>(null);
    totals = signal<any>({});
    pager = new PagerState('saleDate desc');
    buyers = signal<any[]>([]);
    payment: any = null;
    counts = signal<Record<number, number>>({});
    f: any = {
        search: '',
        from: FarmTime.addDays(FarmTime.today(), -90),
        to: FarmTime.today(),
        buyerId: null,
        paymentStatus: null,
        overdueOnly: false,
    };

    chips = computed<ToolbarChip[]>(() => {
        const c = this.counts();
        const all = (Object.values(c) as number[]).reduce((n, v) => n + v, 0);
        return [
            { value: null, label: 'All payments', count: all },
            ...this.Options.paymentStatus.map((o: any) => ({ value: o.value, label: o.label, count: c[o.value] ?? 0 })),
        ];
    });

    resultText = computed(() => {
        const n = this.totals().totalCount ?? 0;
        return n === 1 ? '1 sale' : `${n} sales`;
    });

    ngOnInit(): void {
        if (this.query('overdue') === '1') {
            this.f = { ...this.f, from: null, overdueOnly: true };
        }
        this.restoreList(this.pager, this.f, {
            search: 'string',
            from: 'string',
            to: 'string',
            buyerId: 'string',
            paymentStatus: 'number',
            overdueOnly: 'boolean',
        });
        this.api.get<any[]>('Buyer', 'GetLookup').then((b) => this.buyers.set(b ?? []));
        this.load();
    }

    apply(): void {
        this.pager.reset();
        this.load();
    }

    onSearch(value: string): void {
        this.f.search = value;
        this.apply();
    }

    onPayment(value: unknown): void {
        this.f.paymentStatus = value as number | null;
        this.apply();
    }

    async load(): Promise<void> {
        await this.run(async () => {
            const q = { ...this.f, search: this.f.search || undefined };
            const [r, counts] = await Promise.all([
                this.api.get<any>('Sale', 'GetAll', { ...q, ...this.pager.params }),
                this.api.get<any[]>('Sale', 'GetPaymentStatusCounts', q),
            ]);
            this.counts.set(Object.fromEntries((counts ?? []).map((x) => [x.status, x.count])));
            this.totals.set(r);
            this.sales.set(r.items);
            const reload = this.pager.update(r.totalCount);
            this.syncPager(this.pager);
            if (reload) {
                await this.load();
            }
        });
    }

    exportBody(): Record<string, unknown> {
        return {
            list: 'Sales',
            search: this.f.search || null,
            buyerId: this.f.buyerId,
            from: this.f.from,
            to: this.f.to,
            paymentStatus: this.f.paymentStatus,
            overdueOnly: this.f.overdueOnly,
        };
    }

    startPayment(s: any): void {
        this.payment = { sale: s, paidOn: this.today, amount: s.balance, method: '' };
    }

    async savePayment(): Promise<void> {
        const p = this.payment;
        let sale: any;
        if (
            await this.act(
                async () =>
                    (sale = await this.api.post<any>('Sale', 'AddPayment', {
                        saleId: p.sale.id,
                        paidOn: p.paidOn,
                        amount: p.amount,
                        method: p.method,
                    }))
            )
        ) {
            this.notifySaved('Payment recorded.');
            if (sale?.warnings?.length) {
                this.showWarnings(sale.warnings);
            }
            this.payment = null;
            this.load();
        }
    }
}
