import { Component, OnInit, computed, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { PagerState } from '@shared/fpms/pager';
import { FPMS_IMPORTS } from '../shared-imports';
import { ToolbarChip } from '@shared/fpms/ui';

const CLOSED = 5;

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Pond batches" subtitle="One batch per grow-out pond per cycle.">
            @if (can('Farm.Reports')) {
                <button
                    class="btn btn-outline-secondary"
                    (click)="
                        exportCsv('ExportList', {
                            list: 'Batches',
                            batchStatus: status,
                            openOnly: openOnly,
                            search: search || null,
                        })
                    "
                    [disabled]="busy()">
                    <i class="fas fa-file-csv mr-1"></i>
                    Export CSV
                </button>
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <div class="card">
                    <div class="card-header">
                        <fpms-list-toolbar
                            placeholder="Search by pond, batch or cycle"
                            [search]="search"
                            [resultText]="resultText()"
                            [chips]="chips()"
                            [chip]="chipValue"
                            (searchChange)="onSearch($event)"
                            (chipChange)="onStatus($event)"></fpms-list-toolbar>
                    </div>
                    <div class="card-body p-0 table-responsive">
                        @if (!batches()) {
                            <fpms-loading></fpms-loading>
                        } @else if (!batches().length) {
                            <fpms-empty
                                text="No batches match. Clear the search, or choose another status."
                                icon="fas fa-seedling">
                                @if (can('Farm.Production.Stock')) {
                                    <a routerLink="/app/production/create-batch" class="btn btn-sm btn-primary">
                                        Stock a batch
                                    </a>
                                }
                            </fpms-empty>
                        } @else {
                            <table class="table table-hover mb-0">
                                <thead>
                                    <tr>
                                        <th fpmsSort="batchCode" [pager]="pager" (sorted)="load()">Batch</th>
                                        <th fpmsSort="cycleCode" [pager]="pager" (sorted)="load()">Cycle</th>
                                        <th fpmsSort="pondCode" [pager]="pager" (sorted)="load()">Pond</th>
                                        <th fpmsSort="status" [pager]="pager" (sorted)="load()">Status</th>
                                        <th fpmsSort="stockingDate" [pager]="pager" (sorted)="load()">Stocking</th>
                                        <th
                                            class="text-right"
                                            fpmsSort="plCountStocked"
                                            [pager]="pager"
                                            (sorted)="load()">
                                            PL
                                        </th>
                                        <th class="text-right">DOC</th>
                                        <th class="text-right">ABW (g)</th>
                                        <th class="text-right">FCR</th>
                                        <th class="text-right">Est. survival</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    @for (b of batches(); track b.id) {
                                        <tr>
                                            <td>
                                                <a [routerLink]="['/app/production/batches', b.id]">
                                                    {{ b.batchCode }}
                                                </a>
                                            </td>
                                            <td>
                                                <a [routerLink]="['/app/production/cycles', b.cycleId]">
                                                    {{ b.cycleCode }}
                                                </a>
                                            </td>
                                            <td>{{ b.pondCode }}</td>
                                            <td>
                                                <fpms-badge [text]="b.status | label: L.BatchStatuses"></fpms-badge>
                                            </td>
                                            <td class="text-nowrap">
                                                {{ b.stockingDate | farmDate }}
                                            </td>
                                            <td class="text-right">{{ b.plCountStocked | num: 0 }}</td>
                                            <td class="text-right">{{ b.doc ?? '—' }}</td>
                                            <td class="text-right">{{ b.currentAbwG | num: 2 }}</td>
                                            <td class="text-right">{{ b.fcrToDate | num: 2 }}</td>
                                            <td class="text-right">
                                                @if (
                                                    b.estimatedSurvivalPct !== null &&
                                                    b.estimatedSurvivalPct !== undefined
                                                ) {
                                                    {{ b.estimatedSurvivalPct | num: 1 }}%
                                                } @else {
                                                    —
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
export class BatchesComponent extends FpmsPage implements OnInit {
    batches = signal<any[] | null>(null);
    counts = signal<Record<number, number>>({});
    status: number | null = null;
    openOnly = true;
    search = '';
    pager = new PagerState('default');

    chips = computed<ToolbarChip[]>(() => {
        const c = this.counts();
        const all = (Object.values(c) as number[]).reduce((n, v) => n + v, 0);
        const closed = c[CLOSED] ?? 0;
        return [
            { value: 'open', label: 'Open batches', count: all - closed },
            ...this.Options.batchStatus.map((o: any) => ({ value: o.value, label: o.label, count: c[o.value] ?? 0 })),
            { value: 'all', label: 'All batches', count: all },
        ];
    });

    get chipValue(): unknown {
        return this.status !== null ? this.status : this.openOnly ? 'open' : 'all';
    }

    resultText = computed(() => {
        const t = this.total();
        return t === 1 ? '1 batch' : `${t} batches`;
    });

    private total = signal(0);

    ngOnInit(): void {
        this.restoreList(this.pager, this, { status: 'number', openOnly: 'boolean', search: 'string' });
        this.load();
    }

    onSearch(value: string): void {
        this.search = value;
        this.pager.reset();
        this.load();
    }

    onStatus(value: unknown): void {
        this.status = typeof value === 'number' ? value : null;
        this.openOnly = value === 'open';
        this.pager.reset();
        this.load();
    }

    async load(): Promise<void> {
        const [r, counts] = await Promise.all([
            this.api.get<any>('Batch', 'GetPage', {
                status: this.status,
                openOnly: this.openOnly,
                search: this.search || undefined,
                ...this.pager.params,
            }),
            this.api.get<any[]>('Batch', 'GetStatusCounts', {
                search: this.search || undefined,
            }),
        ]);
        this.batches.set(r.items);
        this.total.set(r.totalCount);
        this.counts.set(Object.fromEntries((counts ?? []).map((x) => [x.status, x.count])));
        const reload = this.pager.update(r.totalCount);
        this.syncPager(this.pager);
        if (reload) {
            await this.load();
        }
    }
}
