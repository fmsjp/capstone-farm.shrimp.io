import { Component, ChangeDetectionStrategy, Injector, Type, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgComponentOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs/operators';
import { FpmsApiService } from '@shared/fpms/api.service';
import { FarmTime } from '@shared/fpms/farm-time';
import { FeedingSessions, LogTypes, NewLogTypes } from '@shared/fpms/labels';
import { logSummary } from '@shared/fpms/log-summary';
import { EMBEDDED_QUERY, LogDrawerRequest, LogDrawerService } from '@shared/fpms/log-drawer.service';

type Kind = 'feeding' | 'log';

const NEW_LOG_TYPES = NewLogTypes.map((value) => ({ value, label: (LogTypes as Record<number, string>)[value] }));

interface DayView {
    feeding: { session: string; text: string }[];
    logs: { id: string; type: string; time: string; summary: string }[];
}

@Component({
    selector: 'fpms-log-drawer',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgComponentOutlet, FormsModule, RouterLink],
    template: `
        @if (request(); as r) {
            <div class="fpms-dialog-backdrop no-print">
                <div class="fpms-dialog" role="dialog" aria-modal="true" aria-labelledby="lw-h">
                    <div class="fpms-dialog-head">
                        <div class="flex-fill">
                            <h2 id="lw-h">Record operations</h2>
                            <p class="text-muted mb-0">
                                {{ stage() === 'pond' ? 'Logging is recorded for one pond at a time.' : 'Pick what to record; the form opens below.' }}
                            </p>
                        </div>
                        <button type="button" class="fpms-dialog-close" (click)="close()" aria-label="Close" title="Close">
                            &times;
                        </button>
                    </div>
                    <div class="fpms-dialog-body">
                        @if (stage() === 'pond') {
                            <div class="form-group mb-0">
                                <label for="lw-pond">Which pond? <span class="text-danger">*</span></label>
                                <select id="lw-pond" class="form-control" [ngModel]="batchId()" (ngModelChange)="batchId.set($event)">
                                    @for (b of batches(); track b.id) {
                                        <option [ngValue]="b.id">{{ b.pondCode }} · {{ b.batchCode }}</option>
                                    }
                                </select>
                                @if (!batches().length) {
                                    <small class="form-text text-muted">No pond has an open batch yet.</small>
                                }
                            </div>
                        } @else {
                            <div class="form-row align-items-end mb-2">
                                <div class="col">
                                    <strong>Pond:</strong>
                                    {{ pondLabel() }}
                                </div>
                                <div class="col-auto">
                                    <label for="lw-date" class="small mb-0 mr-1">Day</label>
                                    <input
                                        id="lw-date"
                                        type="date"
                                        class="form-control form-control-sm d-inline-block w-auto"
                                        [max]="todayValue"
                                        [ngModel]="dateValue()"
                                        (ngModelChange)="changeDate($event)" />
                                </div>
                            </div>
                            <div class="border rounded p-2 mb-3">
                                <div class="small text-muted mb-1">Already logged {{ isToday() ? 'today' : 'this day' }}</div>
                                @if (day() === null) {
                                    <span class="small text-muted">Loading…</span>
                                } @else {
                                    @for (f of day()!.feeding; track f.session) {
                                        <div class="small">
                                            <strong>{{ f.session }} feeding:</strong>
                                            {{ f.text }}
                                        </div>
                                    }
                                    @for (l of day()!.logs; track l.id) {
                                        <div class="small">
                                            <strong>{{ l.type }}</strong>
                                            <span class="text-muted">{{ l.time }}</span>
                                            : {{ l.summary }}
                                            <a [routerLink]="['/app/logbook', l.id]">Open</a>
                                        </div>
                                    } @empty {
                                        <div class="small text-muted">No other entries.</div>
                                    }
                                }
                            </div>
                            <label class="d-block">Add {{ isToday() ? 'today' : 'to this day' }}</label>
                            <div class="fpms-choice mb-3" role="radiogroup" aria-label="Record type">
                                <button
                                    type="button"
                                    class="btn mr-1 mb-1"
                                    role="radio"
                                    [attr.aria-checked]="selected() === 'feeding'"
                                    [class.btn-primary]="selected() === 'feeding'"
                                    [class.btn-outline-primary]="selected() !== 'feeding'"
                                    (click)="pick('feeding', null)">
                                    <i class="fas fa-utensils mr-1" aria-hidden="true"></i>
                                    Feeding (AM/PM)
                                </button>
                                @for (o of logTypes; track o.value) {
                                    <button
                                        type="button"
                                        class="btn mr-1 mb-1"
                                        role="radio"
                                        [attr.aria-checked]="selected() === o.value"
                                        [class.btn-primary]="selected() === o.value"
                                        [class.btn-outline-primary]="selected() !== o.value"
                                        (click)="pick('log', o.value)">
                                        {{ o.label }}
                                    </button>
                                }
                            </div>
                            @if (view(); as v) {
                                <div id="lw-form" class="border-top pt-3">
                                    <ng-container *ngComponentOutlet="v.type; injector: v.injector"></ng-container>
                                </div>
                            }
                        }
                    </div>
                    @if (stage() === 'pond' || !startedWithPond()) {
                        <div class="fpms-dialog-foot">
                            @if (stage() === 'work') {
                                <button type="button" class="btn btn-default mr-auto" (click)="changePond()">Change pond</button>
                            } @else {
                                <button type="button" class="btn btn-default" (click)="close()">Cancel</button>
                                <button type="button" class="btn btn-primary" [disabled]="!batchId()" (click)="start()">Next</button>
                            }
                        </div>
                    }
                </div>
            </div>
        }
    `,
})
export class LogDrawerComponent {
    private router = inject(Router);
    private injector = inject(Injector);
    private api = inject(FpmsApiService);
    private drawer = inject(LogDrawerService);

