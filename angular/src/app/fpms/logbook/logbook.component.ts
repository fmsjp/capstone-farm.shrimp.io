import { Component, OnInit, computed, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime } from '@shared/fpms/farm-time';
import { PagerState } from '@shared/fpms/pager';
import { ToolbarChip } from '@shared/fpms/ui';
import { logSummary } from '@shared/fpms/log-summary';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header
            title="Logbooks"
            subtitle="Daily production records by pond. Feeding has its own page.">
            @if (can('Farm.Reports')) {
                <button
                    class="btn btn-outline-secondary mr-1"
                    (click)="exportCsv('ExportList', exportBody())"
                    [disabled]="busy()">
                    Export CSV
                </button>
            }
            @if (can('Farm.Logs.Create')) {
                <a routerLink="/app/logbook/new" class="btn btn-primary">Record operations</a>
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <section class="card" aria-label="Logbook">
                    <div class="card-header">
                        <fpms-list-toolbar
                            placeholder="Search details, pond or person"
                            [search]="f.search"
                            [resultText]="resultText()"
                            [chips]="chips()"
                            [chip]="f.logType"
                            (searchChange)="onSearch($event)"
                            (chipChange)="onType($event)">
                            <label class="lb-field">
                                <span>Pond</span>
                                <select
                                    class="form-control form-control-sm"
                                    [(ngModel)]="f.batchId"
                                    (ngModelChange)="apply()">
                                    <option [ngValue]="null">All ponds</option>
                                    @for (x of batches(); track x.id) {
                                        <option [ngValue]="x.id">{{ x.pondCode }} · {{ x.batchCode }}</option>
                                    }
                                </select>
                            </label>
                            <label class="lb-field">
                                <span>From</span>
                                <input
                                    type="date"
                                    class="form-control form-control-sm"
                                    [(ngModel)]="f.from"
                                    (ngModelChange)="apply()" />
                            </label>
                            <label class="lb-field">
                                <span>To</span>
                                <input
                                    type="date"
                                    class="form-control form-control-sm"
                                    [(ngModel)]="f.to"
                                    (ngModelChange)="apply()" />
                            </label>
                        </fpms-list-toolbar>
                        <div class="mt-1">
                            <div class="custom-control custom-checkbox custom-control-inline">
                                <input
                                    type="checkbox"
                                    class="custom-control-input"
                                    id="oor"
                                    [(ngModel)]="f.outOfRangeOnly"
                                    (ngModelChange)="apply()" />
                                <label class="custom-control-label small" for="oor">Out of range only</label>
                            </div>
                            <div class="custom-control custom-checkbox custom-control-inline">
                                <input
                                    type="checkbox"
                                    class="custom-control-input"
                                    id="sv"
                                    [(ngModel)]="f.includeVoided"
                                    (ngModelChange)="apply()" />
                                <label class="custom-control-label small" for="sv">Show voided</label>
                            </div>
                        </div>
                    </div>
                    <div class="card-body p-0 table-responsive">
                        @if (!logs()) {
                            <fpms-loading></fpms-loading>
                        } @else if (!logs().length) {
                            <fpms-empty
                                text="No log entries match. Clear the search or the filters."
                                icon="fas fa-book"></fpms-empty>
                        } @else {
                            <table class="table table-sm mb-0 lb-table">
                                <thead>
                                    <tr>
                                        <th fpmsSort="observedAt" [pager]="pager" (sorted)="load()">Observed</th>
                                        <th>Pond and batch</th>
                                        <th fpmsSort="logType" [pager]="pager" (sorted)="load()">Type</th>
                                        <th>Details</th>
                                        <th fpmsSort="recordedAt" [pager]="pager" (sorted)="load()">Recorded by</th>
                                        <th class="text-right">Actions</th>
                                    </tr>
                                </thead>
                                @for (l of logs(); track l.id) {
                                    <tbody [class.lb-voided]="l.isVoided">
                                        <tr [class.fpms-row-warn]="l.hasOutOfRange && !l.isVoided">
                                            <td class="text-nowrap">
                                                <a [routerLink]="['/app/logbook', l.id]">
                                                    {{ l.observedAtUtc | farmDate }}
                                                </a>
                                                <div class="small text-muted">{{ timeOf(l.observedAtUtc) }}</div>
                                            </td>
                                            <td class="text-nowrap">
                                                <a [routerLink]="['/app/production/batches', l.batchId]">
                                                    <strong>{{ l.pondCode }}</strong>
                                                </a>
                                                <div class="small text-muted">{{ l.batchCode }}</div>
                                            </td>
                                            <td>
                                                {{ l.logType | label: L.LogTypes }}
                                                @if (l.hasOutOfRange) {
                                                    <div><span class="badge badge-warning">Out of range</span></div>
                                                }
                                                @if (l.isHighMortality) {
                                                    <div><span class="badge badge-danger">High mortality</span></div>
                                                }
                                            </td>
                                            <td class="small">
                                                @if (l.measurements?.length) {
                                                    @for (m of l.measurements; track m.parameterCode) {
                                                        <span
                                                            class="mr-2"
                                                            [class.text-danger]="m.isOutOfRange"
                                                            [class.font-weight-bold]="m.isOutOfRange">
                                                            {{ m.parameterName }} {{ m.value }} {{ m.unitCode
                                                            }}{{
                                                                m.isOutOfRange
                                                                    ? m.side === 1
                                                                        ? ', low'
                                                                        : m.side === 2
                                                                          ? ', high'
                                                                          : ''
                                                                    : ''
                                                            }}
                                                        </span>
                                                    }
                                                } @else {
                                                    {{ summary(l) }}
                                                }
                                                @if (l.notes && l.logType !== 9) {
                                                    <div class="text-muted">{{ l.notes }}</div>
                                                }
                                            </td>
                                            <td class="small">
                                                {{ l.recordedBy }}
                                                <div class="text-muted">
                                                    recorded {{ l.creationTime | farmDateTime }}
                                                </div>
                                                @if (lateText(l); as late) {
                                                    <div class="text-warning-dark">{{ late }}</div>
                                                }
                                            </td>
                                            <td class="text-right">
                                                @if (l.isVoided) {
                                                    <span class="badge badge-secondary">Voided</span>
                                                } @else {
                                                    @if (l.canEdit) {
                                                        <a
                                                            class="btn btn-sm btn-outline-secondary mr-1"
                                                            [routerLink]="['/app/logbook', l.id]">
                                                            Correct
                                                        </a>
                                                    }
                                                    @if (l.canVoid) {
                                                        <button
                                                            type="button"
                                                            class="btn btn-sm btn-outline-secondary"
                                                            (click)="startVoid(l)">
                                                            Void
                                                        </button>
                                                    }
                                                    @if (!l.canEdit && !l.canVoid) {
                                                        @if (l.isBatchClosed) {
                                                            <span class="small text-muted">
                                                                Locked: batch is closed.
                                                                <br />
                                                                The Owner can reopen it.
                                                            </span>
                                                        } @else {
                                                            <a
                                                                class="btn btn-sm btn-outline-secondary"
                                                                [routerLink]="['/app/logbook', l.id]">
                                                                View
                                                            </a>
                                                            <div class="small text-muted">Recorded by someone else</div>
                                                        }
                                                    }
                                                }
                                            </td>
                                        </tr>
                                        @if (l.isVoided) {
                                            <tr class="lb-note">
                                                <td colspan="6" class="small text-muted">
                                                    Voided: {{ l.voidReason }}
                                                    @if (l.voidedBy) {
                                                        · by {{ l.voidedBy }}
                                                    }
                                                    @if (l.voidedAtUtc) {
                                                        · {{ l.voidedAtUtc | farmDateTime }}
                                                    }
                                                </td>
                                            </tr>
                                        }
                                        @if (voiding()?.id === l.id) {
                                            <tr class="lb-note">
                                                <td colspan="6">
                                                    <label [attr.for]="'vr-' + l.id" class="mb-1">
                                                        Reason for voiding
                                                        <span class="text-danger">*</span>
                                                    </label>
                                                    <input
                                                        [id]="'vr-' + l.id"
                                                        class="form-control form-control-sm mb-1"
                                                        maxlength="500"
                                                        placeholder="e.g. entered on the wrong pond"
                                                        [(ngModel)]="voidReason" />
                                                    @if (voidError()) {
                                                        <div class="small text-danger mb-1">Enter a reason first.</div>
                                                    }
                                                    <div class="small text-muted mb-2">
                                                        The record stays in the history and drops out of totals. A
                                                        voided water reading no longer counts as an alert.
                                                    </div>
                                                    <button
                                                        type="button"
                                                        class="btn btn-sm btn-danger mr-1"
                                                        (click)="confirmVoid(l)"
                                                        [disabled]="busy()">
                                                        Void log
                                                    </button>
                                                    <button
                                                        type="button"
                                                        class="btn btn-sm btn-outline-secondary"
                                                        (click)="cancelVoid()">
                                                        Cancel
                                                    </button>
                                                </td>
                                            </tr>
                                        }
                                    </tbody>
                                }
                            </table>
                        }
                    </div>
                    <fpms-pager [state]="pager" (changed)="load()"></fpms-pager>
                    <div class="px-3 py-2 small text-muted border-top">
                        @if (isOwner) {
                            As Owner you can correct and void any entry until its batch is closed.
                        } @else {
                            You can correct entries you recorded until their batch is closed. A Farm Owner can void
                            entries.
                        }
                    </div>
                </section>
            </div>
        </section>
    `,
    styles: [
        `
            .lb-field {
                display: flex;
                align-items: center;
                gap: 0.5rem;
                margin: 0;
                font-weight: 400;
                font-size: 0.9rem;
                color: var(--fpms-muted);
            }
            .lb-field .form-control {
                height: 38px;
                width: auto;
            }
            .lb-table tbody + tbody {
                border-top: 1px solid var(--fpms-line);
            }
            .lb-table td {
                vertical-align: top;
            }
            .lb-voided tr:first-child td {
                color: var(--fpms-muted);
                text-decoration: line-through;
                text-decoration-color: var(--fpms-line-strong);
            }
            .lb-voided tr:first-child td:last-child {
                text-decoration: none;
            }
            .lb-note td {
                background: var(--fpms-bg);
                border-top: 0;
            }
        `,
    ],
})
export class LogbookComponent extends FpmsPage implements OnInit {
    logs = signal<any[] | null>(null);
    batches = signal<any[]>([]);
    counts = signal<Record<number, number>>({});
    total = signal(0);
    voiding = signal<any | null>(null);
    voidError = signal(false);
    voidReason = '';
    pager = new PagerState('observedAt desc');
    f: any = {
        search: '',
        batchId: null,
        logType: null,
        from: FarmTime.addDays(FarmTime.today(), -14),
        to: FarmTime.today(),
        outOfRangeOnly: false,
        includeVoided: false,
    };

    chips = computed<ToolbarChip[]>(() => {
        const c = this.counts();
        const all = (Object.values(c) as number[]).reduce((n, v) => n + v, 0);
        return [
            { value: null, label: 'All types', count: all },
            ...this.Options.logType
                .filter((o: any) => (c[o.value] ?? 0) > 0)
                .map((o: any) => ({ value: o.value, label: o.label, count: c[o.value] })),
        ];
    });

    resultText = computed(() => (this.total() === 1 ? '1 entry' : `${this.total()} entries`));

    ngOnInit(): void {
        this.f.batchId = this.query('batchId') || null;
        this.restoreList(this.pager, this.f, {
            search: 'string',
            batchId: 'string',
            logType: 'number',
            from: 'string',
            to: 'string',
            outOfRangeOnly: 'boolean',
            includeVoided: 'boolean',
        });
        this.api
            .get<any[]>('Batch', 'GetAll', {})
            .then((b) =>
                this.batches.set(
                    (b ?? [])
                        .slice()
                        .sort(
                            (x, y) =>
                                String(x.pondCode).localeCompare(String(y.pondCode)) ||
                                String(x.batchCode).localeCompare(String(y.batchCode))
                        )
                )
            );
        this.load();
    }

    summary(l: any): string {
        return logSummary(l);
    }

    timeOf(utc: string): string {
        return FarmTime.time(utc);
    }

    lateText(l: any): string | null {
        const obs = FarmTime.isoDateOf(l.observedAtUtc);
        const rec = FarmTime.isoDateOf(l.creationTime);
        if (!obs || !rec) {
            return null;
        }
        const days = Math.round((Date.parse(rec) - Date.parse(obs)) / 86400000);
        return days >= 1 ? `entered ${days} ${days === 1 ? 'day' : 'days'} later` : null;
    }

    private params(): Record<string, unknown> {
        return { ...this.f, search: this.f.search || undefined, includeVoided: this.f.includeVoided || undefined };
    }

    async load(): Promise<void> {
        await this.run(async () => {
            const [r, counts] = await Promise.all([
                this.api.get<any>('Log', 'GetList', { ...this.params(), ...this.pager.params }),
                this.api.get<any[]>('Log', 'GetTypeCounts', this.params()),
            ]);
            this.logs.set(r.items);
            this.total.set(r.totalCount);
            this.counts.set(Object.fromEntries((counts ?? []).map((x) => [x.status, x.count])));
            const reload = this.pager.update(r.totalCount);
            this.syncPager(this.pager);
            if (reload) {
                await this.load();
            }
        });
    }

    apply(): void {
        this.pager.reset();
        this.cancelVoid();
        this.load();
    }

    onSearch(value: string): void {
        this.f.search = value;
        this.apply();
    }

    onType(value: unknown): void {
        this.f.logType = value as number | null;
        this.apply();
    }

    startVoid(l: any): void {
        this.voiding.set(l);
        this.voidReason = '';
        this.voidError.set(false);
    }

    cancelVoid(): void {
        this.voiding.set(null);
        this.voidReason = '';
        this.voidError.set(false);
    }

    async confirmVoid(l: any): Promise<void> {
        const reason = this.voidReason.trim();
        if (!reason) {
            this.voidError.set(true);
            return;
        }
        if (await this.act(() => this.api.post('Log', 'Void', { id: l.id, reason }))) {
            this.notifySaved('Entry voided.');
            this.cancelVoid();
            await this.load();
        }
    }

    exportBody(): Record<string, unknown> {
        return {
            list: 'Logs',
            batchId: this.f.batchId,
            logType: this.f.logType,
            from: this.f.from,
            to: this.f.to,
            outOfRangeOnly: this.f.outOfRangeOnly,
            search: this.f.search || null,
        };
    }
}
