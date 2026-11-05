import { Component, OnInit, computed, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';
import { CalendarCell, LOG_CALENDAR_STYLES, buildCalendarCells, shortDate } from '../log-calendar';

const CALENDAR_DAYS = 14;

interface CalendarRow {
    batchId: string;
    pondId: string;
    pondCode: string;
    batchCode: string;
    cells: CalendarCell[];
    gapDates: string[];
    doc: number | null;
    abwG: number | null;
    fcr: number | null;
    survival: number | null;
    sampleDays: number | null;
    hasAlert: boolean;
}

interface AttentionRow {
    title: string;
    detail?: string;
    ownerOnly?: boolean;
    where: string;
    whereSub?: string;
    whereLink?: any[];
    since: string;
    action: string;
    actionLink: any[];
    actionParams?: any;
}

interface AttentionGroup {
    key: string;
    title: string;
    note: string;
    rows: AttentionRow[];
}

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (!d()) {
                    <fpms-loading></fpms-loading>
                } @else {
                    @let x = d();

                    @if (x.isOwner && pendingSetup().length) {
                        <div class="card card-outline card-primary">
                            <div class="card-header">
                                <h3 class="card-title">
                                    <i class="fas fa-tasks" aria-hidden="true"></i>
                                    Set up your farm
                                </h3>
                            </div>
                            <div class="card-body py-2">
                                <ul class="list-unstyled mb-0 fpms-setup">
                                    @for (t of x.setupTasks; track t.key) {
                                        <li>
                                            <i
                                                class="far mr-2"
                                                [class.fa-check-circle]="t.done"
                                                [class.text-success]="t.done"
                                                [class.fa-circle]="!t.done"></i>
                                            @if (t.done) {
                                                <span class="text-muted">{{ t.label }}</span>
                                            } @else {
                                                <a [routerLink]="t.route">{{ t.label }}</a>
                                            }
                                        </li>
                                    }
                                </ul>
                            </div>
                        </div>
                    }

                    <section aria-label="Key figures" class="mb-3">
                        <div class="fpms-stats">
                            <div class="fpms-stat">
                                <div class="l">Active cycles</div>
                                <div class="v">{{ x.kpis.activeCycles }}</div>
                            </div>
                            <div class="fpms-stat">
                                <div class="l">Stocked batches</div>
                                <div class="v">{{ x.kpis.stockedBatches }}</div>
                            </div>
                            <div class="fpms-stat">
                                <div class="l">Avg. current ABW</div>
                                <div class="v">{{ x.kpis.averageAbwG | num: 1 }} g</div>
                            </div>
                            <div class="fpms-stat">
                                <div class="l">Avg. FCR to date</div>
                                <div class="v">{{ x.kpis.averageFcrToDate | num: 2 }}</div>
                            </div>
                            <div class="fpms-stat">
                                <div class="l">Est. survival</div>
                                <div class="v">{{ x.kpis.averageEstimatedSurvivalPct | num: 1 }}%</div>
                            </div>
                        </div>
                        <div class="fpms-kpi-links">
                            <a
                                class="fpms-kpi-link"
                                [class.warn]="alertPondCount() > 0"
                                href="#attention"
                                (click)="jump($event)">
                                <span class="v">{{ alertPondCount() }}</span>
                                <span>
                                    <span class="l">Ponds with water alerts</span>
                                </span>
                            </a>
                            <a
                                class="fpms-kpi-link"
                                [class.warn]="x.kpis.highMortalityDays7Days > 0"
                                href="#attention"
                                (click)="jump($event)">
                                <span class="v">{{ x.kpis.highMortalityDays7Days }}</span>
                                <span>
                                    <span class="l">High-mortality days</span>
                                </span>
                            </a>
                            <a
                                class="fpms-kpi-link"
                                [class.warn]="x.kpis.blankDailyRecordsYesterday > 0"
                                routerLink="/app/logbook/status">
                                <span class="v">{{ x.kpis.blankDailyRecordsYesterday }}</span>
                                <span>
                                    <span class="l">Blank daily records</span>
                                </span>
                            </a>
                        </div>
                    </section>

                    <section class="card" aria-labelledby="cal-h">
                        <div class="card-header d-flex flex-wrap align-items-center justify-content-between">
                            <div>
                                <h2 id="cal-h" class="card-title fpms-h2">Ponds and log calendar</h2>
                            </div>
                            <div class="d-flex flex-wrap align-items-center mt-2 mt-md-0 ml-auto">
                            <div class="btn-group-toggle" role="group" aria-label="Show">
                                <button
                                    type="button"
                                    class="fpms-chip"
                                    [attr.aria-pressed]="group() === 'all'"
                                    (click)="group.set('all')">
                                    All batches · {{ rows().length }}
                                </button>
                                <button
                                    type="button"
                                    class="fpms-chip"
                                    [attr.aria-pressed]="group() === 'alert'"
                                    (click)="group.set('alert')">
                                    Alerts · {{ alertRows() }}
                                </button>
                            </div>
                            @if (can('Farm.Logs')) {
                                <a routerLink="/app/logbook/status" class="btn btn-outline-primary btn-sm ml-3">
                                    Proceed to Operations
                                    <i class="fas fa-arrow-right ml-1" aria-hidden="true"></i>
                                </a>
                            }
                            </div>
                        </div>
                        <div class="card-body">
                            <div class="fpms-toolbar">
                                <label class="fpms-search">
                                    <i class="fas fa-search" aria-hidden="true"></i>
                                    <input
                                        type="text"
                                        aria-label="Search ponds or batches"
                                        placeholder="Search a pond or batch"
                                        [ngModel]="q()"
                                        (ngModelChange)="q.set($event)" />
                                </label>
                                <label class="fpms-sort">
                                    <span>Sort by</span>
                                    <select
                                        class="form-control form-control-sm"
                                        [ngModel]="sort()"
                                        (ngModelChange)="sort.set($event)">
                                        <option value="pond">Pond code</option>
                                        <option value="attention">Needs attention first</option>
                                        <option value="doc">DOC, highest first</option>
                                        <option value="fcr">FCR, highest first</option>
                                    </select>
                                </label>
                            </div>

                            @if (!s()) {
                                <fpms-loading></fpms-loading>
                            } @else if (!rows().length) {
                                <fpms-empty text="No batch is stocked yet." icon="fas fa-water">
                                    @if (can('Farm.Production.Stock')) {
                                        <a routerLink="/app/production/create-batch" class="btn btn-sm btn-primary">
                                            Create batch
                                        </a>
                                    }
                                </fpms-empty>
                            } @else {
                                <div class="fpms-cal-scroll">
                                    <div class="fpms-cal">
                                        <div class="fpms-cal-row head">
                                            <div>Pond and batch</div>
                                            @for (dd of dayHeads(); track dd.date) {
                                                <div class="dh">
                                                    <div class="n">{{ dd.n }}</div>
                                                    <div>{{ dd.w }}</div>
                                                </div>
                                            }
                                            <div class="r">DOC</div>
                                            <div class="r">ABW (g)</div>
                                            <div class="r">FCR</div>
                                            <div class="r">Surv.</div>
                                            <div>Last sample</div>
                                            <div>Water</div>
                                        </div>
                                        @for (r of visible(); track r.batchId) {
                                            <div class="fpms-cal-row">
                                                <div class="lead">
                                                    <a [routerLink]="['/app/production/batches', r.batchId]">
                                                        <strong>{{ r.pondCode }}</strong>
                                                    </a>
                                                    <div class="small text-muted">{{ r.batchCode }}</div>
                                                </div>
                                                @for (c of r.cells; track c.date) {
                                                    <span class="sq" [ngClass]="c.state" [title]="c.title">
                                                        {{
                                                            c.state === 'na' || c.state === 'today-empty' ? '' : c.count
                                                        }}
                                                        @for (dt of c.dots; track dt.kind) {
                                                            <span class="dot" [ngClass]="'dot-' + dt.kind"></span>
                                                        }
                                                    </span>
                                                }
                                                <div class="r">{{ r.doc ?? '–' }}</div>
                                                <div class="r">{{ r.abwG === null ? '–' : (r.abwG | num: 1) }}</div>
                                                <div class="r">{{ r.fcr === null ? '–' : (r.fcr | num: 2) }}</div>
                                                <div class="r">
                                                    {{ r.survival === null ? '–' : (r.survival | num: 0) + '%' }}
                                                </div>
                                                <div
                                                    [class.text-warning-dark]="
                                                        r.sampleDays === null || r.sampleDays > 14
                                                    ">
                                                    {{ sampleText(r.sampleDays) }}
                                                </div>
                                                <div>
                                                    @if (r.hasAlert) {
                                                        <span class="badge badge-warning">Alert</span>
                                                    } @else {
                                                        <span class="text-muted small">In range</span>
                                                    }
                                                </div>
                                            </div>
                                        }
                                    </div>
                                </div>
                                <div class="mt-3 small">
                                    <a routerLink="/app/logbook/status">See the full status, up to 31 days</a>
                                </div>
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
                            }
                        </div>
                    </section>

                    <section class="card" aria-labelledby="att-h" id="attention">
                        <div class="card-header d-flex flex-wrap align-items-baseline">
                            <h2 id="att-h" class="card-title fpms-h2 mr-2">Needs attention</h2>
                            <span class="badge badge-light mr-2">
                                {{ attentionTotal() }} {{ attentionTotal() === 1 ? 'item' : 'items' }}
                            </span>
                        </div>
                        <div class="card-body p-0">
                            @if (!groups().length) {
                                <fpms-empty
                                    text="Nothing needs attention right now."
                                    icon="far fa-check-circle"></fpms-empty>
                            } @else {
                                <ul class="list-unstyled mb-0 fpms-att">
                                    @for (g of groups(); track g.key) {
                                        @for (r of g.rows; track $index) {
                                            <li [ngClass]="'grp-' + g.key">
                                                <div class="what">
                                                    <span class="font-weight-bold">{{ r.title }}</span>
                                                    @if (r.ownerOnly) {
                                                        <span class="badge badge-secondary ml-1">Owner</span>
                                                    }
                                                    <span class="text-muted">
                                                        ·
                                                        @if (r.whereLink) {
                                                            <a [routerLink]="r.whereLink">{{ r.where }}</a>
                                                        } @else {
                                                            {{ r.where }}
                                                        }
                                                    </span>
                                                </div>
                                                <a
                                                    class="btn btn-sm btn-outline-secondary"
                                                    [routerLink]="r.actionLink"
                                                    [queryParams]="r.actionParams">
                                                    {{ r.action }}
                                                </a>
                                            </li>
                                        }
                                    }
                                </ul>
                            }
                        </div>
                    </section>
                }
            </div>
        </section>
    `,
    styles: [
        LOG_CALENDAR_STYLES,
        `
            .fpms-stats {
                grid-template-columns: repeat(2, 1fr);
            }
            @media (min-width: 768px) {
                .fpms-stats {
                    grid-template-columns: repeat(5, 1fr);
                }
            }
            .fpms-h2 {
                float: none;
                margin: 0;
                font-size: 1.15rem;
                font-weight: 600;
                color: var(--fpms-primary);
            }
            .fpms-stat .s {
                font-size: 0.78rem;
                color: var(--fpms-muted);
            }
            .fpms-kpi-links {
                display: grid;
                grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
                gap: 0.75rem;
                margin-top: 0.75rem;
            }
            .fpms-kpi-link {
                display: flex;
                align-items: center;
                gap: 0.75rem;
                padding: 0.65rem 0.9rem;
                background: var(--fpms-surface);
                border: 1px solid var(--fpms-line);
                border-radius: 10px;
                color: var(--fpms-text);
            }
            .fpms-kpi-link:hover {
                text-decoration: none;
                border-color: var(--fpms-primary);
            }
            .fpms-kpi-link.warn {
                background: var(--fpms-warn-bg);
                border-color: var(--fpms-warn-line);
            }
            .fpms-kpi-link .v {
                font-size: 1.4rem;
                font-weight: 700;
            }
            .fpms-kpi-link .l,
            .fpms-kpi-link .s {
                display: block;
                line-height: 1.25;
            }
            .fpms-kpi-link .l {
                font-size: 0.88rem;
                font-weight: 600;
            }
            .fpms-kpi-link .s {
                font-size: 0.78rem;
                color: var(--fpms-muted);
            }
            .fpms-chip {
                min-height: 36px;
                padding: 0 0.9rem;
                margin-left: 0.4rem;
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
                max-width: 320px;
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
            .fpms-sort {
                display: flex;
                align-items: center;
                gap: 0.5rem;
                margin: 0;
                font-weight: 400;
                font-size: 0.9rem;
            }
            .fpms-sort select {
                height: 38px;
                width: auto;
            }
            .fpms-cal-scroll {
                overflow-x: auto;
            }
            .fpms-cal {
                min-width: 1060px;
                display: flex;
                flex-direction: column;
                gap: 6px;
            }
            .fpms-cal-row {
                display: grid;
                grid-template-columns: 150px repeat(14, minmax(0, 1fr)) 48px 62px 48px 52px 96px 84px;
                gap: 4px;
                align-items: center;
                font-size: 0.9rem;
            }
            .fpms-cal-row.head {
                align-items: end;
                font-size: 0.78rem;
                color: var(--fpms-muted);
            }
            .fpms-cal-row .r {
                text-align: right;
            }
            .fpms-cal-row .dh {
                text-align: center;
                line-height: 1.25;
            }
            .fpms-cal-row .dh .n {
                font-weight: 600;
                color: var(--fpms-text);
            }
            .fpms-legend {
                display: flex;
                flex-wrap: wrap;
                gap: 0.4rem 1.25rem;
                margin-top: 0.9rem;
                padding-top: 0.8rem;
                border-top: 1px solid var(--fpms-line);
                font-size: 0.8rem;
                color: var(--fpms-muted);
            }
            .fpms-att li {
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 0.75rem;
                padding: 0.5rem 1rem;
                border-bottom: 1px solid var(--fpms-line);
                border-left: 4px solid transparent;
            }
            .fpms-att li:last-child {
                border-bottom: 0;
            }
            .fpms-att li.grp-act {
                border-left-color: #e0a030;
            }
            .fpms-att li.grp-catchup {
                border-left-color: #b8ad80;
            }
            .fpms-att li.grp-admin {
                border-left-color: #7a9bb0;
            }
        `,
    ],
})
export class DashboardComponent extends FpmsPage implements OnInit {
    d = signal<any>(null);
    s = signal<any>(null);
    pendingSetup = signal<any[]>([]);

    q = signal('');
    sort = signal<'pond' | 'attention' | 'doc' | 'fcr'>('pond');
    group = signal<'all' | 'alert'>('all');

    private readonly weekday = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];

    async ngOnInit(): Promise<void> {
        const [data, status] = await Promise.all([
            this.api.get<any>('Dashboard', 'Get'),
            this.can('Farm.Logs')
                ? this.run(() =>
                      this.api.get<any>('Feeding', 'GetDailyStatus', { endDate: this.today, days: CALENDAR_DAYS })
                  )
                : Promise.resolve(null),
        ]);
        this.pendingSetup.set((data?.setupTasks ?? []).filter((t: any) => !t.done));
        this.d.set(data);
        this.s.set(status ?? { rows: [] });
    }

    dates = computed<string[]>(() => {
        const out: string[] = [];
        for (let i = CALENDAR_DAYS - 1; i >= 0; i--) {
            out.push(FarmTime.addDays(this.today, -i));
        }
        return out;
    });

    dayHeads = computed(() =>
        this.dates().map((date) => {
            const [y, m, dd] = date.split('-').map(Number);
            return { date, n: String(dd), w: this.weekday[new Date(Date.UTC(y, m - 1, dd)).getUTCDay()] };
        })
    );

    rangeText = computed(() => {
        const ds = this.dates();
        return `${this.shortDate(ds[0])} to ${this.shortDate(ds[ds.length - 1])}`;
    });

    rows = computed<CalendarRow[]>(() => {
        const status = this.s();
        const dash = this.d();
        if (!status?.rows || !dash) {
            return [];
        }
        const tiles = new Map<string, any>((dash.ponds as any[]).filter((p) => p.batchId).map((p) => [p.batchId, p]));
        return (status.rows as any[]).map((r) => {
            const t = tiles.get(r.batchId);
            const { cells, gapDates } = buildCalendarCells(r, this.dates(), this.today);
            return {
                batchId: r.batchId,
                pondId: r.pondId,
                pondCode: r.pondCode,
                batchCode: r.batchCode,
                cells,
                gapDates,
                doc: t?.doc ?? null,
                abwG: t?.abwG ?? null,
                fcr: t?.fcrToDate ?? null,
                survival: t?.estimatedSurvivalPct ?? null,
                sampleDays: r.daysSinceGrowthSampling ?? null,
                hasAlert: !!t?.hasWaterQualityAlert,
            };
        });
    });

    alertRows = computed(() => this.rows().filter((r) => r.hasAlert).length);

    visible = computed<CalendarRow[]>(() => {
        const q = this.q().trim().toLowerCase();
        const num = (v: number | null) => (v === null ? -1 : v);
        return this.rows()
            .filter(
                (r) =>
                    (this.group() === 'all' || r.hasAlert) &&
                    (!q || `${r.pondCode} ${r.batchCode}`.toLowerCase().includes(q))
            )
            .sort((a, b) => {
                switch (this.sort()) {
                    case 'attention':
                        return Number(b.hasAlert) - Number(a.hasAlert) || a.pondCode.localeCompare(b.pondCode);
                    case 'doc':
                        return num(b.doc) - num(a.doc);
                    case 'fcr':
                        return num(b.fcr) - num(a.fcr);
                    default:
                        return a.pondCode.localeCompare(b.pondCode);
                }
            });
    });

    resultText = computed(() =>
        this.visible().length
            ? `${this.visible().length} of ${this.rows().length} batches`
            : 'No batch matches. Clear the search or the filter.'
    );

    alertPondCount = computed(
        () => new Set(((this.d()?.waterQualityAlerts as any[]) ?? []).map((a) => a.batchId)).size
    );

    enteredThrough = computed(() => {
        const logs = (this.d()?.recentLogs as any[]) ?? [];
        if (!logs.length) {
            return 'No entries yet.';
        }
        const latest = logs
            .map((l) => FarmTime.isoDateOf(l.observedAtUtc))
            .sort()
            .pop()!;
        return `Data entered through ${this.shortDate(latest)}`;
    });

    groups = computed<AttentionGroup[]>(() => {
        const dash = this.d();
        if (!dash) {
            return [];
        }
        const act: AttentionRow[] = [];
        for (const a of dash.waterQualityAlerts as any[]) {
            act.push({
                title: (a.readings as string[]).join('; '),
                detail: 'Water-quality reading outside the optimal range. See the recommendation in Farm Standards.',
                where: a.pondCode,
                whereSub: a.batchCode,
                whereLink: ['/app/production/batches', a.batchId],
                since: `Observed ${this.shortDate(String(a.farmDate).substring(0, 10))}`,
                action: 'Open log',
                actionLink: ['/app/logbook', a.logId],
            });
        }
        for (const h of dash.highMortality as any[]) {
            act.push({
                title: `High mortality: ${h.count} dead`,
                detail: 'More than 1% of the estimated population in one entry.',
                where: h.pondCode,
                whereSub: h.batchCode,
                whereLink: ['/app/production/batches', h.batchId],
                since: `Observed ${this.shortDate(String(h.farmDate).substring(0, 10))}`,
                action: 'Open log',
                actionLink: ['/app/logbook', h.logId],
            });
        }

        const catchUp: AttentionRow[] = [];
        for (const r of this.rows()) {
            if (!r.gapDates.length) {
                continue;
            }
            const first = r.gapDates[0];
            const listed = r.gapDates
                .slice(0, 3)
                .map((d) => this.shortDate(d))
                .join(', ');
            catchUp.push({
                title:
                    r.gapDates.length === 1
                        ? `No complete entry for ${listed}`
                        : `Missing entries on ${r.gapDates.length} days`,
                detail:
                    r.gapDates.length > 3
                        ? `${listed} and ${r.gapDates.length - 3} more.`
                        : r.gapDates.length > 1
                          ? `${listed}.`
                          : undefined,
                where: r.pondCode,
                whereSub: r.batchCode,
                whereLink: ['/app/production/batches', r.batchId],
                since: `Since ${this.shortDate(first)}`,
                action: this.can('Farm.Logs.Create') ? 'Open feeding sheet' : 'See daily status',
                actionLink: this.can('Farm.Logs.Create') ? ['/app/logbook/feeding'] : ['/app/logbook/status'],
                actionParams: this.can('Farm.Logs.Create') ? { batchId: r.batchId, date: first } : undefined,
            });
        }

        const admin: AttentionRow[] = [];
        for (const st of dash.stockAlerts as any[]) {
            admin.push({
                title: `${st.kind === 'Negative' ? 'Negative stock' : st.kind === 'Low' ? 'Low stock' : 'Stock to reconcile'}: ${st.itemName}${st.lotNumber ? ' lot ' + st.lotNumber : ''}`,
                detail: `${st.quantity} ${st.unit} on record. Check stock before the next feeding day.`,
                where: st.itemName,
                whereSub: 'Inventory',
                whereLink: ['/app/inventory'],
                since: '',
                action: 'Open Inventory',
                actionLink: ['/app/inventory'],
            });
        }
        for (const f of (dash.finishStocking as any[]) ?? []) {
            admin.push({
                title: 'Fry delivery with PL not stocked yet',
                detail: 'Record any PL not stocked, then finish stocking.',
                ownerOnly: true,
                where: f.hatcheryBatchNumber,
                whereSub: f.hatcheryName,
                whereLink: ['/app/resources/fry-deliveries', f.id],
                since: `Received ${this.shortDate(String(f.deliveryDate).substring(0, 10))}`,
                action: 'Finish stocking',
                actionLink: ['/app/resources/fry-deliveries', f.id],
            });
        }
        if (dash.isOwner) {
            for (const name of dash.missingRequiredDocuments as string[]) {
                admin.push({
                    title: `No active record for ${name}`,
                    detail: 'Required permit. Record it so compliance checks can pass.',
                    ownerOnly: true,
                    where: 'Permits and licenses',
                    whereSub: 'Farm',
                    whereLink: ['/app/farm/documents'],
                    since: 'Not recorded',
                    action: 'Record permit',
                    actionLink: ['/app/farm/documents'],
                });
            }
            for (const doc of dash.documentAlerts as any[]) {
                admin.push({
                    title: `${doc.categoryName}: ${doc.title}`,
                    detail: 'Permit is expiring or expired. Renewing archives the old record.',
                    ownerOnly: true,
                    where: 'Permits and licenses',
                    whereSub: 'Farm',
                    whereLink: ['/app/farm/documents'],
                    since: doc.expiryDate ? `Expires ${this.shortDate(String(doc.expiryDate).substring(0, 10))}` : '',
                    action: 'Renew permit',
                    actionLink: ['/app/farm/documents'],
                });
            }
        }

        return [
            { key: 'act', title: 'Act today', note: 'needs a response at the pond', rows: act },
            { key: 'catchup', title: 'Catch up on entry', note: 'sheets not yet entered', rows: catchUp },
            { key: 'admin', title: 'Admin and supply', note: 'can wait until this week', rows: admin },
        ].filter((g) => g.rows.length);
    });

    attentionTotal = computed(() => this.groups().reduce((n, g) => n + g.rows.length, 0));

    shortDate = shortDate;

    sampleText(days: number | null): string {
        return days === null ? 'None yet' : days === 0 ? 'Today' : days === 1 ? '1 day ago' : `${days} days ago`;
    }

    jump(ev: Event): void {
        ev.preventDefault();
        document.getElementById('attention')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
}
