import { Component, OnInit, computed, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';
import { CalendarCell, LOG_CALENDAR_STYLES, buildCalendarCells } from '../log-calendar';

interface StatusRow {
    batchId: string;
    pondCode: string;
    batchCode: string;
    cells: CalendarCell[];
    gaps: number;
    daysSinceMortality: number | null;
    daysSinceGrowthSampling: number | null;
    daysSinceChemical: number | null;
}

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header
            title="Daily log status"
            subtitle="Every open batch by day: AM feeding, PM feeding and water quality. A square counts how many of the three were entered.">
            @if (can('Farm.Logs.Create')) {
                <a routerLink="/app/logbook/feeding" class="btn btn-primary">Open the feeding sheet</a>
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <section class="card" aria-label="Daily log status">
                    <div class="card-body">
                        <div class="fpms-toolbar">
                            <label class="fpms-search">
                                <i class="fas fa-search" aria-hidden="true"></i>
                                <input
                                    type="text"
                                    aria-label="Search"
                                    placeholder="Search a pond or batch"
                                    [ngModel]="q()"
                                    (ngModelChange)="q.set($event)" />
                            </label>
                            <label class="fpms-field">
                                <span>Show the last</span>
                                <select
                                    class="form-control form-control-sm"
                                    [ngModel]="days()"
                                    (ngModelChange)="setDays($event)">
                                    @for (n of [7, 14, 31]; track n) {
                                        <option [ngValue]="n">{{ n }} days</option>
                                    }
                                </select>
                            </label>
                            <label class="fpms-field">
                                <span>Up to</span>
                                <input
                                    type="date"
                                    class="form-control form-control-sm"
                                    [ngModel]="endDate()"
                                    [max]="today"
                                    (ngModelChange)="setEnd($event)" />
                            </label>
                            <span class="small text-muted ml-auto">{{ resultText() }}</span>
                        </div>
                        <div class="mb-3">
                            <button
                                type="button"
                                class="fpms-chip"
                                [attr.aria-pressed]="filter() === 'all'"
                                (click)="filter.set('all')">
                                All batches · {{ base().length }}
                            </button>
                            <button
                                type="button"
                                class="fpms-chip"
                                [attr.aria-pressed]="filter() === 'gaps'"
                                (click)="filter.set('gaps')">
                                With gaps · {{ withGaps() }}
                            </button>
                        </div>

                        @if (!loaded()) {
                            <fpms-loading></fpms-loading>
                        } @else if (!rows().length) {
                            <fpms-empty
                                [text]="
                                    s().rows.length
                                        ? 'No batches match. Clear the search or the filter.'
                                        : 'No pond is stocked in this period.'
                                "
                                icon="fas fa-calendar-check"></fpms-empty>
                        } @else {
                            <div class="fpms-cal-scroll">
                                <div class="fpms-ds" [style.grid-template-columns]="gridCols()">
                                    <div class="h">Pond and batch</div>
                                    @for (dh of heads(); track dh.date) {
                                        <div class="h dh">
                                            <div class="n">{{ dh.n }}</div>
                                            <div>{{ dh.w }}</div>
                                        </div>
                                    }
                                    <div class="h">Last mortality</div>
                                    <div class="h">Last growth sample</div>
                                    <div class="h">Last chemical</div>

                                    @for (r of rows(); track r.batchId) {
                                        <div class="lead">
                                            <a [routerLink]="['/app/production/batches', r.batchId]">
                                                <strong>{{ r.pondCode }}</strong>
                                            </a>
                                            <div class="small text-muted">{{ r.batchCode }}</div>
                                        </div>
                                        @for (c of r.cells; track c.date) {
                                            <a
                                                class="sq"
                                                [ngClass]="c.state"
                                                [title]="c.title"
                                                [routerLink]="['/app/logbook/feeding']"
                                                [queryParams]="{ batchId: r.batchId, date: c.date }">
                                                {{ c.state === 'na' || c.state === 'today-empty' ? '' : c.count }}
                                                @for (dt of c.dots; track dt.kind) {
                                                    <span class="dot" [ngClass]="'dot-' + dt.kind"></span>
                                                }
                                            </a>
                                        }
                                        <div class="small">{{ since(r.daysSinceMortality) }}</div>
                                        <div
                                            class="small"
                                            [class.text-warning-dark]="
                                                r.daysSinceGrowthSampling === null || r.daysSinceGrowthSampling > 14
                                            ">
                                            {{ since(r.daysSinceGrowthSampling) }}
                                        </div>
                                        <div class="small">{{ since(r.daysSinceChemical) }}</div>
                                    }
                                </div>
                            </div>
                        }

                        <div class="fpms-legend">
                            <span>
                                <i class="sq gap"></i>
                                Gap
                            </span>
                            <span>
                                <i class="sq c1"></i>
                                1 of 3
                            </span>
                            <span>
                                <i class="sq c2"></i>
                                2 of 3
                            </span>
                            <span>
                                <i class="sq c3"></i>
                                3 of 3
                            </span>
                            <span>
                                <i class="sq today-empty"></i>
                                No entry (today)
                            </span>
                            <span>
                                <i class="dot-key dot-m"></i>
                                Mortality
                            </span>
                            <span>
                                <i class="dot-key dot-g"></i>
                                Growth sampling
                            </span>
                            <span>
                                <i class="dot-key dot-c"></i>
                                Chemical
                            </span>
                        </div>
                        <p class="small text-muted mt-3 mb-0">
                            Click a day to open that day's feeding sheet. A blank day is a gap to fill in, never
                            "overdue". Not fed counts as entered.
                        </p>
                    </div>
                </section>
            </div>
        </section>
    `,
    styles: [
        LOG_CALENDAR_STYLES,
        `
            .fpms-toolbar {
                display: flex;
                flex-wrap: wrap;
                align-items: center;
                gap: 0.6rem 1rem;
                margin-bottom: 0.9rem;
            }
            .fpms-search {
                display: flex;
                align-items: center;
                gap: 0.5rem;
                min-height: 38px;
                padding: 0 0.7rem;
                margin: 0;
                border: 1px solid var(--fpms-line-strong);
                border-radius: 8px;
                background: var(--fpms-surface);
                flex: 1 1 220px;
                max-width: 340px;
                color: var(--fpms-muted);
            }
            .fpms-search input {
                border: 0;
                outline: 0;
                flex: 1;
                min-width: 0;
                background: transparent;
                color: var(--fpms-text);
            }
            .fpms-field {
                display: flex;
                align-items: center;
                gap: 0.5rem;
                margin: 0;
                font-weight: 400;
                font-size: 0.9rem;
                color: var(--fpms-muted);
            }
            .fpms-field .form-control {
                height: 38px;
                width: auto;
            }
            .fpms-chip {
                min-height: 36px;
                padding: 0 0.9rem;
                margin-right: 0.4rem;
                border-radius: 18px;
                border: 1px solid var(--fpms-line-strong);
                background: var(--fpms-surface);
                font-size: 0.9rem;
                cursor: pointer;
            }
            .fpms-chip[aria-pressed='true'] {
                background: var(--fpms-primary);
                border-color: var(--fpms-primary);
                color: #fff;
            }
            .fpms-cal-scroll {
                overflow-x: auto;
            }
            .fpms-ds {
                display: grid;
                min-width: 1080px;
                gap: 6px 4px;
                align-items: center;
            }
            .fpms-ds .h {
                align-self: end;
                font-size: 0.78rem;
                color: var(--fpms-muted);
            }
            .fpms-ds .dh {
                text-align: center;
                line-height: 1.25;
            }
            .fpms-ds .dh .n {
                font-weight: 600;
                color: var(--fpms-text);
            }
            .fpms-legend {
                display: flex;
                flex-wrap: wrap;
                gap: 0.4rem 1.25rem;
                margin-top: 1rem;
                padding-top: 0.8rem;
                border-top: 1px solid var(--fpms-line);
                font-size: 0.8rem;
                color: var(--fpms-muted);
            }
        `,
    ],
})
export class DailyStatusComponent extends FpmsPage implements OnInit {
    s = signal<any>({ rows: [] });
    loaded = signal(false);
    q = signal('');
    filter = signal<'all' | 'gaps'>('all');
    days = signal(14);
    endDate = signal(FarmTime.today());

    private readonly weekday = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];

    ngOnInit(): void {
        this.load();
    }

    setDays(n: number): void {
        this.days.set(n);
        this.load();
    }

    setEnd(date: string): void {
        this.endDate.set(date || this.today);
        this.load();
    }

    async load(): Promise<void> {
        this.loaded.set(false);
        const data = await this.run(() =>
            this.api.get<any>('Feeding', 'GetDailyStatus', { endDate: this.endDate(), days: this.days() })
        );
        this.s.set(data ?? { rows: [] });
        this.loaded.set(true);
    }

    dates = computed<string[]>(() => {
        const st = this.s();
        if (!st.from || !st.to) {
            return [];
        }
        const out: string[] = [];
        for (
            let d = String(st.from).substring(0, 10);
            d <= String(st.to).substring(0, 10);
            d = FarmTime.addDays(d, 1)
        ) {
            out.push(d);
        }
        return out;
    });

    heads = computed(() =>
        this.dates().map((date) => {
            const [y, m, d] = date.split('-').map(Number);
            return {
                date,
                n: String(d),
                w: this.days() <= 14 ? this.weekday[new Date(Date.UTC(y, m - 1, d)).getUTCDay()] : '',
            };
        })
    );

    gridCols = computed(() => `150px repeat(${this.dates().length}, minmax(0, 1fr)) 120px 130px 120px`);

    base = computed<StatusRow[]>(() => {
        const q = this.q().trim().toLowerCase();
        const dates = this.dates();
        return ((this.s().rows as any[]) ?? [])
            .filter((r) => !q || `${r.pondCode} ${r.batchCode}`.toLowerCase().includes(q))
            .map((r) => {
                const { cells, gapDates } = buildCalendarCells(r, dates, this.today);
                return {
                    batchId: r.batchId,
                    pondCode: r.pondCode,
                    batchCode: r.batchCode,
                    cells,
                    gaps: gapDates.length,
                    daysSinceMortality: r.daysSinceMortality ?? null,
                    daysSinceGrowthSampling: r.daysSinceGrowthSampling ?? null,
                    daysSinceChemical: r.daysSinceChemical ?? null,
                };
            });
    });

    withGaps = computed(() => this.base().filter((r) => r.gaps > 0).length);

    rows = computed(() => this.base().filter((r) => this.filter() === 'all' || r.gaps > 0));

    resultText = computed(() => `${this.rows().length} of ${((this.s().rows as any[]) ?? []).length} batches`);

    since(n: number | null): string {
        return n === null ? 'None' : n === 0 ? 'Today' : n === 1 ? '1 day ago' : `${n} days ago`;
    }
}
