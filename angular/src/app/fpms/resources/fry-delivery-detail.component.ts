import { Component, OnInit } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header [title]="d?.hatcheryBatchNumber ?? 'Fry delivery'" [subtitle]="d ? 'Post-larvae delivery from ' + d.hatcheryName + '. Delivered ' + (d.deliveryDate | farmDate) + '.' : ''" backLink="/app/resources/fry-deliveries" backLabel="Fry deliveries">
            @if (d) {
                <fpms-badge [text]="d.isStocked ? 'Stocked' : (d.status | label: L.FryDeliveryStatuses)"></fpms-badge>
                @if (d.isStockingFinished) { <span class="badge badge-dark ml-1">Stocking finished</span> }
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (!d) {
                    <fpms-loading></fpms-loading>
                } @else {
                    <div class="row">
                        <div class="col-lg-7">
                            <form #f="ngForm" (ngSubmit)="save()" novalidate>
                                <fieldset [disabled]="!d.canEdit">
                                    <div class="card">
                                        <div class="card-header"><h3 class="card-title"><i class="fas fa-truck" aria-hidden="true"></i>Delivery</h3></div>
                                        <div class="card-body">
                                            <div class="form-row">
                                                <div class="form-group col-md-6">
                                                    <label for="bn">Hatchery batch number <span class="text-danger">*</span></label>
                                                    <input id="bn" class="form-control" name="hatcheryBatchNumber" [(ngModel)]="m.hatcheryBatchNumber" required maxlength="100" />
                                                </div>
                                                <div class="form-group col-md-6">
                                                    <label for="dd">Delivery date <span class="text-danger">*</span></label>
                                                    <input id="dd" type="date" class="form-control" name="deliveryDate" [(ngModel)]="m.deliveryDate" required />
                                                </div>
                                            </div>
                                            <div class="form-row">
                                                <div class="form-group col-md-4">
                                                    <label for="age">PL age (days)</label>
                                                    <input id="age" type="number" min="1" max="60" class="form-control" name="plAgeDays" [(ngModel)]="m.plAgeDays" />
                                                </div>
                                                <div class="form-group col-md-4">
                                                    <label for="pl">PL received <span class="text-danger">*</span></label>
                                                    <input id="pl" type="number" min="1" max="10000000" class="form-control" name="plReceived" [(ngModel)]="m.plReceived" required />
                                                    @if (d.isStocked) {
                                                        <small class="form-text text-muted">{{ d.plStocked | num: 0 }} PL already stocked.{{ d.isStockingFinished ? ' Fixed after Finish stocking.' : '' }}</small>
                                                    }
                                                </div>
                                                <div class="form-group col-md-4">
                                                    <label for="cost">Cost (₱)</label>
                                                    <input id="cost" type="number" min="0" step="0.01" class="form-control" name="costPhp" [(ngModel)]="m.costPhp" />
                                                </div>
                                            </div>
                                            <div class="form-row">
                                                <div class="form-group col-md-4">
                                                    <label for="phc">PL health certificate no.</label>
                                                    <input id="phc" class="form-control" name="plHealthCertificateNo" [(ngModel)]="m.plHealthCertificateNo" maxlength="100" />
                                                </div>
                                                <div class="form-group col-md-4">
                                                    <label for="tpn">Transport permit (DTP/LTP)</label>
                                                    <input id="tpn" class="form-control" name="transportPermitNo" [(ngModel)]="m.transportPermitNo" maxlength="100" placeholder="optional" />
                                                </div>
                                                <div class="form-group col-md-4">
                                                    <label for="sfsc">SFSC no.</label>
                                                    <input id="sfsc" class="form-control" name="sfscNo" [(ngModel)]="m.sfscNo" maxlength="100" placeholder="optional" />
                                                </div>
                                            </div>
                                            <div class="form-group mb-0">
                                                <label for="n">Notes</label>
                                                <input id="n" class="form-control" name="notes" [(ngModel)]="m.notes" maxlength="500" />
                                            </div>
                                        </div>
                                        @if (d.canEdit) {
                                            <div class="card-footer">
                                                <button type="submit" class="btn btn-primary" [disabled]="busy() || !f.form.valid">Save</button>
                                                <fpms-form-hint [form]="f"></fpms-form-hint>
                                            </div>
                                        }
                                    </div>
                                </fieldset>
                            </form>
                            @if (d.cycleId) {
                                <p>
                                    Stocked into cycle <a [routerLink]="['/app/production/cycles', d.cycleId]">{{ d.cycleCode }}</a>:
                                    {{ d.plStocked | num: 0 }} PL stocked{{ d.isStockingFinished ? ', ' + (d.plNotStocked | num: 0) + ' PL not stocked' : ', ' + (d.plLeft | num: 0) + ' PL left' }}.
                                </p>
                            }
                            @if (d.isStockingFinished) {
                                <div class="card">
                                    <div class="card-header"><h3 class="card-title"><i class="fas fa-shrimp" aria-hidden="true"></i>Stocking finished</h3></div>
                                    <div class="card-body py-2 small">
                                        <p class="mb-1">Finished {{ d.stockingFinishedAtUtc | farmDateTime }} by {{ d.stockingFinishedBy }}. PL received = stocked + not stocked.</p>
                                        @for (s of d.shortfalls; track s.reason) {
                                            <div>{{ s.reason | label: L.ShortfallReasons }}: <strong>{{ s.count | num: 0 }} PL</strong>{{ s.note ? ' · ' + s.note : '' }}</div>
                                        } @empty {
                                            <div class="text-muted">Every PL received was stocked.</div>
                                        }
                                    </div>
                                </div>
                            } @else if (d.canFinishStocking) {
                                <div class="card card-outline card-primary">
                                    <div class="card-header"><h3 class="card-title"><i class="fas fa-shrimp" aria-hidden="true"></i>Finish stocking</h3></div>
                                    <div class="card-body">
                                        <p class="small">
                                            Record the PL from this delivery that were not stocked, so the delivery adds up. Afterwards no more ponds can be stocked from it.
                                            <strong>{{ d.plReceived - d.plStocked | num: 0 }} PL</strong> to account for ({{ d.plReceived | num: 0 }} received − {{ d.plStocked | num: 0 }} stocked).
                                        </p>
                                        @for (s of shortfalls; track $index) {
                                            <div class="form-row align-items-end">
                                                <div class="form-group col-5">
                                                    <label class="small" [for]="'sr' + $index">Reason</label>
                                                    <select [id]="'sr' + $index" class="form-control form-control-sm" [(ngModel)]="s.reason">
                                                        @for (o of Options.shortfallReason; track o.value) { <option [ngValue]="o.value">{{ o.label }}</option> }
                                                    </select>
                                                </div>
                                                <div class="form-group col-3">
                                                    <label class="small" [for]="'sc' + $index">PL</label>
                                                    <input [id]="'sc' + $index" type="number" min="1" step="1" class="form-control form-control-sm" [(ngModel)]="s.count" />
                                                </div>
                                                <div class="form-group col-3">
                                                    <label class="small" [for]="'sn' + $index">Note</label>
                                                    <input [id]="'sn' + $index" class="form-control form-control-sm" [(ngModel)]="s.note" maxlength="500" />
                                                </div>
                                                <div class="form-group col-1">
                                                    <button type="button" class="btn btn-sm btn-link text-danger px-0" (click)="shortfalls.splice($index, 1)" aria-label="Remove line"><i class="fas fa-times"></i></button>
                                                </div>
                                            </div>
                                        }
                                        <button type="button" class="btn btn-sm btn-link px-0" (click)="addShortfall()"><i class="fas fa-plus mr-1"></i>Add a reason</button>
                                        <div class="small mt-1" [class.text-danger]="shortfallGap !== 0" [class.text-success]="shortfallGap === 0">
                                            Stocked {{ d.plStocked | num: 0 }} + not stocked {{ shortfallTotal | num: 0 }} = {{ d.plStocked + shortfallTotal | num: 0 }} of {{ d.plReceived | num: 0 }} received
                                            {{ shortfallGap === 0 ? '✓' : '(' + (shortfallGap | num: 0) + ' still to account for)' }}
                                        </div>
                                    </div>
                                    <div class="card-footer">
                                        <button class="btn btn-primary" [disabled]="busy() || shortfallGap !== 0 || !shortfallsValid()" (click)="finishStocking()">Finish stocking</button>
                                    </div>
                                </div>
                            } @else if (d.status === 3 && can('Farm.Production.Stock')) {
                                <a class="btn btn-outline-primary mb-3" routerLink="/app/production/create-batch"><i class="fas fa-shrimp mr-1"></i>Create Batch (Stocking) from this delivery</a>
                            }
                        </div>
                        <div class="col-lg-5">
                            @if (d.canUpdateStatus) {
                                <div class="card">
                                    <div class="card-header"><h3 class="card-title"><i class="fas fa-flag" aria-hidden="true"></i>Update status</h3></div>
                                    <div class="card-body">
                                        <div class="form-row">
                                            <div class="form-group col-6">
                                                <label for="ns">New status</label>
                                                <select id="ns" class="form-control" [(ngModel)]="change.status">
                                                    @for (o of Options.fryDeliveryStatus; track o.value) {
                                                        <option [ngValue]="o.value" [disabled]="o.value === d.status">{{ o.label }}</option>
                                                    }
                                                </select>
                                            </div>
                                            <div class="form-group col-6">
                                                <label for="ca">When</label>
                                                <input id="ca" type="datetime-local" class="form-control" [(ngModel)]="change.changedAt" [max]="nowInput" />
                                            </div>
                                        </div>
                                        <div class="form-group">
                                            <label for="cn">Note</label>
                                            <input id="cn" class="form-control" [(ngModel)]="change.note" maxlength="500" placeholder="e.g. arrived 6 AM, 2 bags damaged" />
                                        </div>
                                        <button class="btn btn-primary btn-sm" [disabled]="busy() || !change.status || change.status === d.status" (click)="updateStatus()">Save status</button>
                                    </div>
                                </div>
                            }
                            <div class="card">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-history" aria-hidden="true"></i>Status history</h3></div>
                                <ul class="list-group list-group-flush">
                                    @for (h of d.history; track $index) {
                                        <li class="list-group-item py-2">
                                            <div><fpms-badge [text]="h.status | label: L.FryDeliveryStatuses"></fpms-badge> <span class="small text-muted ml-1">{{ h.changedAtUtc | farmDateTime }} · {{ h.changedBy }}</span></div>
                                            @if (h.note) { <div class="small">{{ h.note }}</div> }
                                        </li>
                                    }
                                </ul>
                            </div>
                        </div>
                    </div>
                }
            </div>
        </section>
    `,
})
export class FryDeliveryDetailComponent extends FpmsPage implements OnInit {
    d: any = null;
    m: any = {};
    change: any = {};
    shortfalls: { reason: number; count: number | null; note: string }[] = [];
    readonly nowInput = FarmTime.nowInput();

    get shortfallTotal(): number {
        return this.shortfalls.reduce((sum, s) => sum + (Number(s.count) || 0), 0);
    }

    get shortfallGap(): number {
        return this.d ? this.d.plReceived - this.d.plStocked - this.shortfallTotal : 0;
    }

    shortfallsValid(): boolean {
        const reasons = this.shortfalls.map((s) => s.reason);
        return new Set(reasons).size === reasons.length && this.shortfalls.every((s) => s.reason && Number(s.count) > 0);
    }

    addShortfall(): void {
        const used = this.shortfalls.map((s) => s.reason);
        const next = this.Options.shortfallReason.find((o) => !used.includes(o.value));
        this.shortfalls.push({ reason: next?.value ?? 1, count: null, note: '' });
    }

    async finishStocking(): Promise<void> {
        const body = {
            deliveryId: this.d.id,
            rowVersion: this.d.rowVersion,
            shortfalls: this.shortfalls.map((s) => ({ reason: s.reason, count: Number(s.count), note: s.note })),
        };
        if (await this.act(() => this.api.post('FryDelivery', 'FinishStocking', body))) {
            this.notifySaved('Stocking finished. The delivery is accounted for.');
            this.load();
        }
    }

    ngOnInit(): void {
        this.route.paramMap.subscribe(() => this.load());
    }

    async load(): Promise<void> {
        const d = await this.api.get<any>('FryDelivery', 'Get', { id: this.param('id') });
        this.m = { ...d };
        this.change = { status: null, changedAt: FarmTime.nowInput(), note: '' };
        const gap = d.plReceived - d.plStocked;
        this.shortfalls = gap > 0 ? [{ reason: 1, count: gap, note: '' }] : [];
        this.d = d;
        this.setPageTitle(d ? `Fry delivery ${d.hatcheryBatchNumber}` : null);
    }

    async save(): Promise<void> {
        let result: any;
        if (await this.act(async () => (result = await this.api.put<any>('FryDelivery', 'Update', this.m)))) {
            this.notifySaved('Delivery saved.');
            if (result?.warnings?.length) {
                this.showWarnings(result.warnings);
            }
            this.load();
        }
    }

    async updateStatus(): Promise<void> {
        const body = { deliveryId: this.d.id, status: this.change.status, changedAt: this.change.changedAt ? FarmTime.fromLocalInput(this.change.changedAt) : null, note: this.change.note };
        if (await this.act(() => this.api.put('FryDelivery', 'UpdateStatus', body))) {
            this.notifySaved('Status updated.');
            this.load();
        }
    }
}