    readonly request = this.drawer.request;
    readonly stage = signal<'pond' | 'work'>('pond');
    readonly startedWithPond = signal(false);
    readonly batchId = signal<string | null>(null);
    readonly batches = signal<any[]>([]);
    readonly logTypes = NEW_LOG_TYPES;

    readonly selected = signal<'feeding' | number | null>(null);
    readonly view = signal<{ type: Type<unknown>; injector: Injector } | null>(null);

    readonly day = signal<DayView | null>(null);

    constructor() {
        this.router.events
            .pipe(
                filter((e): e is NavigationEnd => e instanceof NavigationEnd),
                takeUntilDestroyed()
            )
            .subscribe(() => this.drawer.close());
        effect(() => {
            const request = this.drawer.request();
            untracked(() => void this.begin(request));
        });
        let first = true;
        effect(() => {
            this.drawer.saved();
            if (first) {
                first = false;
                return;
            }
            untracked(() => void this.loadDay());
        });
    }

    readonly todayValue = FarmTime.today();

    readonly dateValue = signal<string>(FarmTime.today());

    date(): string {
        return this.dateValue();
    }

    async changeDate(value: string): Promise<void> {
        const previous = this.dateValue();
        if (!value || value > this.todayValue || value === previous) {
            return;
        }
        if (!(await this.confirmDiscard())) {
            this.dateValue.set('');
            await Promise.resolve();
            this.dateValue.set(previous);
            return;
        }
        this.dateValue.set(value);
        this.day.set(null);
        void this.loadDay();
        const sel = this.selected();
        if (sel !== null && this.view()) {
            await this.show({ kind: sel === 'feeding' ? 'feeding' : 'log', batchId: this.batchId(), logType: sel === 'feeding' ? null : sel, date: value });
        }
    }

    isToday(): boolean {
        return this.date() === FarmTime.today();
    }

    pondLabel(): string {
        const b = this.batches().find((x) => x.id === this.batchId());
        return b ? `${b.pondCode} · ${b.batchCode}` : '';
    }

    close(): void {
        void this.confirmDiscard().then((ok) => ok && this.drawer.close());
    }

