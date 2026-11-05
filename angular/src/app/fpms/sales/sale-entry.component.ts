import { Component, OnInit } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header [narrow]="true" [title]="isNew ? 'Record sale' : 'Edit sale ' + (original?.dispatchReceiptNo ?? '')" [subtitle]="isNew ? 'Sell harvested kilograms to a buyer. The dispatch receipt number is made when you save.' : ''" backLink="/app/sales" backLabel="Sales"></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid fpms-narrow">
                @if (loading) {
                    <fpms-loading></fpms-loading>
                } @else if (!buyers.length || !sellable.length) {
                    <div class="card">
                        <div class="card-body">
                            @if (!buyers.length) {
                                <fpms-empty text="Add an active buyer first." icon="fas fa-handshake"><a routerLink="/app/resources/buyers" class="btn btn-sm btn-primary">Go to buyers</a></fpms-empty>
                            } @else {
                                <fpms-empty text="No harvest grade has unsold kilograms. Record a harvest with its size grades first." icon="fas fa-shrimp"><a routerLink="/app/harvests" class="btn btn-sm btn-primary">Go to harvests</a></fpms-empty>
                            }
                        </div>
                    </div>
                } @else {
                    <form #f="ngForm" (ngSubmit)="review = true" novalidate>
                        <div class="card">
                            <div class="card-body">
                                <div class="form-row">
                                    <div class="form-group col-md-6">
                                        <label for="by">Buyer <span class="text-danger">*</span></label>
                                        <select id="by" class="form-control" name="buyerId" [(ngModel)]="m.buyerId" required>
                                            @for (b of buyers; track b.id) { <option [value]="b.id">{{ b.name }}</option> }
                                        </select>
                                    </div>
                                    <div class="form-group col-md-3">
                                        <label for="sd">Sale date <span class="text-danger">*</span></label>
                                        <input id="sd" type="date" class="form-control" name="saleDate" [(ngModel)]="m.saleDate" required [max]="today" />
                                    </div>
                                    <div class="form-group col-md-3">
                                        <label for="dd">Due date</label>
                                        <input id="dd" type="date" class="form-control" name="dueDate" [(ngModel)]="m.dueDate" [min]="m.saleDate" />
                                        <small class="form-text text-muted">For credit; shows Overdue when unpaid after it.</small>
                                    </div>
                                </div>
                                <div class="form-group mb-0">
                                    <label for="pt">Payment terms</label>
                                    <input id="pt" class="form-control" name="paymentTerms" [(ngModel)]="m.paymentTerms" maxlength="200" placeholder="e.g. cash, 50% on delivery, balance in 15 days" />
                                </div>
                            </div>
                        </div>
                        <div class="card">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-cash-register" aria-hidden="true"></i>What was sold</h3></div>
                            <div class="card-body p-0 table-responsive">
                                <table class="table table-sm mb-0">
                                    <thead><tr><th>Harvest grade</th><th style="width: 7rem">Farm kg</th><th style="width: 7rem">Buyer kg</th><th style="width: 8rem">Price / kg (₱)</th><th class="text-right">Amount</th><th><span class="sr-only">Actions</span></th></tr></thead>
                                    <tbody>
                                        @for (l of m.lines; track $index) {
                                            <tr>
                                                <td>
                                                    <select class="form-control form-control-sm" [name]="'g' + $index" [(ngModel)]="l.harvestGradeLineId" required aria-label="Harvest grade">
                                                        @for (o of sellable; track o.harvestGradeLineId) {
                                                            <option [value]="o.harvestGradeLineId" [disabled]="used(o.harvestGradeLineId, l)">{{ o.harvestDate | farmDate }} · {{ o.pondCode }} · {{ o.sizeLabel }} ({{ unsold(o) | num: 3 }} kg left)</option>
                                                        }
                                                    </select>
                                                    @if (over(l)) { <small class="text-danger">More than the {{ unsold(option(l)) | num: 3 }} kg unsold.</small> }
                                                </td>
                                                <td><input type="number" min="0.001" step="0.001" class="form-control form-control-sm" [name]="'k' + $index" [(ngModel)]="l.kg" required aria-label="Farm kg" /></td>
                                                <td>
                                                    <input type="number" min="0" step="0.001" class="form-control form-control-sm" [name]="'bk' + $index" [(ngModel)]="l.buyerAcceptedKg" aria-label="Buyer's accepted kg" placeholder="same" />
                                                    @if (diffPct(l) > 10) { <small class="text-warning-dark">{{ diffPct(l) | num: 1 }}% off</small> }
                                                </td>
                                                <td><input type="number" min="0" step="0.01" class="form-control form-control-sm" [name]="'p' + $index" [(ngModel)]="l.pricePerKg" required aria-label="Price per kg" /></td>
                                                <td class="text-right">{{ amount(l) | php }}</td>
                                                <td class="text-right">
                                                    @if (m.lines.length > 1) {
                                                        <button type="button" class="btn btn-sm btn-outline-danger" (click)="m.lines.splice($index, 1)" aria-label="Remove"><i class="fas fa-times"></i></button>
                                                    }
                                                </td>
                                            </tr>
                                        }
                                    </tbody>
                                    <tfoot>
                                        <tr>
                                            <td><button type="button" class="btn btn-sm btn-outline-secondary" (click)="addLine()" [disabled]="m.lines.length >= sellable.length"><i class="fas fa-plus mr-1"></i>Add grade</button></td>
                                            <td><strong>{{ totalKg() | num: 3 }}</strong></td>
                                            <td></td>
                                            <td></td>
                                            <td class="text-right"><strong>{{ total() | php }}</strong></td>
                                            <td></td>
                                        </tr>
                                    </tfoot>
                                </table>
                                <p class="small text-muted px-3 my-2">Leave "Buyer kg" empty when the buyer pays on the farm's weight. When filled, the amount uses it; yield and FCR keep the farm weight.</p>
                            </div>
                        </div>
                        <div class="card">
                            <div class="card-header">
                                <a href="javascript:;" class="card-title text-reset" role="button" (click)="showTransport = !showTransport" [attr.aria-expanded]="showTransport">
                                    <i class="fas mr-1" [class.fa-chevron-right]="!showTransport" [class.fa-chevron-down]="showTransport" aria-hidden="true"></i>Transport details (optional)
                                </a>
                            </div>
                            @if (showTransport) {
                                <div class="card-body">
                                    <div class="form-row">
                                        <div class="form-group col-md-4"><label for="tr">Transporter</label><input id="tr" class="form-control" name="transporter" [(ngModel)]="m.transporter" maxlength="100" /></div>
                                        <div class="form-group col-md-4"><label for="dr">Driver</label><input id="dr" class="form-control" name="driverName" [(ngModel)]="m.driverName" maxlength="100" /></div>
                                        <div class="form-group col-md-4"><label for="pl">Plate no.</label><input id="pl" class="form-control" name="plateNo" [(ngModel)]="m.plateNo" maxlength="20" /></div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group col-md-4"><label for="la">Loaded at</label><input id="la" type="datetime-local" class="form-control" name="loadedAt" [(ngModel)]="m.loadedAt" /></div>
                                        <div class="form-group col-md-4"><label for="de">Destination</label><input id="de" class="form-control" name="destination" [(ngModel)]="m.destination" maxlength="200" /></div>
                                        <div class="form-group col-md-4"><label for="ltp">Local transport permit no.</label><input id="ltp" class="form-control" name="localTransportPermitNo" [(ngModel)]="m.localTransportPermitNo" maxlength="100" /></div>
                                    </div>
                                    <div class="form-group mb-0"><label for="tn">Temperature / icing note</label><input id="tn" class="form-control" name="temperatureNote" [(ngModel)]="m.temperatureNote" maxlength="200" placeholder="e.g. iced 1:1, 4 °C" /></div>
                                </div>
                            }
                        </div>
                        @if (isNew) {
                            <div class="card">
                                <div class="card-body">
                                    <div class="form-row">
                                        <div class="form-group col-md-4 mb-0">
                                            <label for="paid">Paid now (₱)</label>
                                            <input id="paid" type="number" min="0" step="0.01" class="form-control" name="paidNow" [(ngModel)]="m.paidNow" />
                                        </div>
                                        <div class="form-group col-md-4 mb-0">
                                            <label for="pm">Method</label>
                                            <input id="pm" class="form-control" name="paidMethod" [(ngModel)]="m.paidMethod" maxlength="50" placeholder="cash, bank transfer, GCash" />
                                        </div>
                                        <div class="form-group col-md-4 mb-0 d-flex align-items-end">
                                            <button type="button" class="btn btn-sm btn-outline-secondary" (click)="m.paidNow = total()">Paid in full</button>
                                        </div>
                                    </div>
                                    <small class="form-text text-muted">Later payments are recorded from the sales list or the dispatch receipt.</small>
                                </div>
                            </div>
                        }

                        @if (!review) {
                            <button type="submit" class="btn btn-primary btn-lg" [disabled]="!f.form.valid || !validLines().length || anyOver()">Review sale</button>
                            <fpms-form-hint [form]="f"></fpms-form-hint>
                        } @else {
                            <div class="card card-outline card-primary">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-clipboard-check" aria-hidden="true"></i>Check before saving</h3></div>
                                <div class="card-body">
                                    <p class="mb-1">{{ buyerName() }} · {{ m.saleDate | farmDate }} · {{ validLines().length }} grade line(s){{ m.dueDate ? ' · due ' + (m.dueDate | farmDate) : '' }}</p>
                                    <p class="mb-1">Total <strong>{{ totalKg() | num: 3 }} kg</strong> for <strong>{{ total() | php }}</strong>{{ isNew ? '; paid now ' + ((m.paidNow || 0) | php) : '' }}.</p>
                                    <p class="small text-muted mb-0">{{ isNew ? 'A dispatch receipt number is assigned when you save. ' : '' }}The dispatch receipt is for the farm's records, not an official receipt.</p>
                                </div>
                                <div class="card-footer">
                                    <button type="button" class="btn btn-primary mr-2" [disabled]="busy()" (click)="save()">Save sale</button>
                                    <button type="button" class="btn btn-default" (click)="review = false">Back</button>
                                </div>
                            </div>
                        }
                    </form>
                }
            </div>
        </section>
    `,
})
export class SaleEntryComponent extends FpmsPage implements OnInit {
    loading = true;
    isNew = true;
    review = false;
    showTransport = false;
    buyers: any[] = [];
    sellable: any[] = [];
    original: any = null;
    m: any = { lines: [] };

    ngOnInit(): void {
        this.route.paramMap.subscribe(() => this.init());
    }

    async init(): Promise<void> {
        const id = this.param('id');
        this.isNew = !id;
        this.review = false;
        this.original = this.isNew ? null : await this.api.get<any>('Sale', 'Get', { id });
        const [buyers, sellable] = await Promise.all([
            this.api.get<any[]>('Buyer', 'GetLookup'),
            this.api.get<any[]>('Sale', 'GetSellableLines', { saleId: this.original?.id }),
        ]);
        this.buyers = buyers.filter((b) => b.isActive || b.id === this.original?.buyerId);
        this.sellable = sellable;
        if (this.original) {
            const o = this.original;
            this.m = {
                id: o.id, buyerId: o.buyerId, saleDate: o.saleDate, dueDate: o.dueDate ?? '', paymentTerms: o.paymentTerms ?? '',
                transporter: o.transporter, driverName: o.driverName, plateNo: o.plateNo, loadedAt: o.loadedAtUtc ? FarmTime.toLocalInput(o.loadedAtUtc) : '',
                destination: o.destination, localTransportPermitNo: o.localTransportPermitNo, temperatureNote: o.temperatureNote,
                lines: o.lines.map((l: any) => ({ harvestGradeLineId: l.harvestGradeLineId, kg: l.kg, buyerAcceptedKg: l.buyerAcceptedKg, pricePerKg: l.pricePerKg })),
            };
            this.showTransport = !!(o.transporter || o.driverName || o.plateNo || o.loadedAtUtc || o.destination || o.localTransportPermitNo || o.temperatureNote);
        } else {
            this.m = {
                buyerId: this.buyers[0]?.id ?? '', saleDate: this.today, dueDate: '', paymentTerms: '', paidNow: null, paidMethod: '',
                lines: this.sellable.length ? [{ harvestGradeLineId: this.sellable[0].harvestGradeLineId, kg: null, buyerAcceptedKg: null, pricePerKg: null }] : [],
            };
        }
        this.loading = false;
    }

    option(l: any): any {
        return this.sellable.find((o) => o.harvestGradeLineId === l.harvestGradeLineId);
    }

    unsold(o: any): number {
        return o ? o.unsoldKg : 0;
    }

    used(id: string, self: any): boolean {
        return this.m.lines.some((l: any) => l !== self && l.harvestGradeLineId === id);
    }

    over(l: any): boolean {
        const o = this.option(l);
        return !!o && Number(l.kg) > o.unsoldKg + 0.0005;
    }

    anyOver(): boolean {
        return this.m.lines.some((l: any) => this.over(l));
    }

    addLine(): void {
        const free = this.sellable.find((o) => !this.m.lines.some((l: any) => l.harvestGradeLineId === o.harvestGradeLineId));
        this.m.lines.push({ harvestGradeLineId: free?.harvestGradeLineId ?? '', kg: null, buyerAcceptedKg: null, pricePerKg: null });
    }

    private num(v: any): number | null {
        return v === '' || v == null ? null : Number(v);
    }

    amount(l: any): number {
        const kg = this.num(l.buyerAcceptedKg) ?? (Number(l.kg) || 0);
        return Math.round(kg * (Number(l.pricePerKg) || 0) * 100) / 100;
    }

    diffPct(l: any): number {
        const b = this.num(l.buyerAcceptedKg);
        return b == null || !(l.kg > 0) ? 0 : (Math.abs(b - l.kg) / l.kg) * 100;
    }

    validLines(): any[] {
        return (this.m.lines ?? []).filter((l: any) => l.harvestGradeLineId && l.kg > 0 && l.pricePerKg !== null && l.pricePerKg !== '' && l.pricePerKg >= 0);
    }

    totalKg(): number {
        return (this.m.lines ?? []).reduce((s: number, l: any) => s + (Number(l.kg) || 0), 0);
    }

    total(): number {
        return (this.m.lines ?? []).reduce((s: number, l: any) => s + this.amount(l), 0);
    }

    buyerName(): string {
        return this.buyers.find((b) => b.id === this.m.buyerId)?.name ?? '';
    }

    async save(): Promise<void> {
        const body: any = {
            id: this.m.id, buyerId: this.m.buyerId, saleDate: this.m.saleDate, dueDate: this.m.dueDate || null, paymentTerms: this.m.paymentTerms,
            transporter: this.m.transporter, driverName: this.m.driverName, plateNo: this.m.plateNo,
            loadedAt: this.m.loadedAt ? FarmTime.fromLocalInput(this.m.loadedAt) : null, destination: this.m.destination,
            localTransportPermitNo: this.m.localTransportPermitNo, temperatureNote: this.m.temperatureNote,
            lines: this.validLines().map((l: any) => ({ harvestGradeLineId: l.harvestGradeLineId, kg: Number(l.kg), buyerAcceptedKg: this.num(l.buyerAcceptedKg), pricePerKg: Number(l.pricePerKg) })),
        };
        if (this.isNew && Number(this.m.paidNow) > 0) {
            body.initialPayment = { paidOn: this.m.saleDate, amount: Number(this.m.paidNow), method: this.m.paidMethod };
        }
        let sale: any;
        const ok = await this.act(async () => (sale = this.isNew ? await this.api.post<any>('Sale', 'Create', body) : await this.api.put<any>('Sale', 'Update', body)));
        if (ok) {
            this.notifySaved(this.isNew ? `Sale saved as ${sale.dispatchReceiptNo}.` : 'Sale saved.');
            if (sale?.warnings?.length) {
                this.showWarnings(sale.warnings);
            }
            this.router.navigate(['/app/sales', sale.id], { replaceUrl: true });
        }
    }
}
