import { Component, OnInit } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Dispatch receipt" [subtitle]="s ? s.dispatchReceiptNo + ' · ' + s.buyerName : ''" backLink="/app/sales" backLabel="Sales">
            @if (s) {
                @if (can('Farm.Reports')) {
                    @for (b of tracedBatches(); track b.id) {
                        <a class="btn btn-outline-secondary mr-1" [routerLink]="['/app/reports/trace', b.id]" [queryParams]="{ from: 'sales' }">Trace report{{ tracedBatches().length > 1 ? ' · ' + b.label : '' }}</a>
                    }
                }
                <a class="btn btn-outline-primary mr-1" [routerLink]="['/app/sales', s.id, 'edit']">Edit</a>
                @if (canManage) {
                    <button class="btn btn-outline-danger mr-1" (click)="voiding = { reason: '' }">Void</button>
                }
                <button class="btn btn-primary" (click)="print()"><i class="fas fa-print mr-1"></i>Print</button>
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (!s) {
                    <fpms-loading></fpms-loading>
                } @else {
                    @if (voiding) {
                        <div class="card card-outline card-danger d-print-none">
                            <div class="card-body">
                                <label for="vr">Why is this sale void? <span class="text-danger">*</span></label>
                                <input id="vr" class="form-control" [(ngModel)]="voiding.reason" maxlength="200" placeholder="e.g. entered twice, buyer cancelled" />
                                <small class="form-text text-muted">Its kilograms become unsold again. The number {{ s.dispatchReceiptNo }} is not reused.</small>
                            </div>
                            <div class="card-footer">
                                <button class="btn btn-danger mr-2" [disabled]="busy() || !voiding.reason.trim()" (click)="voidSale()">Void sale</button>
                                <button class="btn btn-default" (click)="voiding = null">Cancel</button>
                            </div>
                        </div>
                    }
                    <div class="fpms-paper fpms-print-doc">
                        <div class="d-flex justify-content-between mb-3">
                            <div>
                                <h4 class="mb-0">{{ s.farmName }}</h4>
                                <div class="small">{{ s.farmAddress }}</div>
                                <div class="small">{{ s.farmContact }}</div>
                                <div class="small">BFAR registration no.: {{ s.bfarRegistrationNo || '—' }}</div>
                                @if (s.sfscNos) {
                                    <div class="small">Fry source certificate (SFSC): {{ s.sfscNos }}</div>
                                }
                            </div>
                            <div class="text-right">
                                <h5 class="mb-0">DISPATCH RECEIPT</h5>
                                <div><strong>No. {{ s.dispatchReceiptNo }}</strong></div>
                                <div class="small">Date: {{ s.saleDate | farmDate }}</div>
                                @if (s.dueDate) { <div class="small">Due: {{ s.dueDate | farmDate }}</div> }
                            </div>
                        </div>
                        <div class="row mb-3">
                            <div class="col-6">
                                <div class="small text-muted">Buyer</div>
                                <strong>{{ s.buyerName }}</strong>
                                <div class="small">{{ s.buyerContact }} {{ s.buyerPhone ? '· ' + s.buyerPhone : '' }}</div>
                                <div class="small">{{ s.buyerAddress }}</div>
                            </div>
                            <div class="col-6 text-right">
                                <div class="small text-muted">Source</div>
                                <div>{{ s.batches }}</div>
                            </div>
                        </div>
                        <div class="table-responsive">
                        <table class="table table-bordered table-sm">
                            <thead><tr><th>Harvest</th><th>Size</th><th class="text-right">Farm kg</th><th class="text-right">Buyer kg</th><th class="text-right">Price per kg</th><th class="text-right">Amount</th></tr></thead>
                            <tbody>
                                @for (l of s.lines; track l.id) {
                                    <tr>
                                        <td>{{ l.harvestedAtUtc | farmDayOf }} · {{ l.pondCode }} · {{ l.batchCode }}</td>
                                        <td>{{ l.sizeLabel }}</td>
                                        <td class="text-right">{{ l.kg | num: 3 }}</td>
                                        <td class="text-right">{{ l.buyerAcceptedKg != null ? (l.buyerAcceptedKg | num: 3) : '' }}</td>
                                        <td class="text-right">{{ l.pricePerKg | php }}</td>
                                        <td class="text-right">{{ l.amount | php }}</td>
                                    </tr>
                                }
                            </tbody>
                            <tfoot>
                                <tr class="font-weight-bold"><td colspan="2">Total</td><td class="text-right">{{ s.totalKg | num: 3 }}</td><td class="text-right">{{ s.totalBuyerKg != null ? (s.totalBuyerKg | num: 3) : '' }}</td><td></td><td class="text-right">{{ s.total | php }}</td></tr>
                                <tr><td colspan="5" class="text-right">Paid</td><td class="text-right">{{ s.paid | php }}</td></tr>
                                <tr><td colspan="5" class="text-right">{{ s.overpaid > 0 ? 'Overpaid' : 'Balance' }}</td><td class="text-right">{{ (s.overpaid > 0 ? s.overpaid : s.balance) | php }}</td></tr>
                            </tfoot>
                        </table>
                        </div>
                        <p class="small mb-2">
                            Payment terms: {{ s.paymentTerms || '—' }} · Status: {{ s.paymentStatus | label: L.PaymentStatuses }}
                            @if (s.isOverdue) { <strong class="text-danger"> · Overdue</strong> }
                        </p>
                        @if (s.payments.length) {
                            <div class="table-responsive">
                            <table class="table table-sm small mb-3">
                                <thead><tr><th>Payment date</th><th>Method</th><th>Note</th><th class="text-right">Amount</th><th class="d-print-none"><span class="sr-only">Actions</span></th></tr></thead>
                                <tbody>
                                    @for (p of s.payments; track p.id) {
                                        <tr>
                                            <td>{{ p.paidOn | farmDate }}</td>
                                            <td>{{ p.method || '—' }}</td>
                                            <td>{{ p.note }}</td>
                                            <td class="text-right">{{ p.amount | php }}</td>
                                            <td class="text-right d-print-none">
                                                @if (canManage) {
                                                    <button class="btn btn-xs btn-link text-danger p-0" [disabled]="busy()" (click)="removePayment(p)">Remove</button>
                                                }
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                            </div>
                        }
                        @if (hasTransport()) {
                            <div class="small mb-3">
                                <div class="text-muted">Transport</div>
                                {{ s.transporter || '' }}{{ s.driverName ? ' · Driver ' + s.driverName : '' }}{{ s.plateNo ? ' · Plate ' + s.plateNo : '' }}
                                {{ s.loadedAtUtc ? ' · Loaded ' + (s.loadedAtUtc | farmDateTime) : '' }}{{ s.destination ? ' · To ' + s.destination : '' }}
                                {{ s.localTransportPermitNo ? ' · Permit ' + s.localTransportPermitNo : '' }}{{ s.temperatureNote ? ' · ' + s.temperatureNote : '' }}
                            </div>
                        }
                        <div class="row mt-5 mb-3 small">
                            <div class="col-4"><div class="fpms-sign">Released by (farm)</div></div>
                            <div class="col-4"><div class="fpms-sign">Driver</div></div>
                            <div class="col-4"><div class="fpms-sign">Received by (buyer)</div></div>
                        </div>
                        <p class="small text-center border-top pt-2 mb-0"><strong>{{ s.documentNotice }}</strong></p>
                        <p class="small text-center text-muted mb-0">Recorded by {{ s.recordedBy }} on {{ s.creationTime | farmDateTime }}</p>
                    </div>
                }
            </div>
        </section>
    `,
})
export class SalePrintComponent extends FpmsPage implements OnInit {
    tracedBatches(): { id: string; label: string }[] {
        const seen = new Map<string, string>();
        for (const l of this.s?.lines ?? []) {
            if (!seen.has(l.batchId)) {
                seen.set(l.batchId, `${l.pondCode} ${l.batchCode}`);
            }
        }
        return Array.from(seen, ([id, label]) => ({ id, label }));
    }

    s: any = null;
    voiding: any = null;
    canManage = false;

    ngOnInit(): void {
        this.canManage = this.can('Farm.Production.Manage');
        this.route.paramMap.subscribe(() => this.init());
    }

    async init(): Promise<void> {
        this.voiding = null;
        this.s = await this.api.get<any>('Sale', 'Get', { id: this.param('id') });
        this.setPageTitle(this.s ? `Dispatch receipt ${this.s.dispatchReceiptNo}` : null);
    }

    hasTransport(): boolean {
        const s = this.s;
        return !!(s.transporter || s.driverName || s.plateNo || s.loadedAtUtc || s.destination || s.localTransportPermitNo || s.temperatureNote);
    }

    async removePayment(p: any): Promise<void> {
        if (!(await this.confirm(`Remove the ${p.paidOn} payment of ₱${p.amount}?`))) return;
        let sale: any;
        if (await this.act(async () => (sale = await this.api.delete<any>('Sale', 'RemovePayment', { id: p.id })))) {
            this.notifySaved('Payment removed.');
            this.s = sale;
        }
    }

    async voidSale(): Promise<void> {
        if (await this.act(() => this.api.post('Sale', 'Void', { id: this.s.id, reason: this.voiding.reason.trim() }))) {
            this.notifySaved(`Sale ${this.s.dispatchReceiptNo} voided.`);
            this.router.navigate(['/app/sales'], { replaceUrl: true });
        }
    }
}