    private confirmDiscard(): Promise<boolean> {
        if (!this.drawer.embeddedPage?.hasUnsavedInput()) {
            return Promise.resolve(true);
        }
        return new Promise<boolean>((resolve) =>
            abp.message.confirm('The entry you started is not saved and will be discarded.', 'Discard this entry?', (ok: boolean) => resolve(!!ok))
        );
    }

    start(): void {
        this.stage.set('work');
        void this.loadDay();
    }

    async changePond(): Promise<void> {
        if (!(await this.confirmDiscard())) {
            return;
        }
        this.view.set(null);
        this.selected.set(null);
        this.day.set(null);
        this.stage.set('pond');
    }

    async pick(kind: Kind, logType: number | null): Promise<void> {
        const selected = kind === 'feeding' ? 'feeding' : logType;
        if (this.selected() === selected && this.view()) {
            return;
        }
        if (!(await this.confirmDiscard())) {
            return;
        }
        this.selected.set(selected);
        await this.show({ kind, batchId: this.batchId(), logType, date: this.dateValue() });
        setTimeout(() => document.getElementById('lw-form')?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
    }

    private async begin(request: LogDrawerRequest | null): Promise<void> {
        this.view.set(null);
        this.selected.set(null);
        this.day.set(null);
        if (!request) {
            return;
        }
        this.batchId.set(request.batchId ?? null);
        this.dateValue.set(request.date && request.date <= this.todayValue ? request.date : this.todayValue);
        this.startedWithPond.set(!!request.batchId);
        if (request.batchId) {
            this.stage.set('work');
            void this.loadDay();
            if (request.kind) {
                await this.pick(request.kind, request.logType ?? null);
            }
            return;
        }
        this.stage.set('pond');
        const batches = await this.api.get<any[]>('Batch', 'GetAll', { openOnly: true });
        this.batches.set(batches.sort((x, y) => String(x.pondCode).localeCompare(String(y.pondCode), undefined, { numeric: true })));
        this.batchId.set(this.batches()[0]?.id ?? null);
    }

    private async loadDay(): Promise<void> {
        const batchId = this.batchId();
        const date = this.date();
        if (!batchId) {
            return;
        }
        const [sessions, logs] = await Promise.all([
            this.api.get<any>('Feeding', 'GetList', { batchId, from: date, to: date, maxResultCount: 10 }),
            this.api.get<any>('Log', 'GetList', { batchId, from: date, to: date, maxResultCount: 50 }),
        ]);
        if (this.batchId() !== batchId || this.date() !== date) {
            return;
        }
        const feeding = [1, 2].map((n) => {
            const f = (sessions.items as any[]).find((x) => x.session === n);
            const text = !f ? 'not recorded' : f.status === 2 ? 'not fed' : `${f.kg} kg${f.fedAtTime ? ' at ' + f.fedAtTime : ''}`;
            return { session: (FeedingSessions as Record<number, string>)[n], text };
        });
        this.day.set({
            feeding,
            logs: (logs.items as any[])
                .filter((l) => !l.isVoided)
                .map((l) => ({
                    id: l.id,
                    type: (LogTypes as Record<number, string>)[l.logType] ?? '',
                    time: FarmTime.time(l.observedAtUtc),
                    summary: logSummary(l) || (l.notes ?? ''),
                })),
        });
    }

    private async show(request: LogDrawerRequest): Promise<void> {
        const query: Record<string, string | null> = {
            batchId: request.batchId ?? null,
            date: request.date ?? null,
            logType: request.logType != null ? String(request.logType) : null,
        };
        const injector = Injector.create({ providers: [{ provide: EMBEDDED_QUERY, useValue: query }], parent: this.injector });
        const type =
            request.kind === 'feeding'
                ? (await import('../fpms/logbook/feeding-sheet.component')).FeedingSheetComponent
                : (await import('../fpms/logbook/log-entry.component')).LogEntryComponent;
        this.view.set({ type, injector });
    }
}
