import { Component, OnInit, computed, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { PagerState } from '@shared/fpms/pager';
import { EMPTY_GUID } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';
import { ToolbarChip } from '@shared/fpms/ui';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header
            title="Fry deliveries"
            subtitle="Post-larvae deliveries from hatcheries, with their status history.">
            @if (can('Farm.Reports')) {
                <button
                    class="btn btn-outline-secondary mr-2"
                    (click)="
                        exportCsv('ExportList', {
                            list: 'FryDeliveries',
                            deliveryStatus: status,
                            search: search || null,
                        })
                    "
                    [disabled]="busy()">
                    <i class="fas fa-file-csv mr-1"></i>
                    Export CSV
                </button>
            }
            @if (canManage) {
                <button class="btn btn-primary" (click)="startCreate()" [disabled]="!!form">
                    <i class="fas fa-plus mr-1"></i>
                    Record delivery
                </button>
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (form) {
                    <div class="fpms-dialog-backdrop"><div class="fpms-dialog" role="dialog" aria-modal="true"><div class="fpms-dialog-body p-0">
                        <div class="card card-outline card-primary">
                            <div class="card-header">
                                <h3 class="card-title">
                                    <i class="fas fa-truck" aria-hidden="true"></i>
                                    Record a fry delivery
                                </h3>
                            </div>
                            @if (!hatcheries().length) {
                                <div class="card-body">
                                    <fpms-empty text="Add an active hatchery first." icon="fas fa-egg">
                                        <a routerLink="/app/resources/hatcheries" class="btn btn-sm btn-primary">
                                            Go to hatchery sources
                                        </a>
                                    </fpms-empty>
                                </div>
                            } @else {
                                <form #f="ngForm" (ngSubmit)="save()" novalidate>
                                    <div class="card-body">
                                        <div class="form-row">
                                            <div class="form-group col-md-4">
                                                <label for="h">
                                                    Hatchery
                                                    <span class="text-danger">*</span>
                                                </label>
                                                <select
                                                    id="h"
                                                    class="form-control"
                                                    name="hatcheryId"
                                                    [(ngModel)]="form.hatcheryId"
                                                    required>
                                                    @for (h of hatcheries(); track h.id) {
                                                        <option [value]="h.id">{{ h.name }}</option>
                                                    }
                                                </select>
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label for="bn">
                                                    Hatchery batch number
                                                    <span class="text-danger">*</span>
                                                </label>
                                                <input
                                                    id="bn"
                                                    class="form-control"
                                                    name="hatcheryBatchNumber"
                                                    [(ngModel)]="form.hatcheryBatchNumber"
                                                    required
                                                    maxlength="100" />
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label for="dd">
                                                    Delivery date
                                                    <span class="text-danger">*</span>
                                                </label>
                                                <input
                                                    id="dd"
                                                    type="date"
                                                    class="form-control"
                                                    name="deliveryDate"
                                                    [(ngModel)]="form.deliveryDate"
                                                    required />
                                            </div>
                                        </div>
                                        <div class="form-row">
                                            <div class="form-group col-md-3">
                                                <label for="age">PL age (days)</label>
                                                <input
                                                    id="age"
                                                    type="number"
                                                    min="1"
                                                    max="60"
                                                    step="1"
                                                    class="form-control"
                                                    name="plAgeDays"
                                                    [(ngModel)]="form.plAgeDays"
                                                    placeholder="12 for PL12" />
                                                @if (form.plAgeDays && (form.plAgeDays < 10 || form.plAgeDays > 20)) {
                                                    <small class="text-warning-dark">
                                                        <i class="fas fa-exclamation-triangle mr-1"></i>
                                                        Outside the usual PL10–PL20 (the farm's range in Farm Standards
                                                        applies when saved).
                                                    </small>
                                                }
                                            </div>
                                            <div class="form-group col-md-3">
                                                <label for="pl">
                                                    Total PL received
                                                    <span class="text-danger">*</span>
                                                </label>
                                                <input
                                                    id="pl"
                                                    type="number"
                                                    min="1"
                                                    max="10000000"
                                                    step="1"
                                                    class="form-control"
                                                    name="plReceived"
                                                    [(ngModel)]="form.plReceived"
                                                    required />
                                            </div>
                                            <div class="form-group col-md-3">
                                                <label for="cost">Cost (₱)</label>
                                                <input
                                                    id="cost"
                                                    type="number"
                                                    min="0"
                                                    step="0.01"
                                                    class="form-control"
                                                    name="costPhp"
                                                    [(ngModel)]="form.costPhp" />
                                            </div>
                                            <div class="form-group col-md-3">
                                                <label for="st">
                                                    Status now
                                                    <span class="text-danger">*</span>
                                                </label>
                                                <select
                                                    id="st"
                                                    class="form-control"
                                                    name="initialStatus"
                                                    [(ngModel)]="form.initialStatus">
                                                    @for (o of Options.fryDeliveryStatus; track o.value) {
                                                        <option [ngValue]="o.value">{{ o.label }}</option>
                                                    }
                                                </select>
                                            </div>
                                        </div>
                                        <div class="form-row">
                                            <div class="form-group col-md-4">
                                                <label for="phc">PL health certificate no.</label>
                                                <input
                                                    id="phc"
                                                    class="form-control"
                                                    name="plHealthCertificateNo"
                                                    [(ngModel)]="form.plHealthCertificateNo"
                                                    maxlength="100" />
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label for="tpn">Transport permit (DTP/LTP)</label>
                                                <input
                                                    id="tpn"
                                                    class="form-control"
                                                    name="transportPermitNo"
                                                    [(ngModel)]="form.transportPermitNo"
                                                    maxlength="100"
                                                    placeholder="optional" />
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label for="sfsc">SFSC no.</label>
                                                <input
                                                    id="sfsc"
                                                    class="form-control"
                                                    name="sfscNo"
                                                    [(ngModel)]="form.sfscNo"
                                                    maxlength="100"
                                                    placeholder="optional" />
                                            </div>
                                        </div>
                                        <div class="form-group mb-0">
                                            <label for="n">Notes</label>
                                            <input
                                                id="n"
                                                class="form-control"
                                                name="notes"
                                                [(ngModel)]="form.notes"
                                                maxlength="500" />
                                        </div>
                                    </div>
                                    <div class="card-footer">
                                        <button
                                            type="submit"
                                            class="btn btn-primary mr-2"
                                            [disabled]="busy() || !f.form.valid">
                                            Save delivery
                                        </button>
                                        <button type="button" class="btn btn-default" (click)="form = null">Cancel</button>
                                        <fpms-form-hint [form]="f"></fpms-form-hint>
                                    </div>
                                </form>
                            }
                        </div>
                    </div></div></div>
                }
                <div class="card">
                    <div class="card-header">
                        <fpms-list-toolbar
                            placeholder="Search by hatchery lot, hatchery or certificate"
                            [search]="search"
                            [resultText]="resultText()"
                            [chips]="chips()"
                            [chip]="status"
                            (searchChange)="onSearch($event)"
                            (chipChange)="onStatus($event)"></fpms-list-toolbar>
                    </div>
                    <div class="card-body p-0 table-responsive">
                        @if (!list()) {
                            <fpms-loading></fpms-loading>
                        } @else if (!list().length) {
                            <fpms-empty
                                [text]="
                                    search || status !== null
                                        ? 'No deliveries match. Clear the search or the filter.'
                                        : 'No fry deliveries yet.'
                                "
                                icon="fas fa-truck"></fpms-empty>
                        } @else {
                            <table class="table table-hover mb-0">
                                <thead>
                                    <tr>
                                        <th fpmsSort="hatcheryBatchNumber" [pager]="pager" (sorted)="load()">
                                            Batch number
                                        </th>
                                        <th fpmsSort="hatcheryName" [pager]="pager" (sorted)="load()">Hatchery</th>
                                        <th fpmsSort="deliveryDate" [pager]="pager" (sorted)="load()">Delivered</th>
                                        <th class="text-right">PL age</th>
                                        <th class="text-right" fpmsSort="plReceived" [pager]="pager" (sorted)="load()">
                                            PL received
                                        </th>
                                        <th class="text-right">PL left</th>
                                        <th fpmsSort="status" [pager]="pager" (sorted)="load()">Status</th>
                                        <th>Cycle</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    @for (d of list(); track d.id) {
                                        <tr>
                                            <td>
                                                <a [routerLink]="['/app/resources/fry-deliveries', d.id]">
                                                    {{ d.hatcheryBatchNumber }}
                                                </a>
                                            </td>
                                            <td>{{ d.hatcheryName }}</td>
                                            <td class="text-nowrap">{{ d.deliveryDate | farmDate }}</td>
                                            <td class="text-right">
                                                {{ d.plAgeDays != null ? 'PL' + d.plAgeDays : '—' }}
                                            </td>
                                            <td class="text-right">{{ d.plReceived | num: 0 }}</td>
                                            <td class="text-right">{{ d.plLeft | num: 0 }}</td>
                                            <td>
                                                <fpms-badge
                                                    [text]="
                                                        d.isStocked
                                                            ? 'Stocked'
                                                            : (d.status | label: L.FryDeliveryStatuses)
                                                    "></fpms-badge>
                                            </td>
                                            <td>
                                                @if (d.cycleId) {
                                                    <a [routerLink]="['/app/production/cycles', d.cycleId]">
                                                        {{ d.cycleCode }}
                                                    </a>
                                                } @else {
                                                    <span class="text-muted">—</span>
                                                }
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        }
                    </div>
                    <fpms-pager [state]="pager" (changed)="load()"></fpms-pager>
                </div>
            </div>
        </section>
    `,
})
export class FryDeliveriesComponent extends FpmsPage implements OnInit {
    list = signal<any[] | null>(null);
    hatcheries = signal<any[]>([]);
    counts = signal<Record<number, number>>({});
    total = signal(0);
    status: number | null = null;
    search = '';
    pager = new PagerState('deliveryDate desc');
    form: any = null;

    get canManage(): boolean {
        return this.can('Farm.FryDeliveries.Manage');
    }

    chips = computed<ToolbarChip[]>(() => {
        const c = this.counts();
        const all = (Object.values(c) as number[]).reduce((n, v) => n + v, 0);
        return [
            { value: null, label: 'All statuses', count: all },
            ...this.Options.fryDeliveryStatus.map((o: any) => ({
                value: o.value,
                label: o.label,
                count: c[o.value] ?? 0,
            })),
        ];
    });

    resultText = computed(() => (this.total() === 1 ? '1 delivery' : `${this.total()} deliveries`));

    ngOnInit(): void {
        this.restoreList(this.pager, this, { status: 'number', search: 'string' });
        this.load();
    }

    onSearch(value: string): void {
        this.search = value;
        this.pager.reset();
        this.load();
    }

    onStatus(value: unknown): void {
        this.status = value as number | null;
        this.pager.reset();
        this.load();
    }

    async load(): Promise<void> {
        const q = { search: this.search || undefined };
        const [r, counts] = await Promise.all([
            this.api.get<any>('FryDelivery', 'GetAll', { status: this.status, ...q, ...this.pager.params }),
            this.api.get<any[]>('FryDelivery', 'GetStatusCounts', q),
        ]);
        this.total.set(r.totalCount);
        this.counts.set(Object.fromEntries((counts ?? []).map((x) => [x.status, x.count])));
        this.list.set(r.items);
        const reload = this.pager.update(r.totalCount);
        this.syncPager(this.pager);
        if (reload) {
            await this.load();
        }
    }

    async startCreate(): Promise<void> {
        const list = (await this.api.get<any[]>('Hatchery', 'GetLookup')).filter((h) => h.isActive);
        this.hatcheries.set(list);
        this.form = {
            id: EMPTY_GUID,
            hatcheryId: list[0]?.id ?? '',
            hatcheryBatchNumber: '',
            deliveryDate: this.today,
            plAgeDays: null,
            plReceived: null,
            costPhp: null,
            notes: '',
            plHealthCertificateNo: '',
            transportPermitNo: '',
            sfscNo: '',
            initialStatus: 1,
        };
    }

    async save(): Promise<void> {
        let result: any;
        if (await this.act(async () => (result = await this.api.post<any>('FryDelivery', 'Create', this.form)))) {
            this.notifySaved('Fry delivery recorded.');
            if (result?.warnings?.length) {
                this.showWarnings(result.warnings);
            }
            this.router.navigate(['/app/resources/fry-deliveries', result.id], { replaceUrl: true });
        }
    }
}
