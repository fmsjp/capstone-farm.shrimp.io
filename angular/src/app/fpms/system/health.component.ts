import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { PagerState } from '@shared/fpms/pager';
import { FPMS_IMPORTS } from '../shared-imports';

const KINDS = [
    { value: 1, label: 'Full backup' },
    { value: 2, label: 'Log backup' },
    { value: 3, label: 'Integrity check' },
    { value: 4, label: 'Restore test' },
    { value: 5, label: 'Off-site copy' },
];

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="System health" subtitle="Backups, storage, certificate and server errors. System Administration.">
            <button class="btn btn-default" (click)="reload()" [disabled]="busy()"><i class="fas fa-sync-alt mr-1"></i>Refresh</button>
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (!health()) {
                    <fpms-loading></fpms-loading>
                } @else {
                    @let h = health();
                    @if (h.alerts.length) {
                        <div class="card card-outline" [class.card-danger]="hasCritical()" [class.card-warning]="!hasCritical()">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-exclamation-triangle" aria-hidden="true"></i>Alerts ({{ h.alerts.length }})</h3></div>
                            <ul class="list-group list-group-flush">
                                @for (a of h.alerts; track a.code) {
                                    <li class="list-group-item">
                                        <fpms-badge [text]="a.severity === 2 ? 'Critical' : 'Warning'" [tone]="a.severity === 2 ? 'danger' : 'warning'"></fpms-badge>
                                        <span class="ml-2">{{ a.message }}</span>
                                    </li>
                                }
                            </ul>
                        </div>
                    } @else {
                        <div class="alert alert-success" role="status"><i class="fas fa-check-circle mr-1"></i>No alerts. Backups are current and storage is fine.</div>
                    }
                    <div class="row">
                        <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ h.databaseSizeGb != null ? (h.databaseSizeGb | num: 2) + ' GB' : '—' }}</div><div class="l">Database size (limit 10 GB)</div></div></div>
                        <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ h.freeDiskPct != null ? (h.freeDiskPct | num: 1) + '%' : '—' }}</div><div class="l">Disk free{{ h.freeDiskGb != null ? ' (' + (h.freeDiskGb | num: 1) + ' GB)' : '' }}</div></div></div>
                        <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ h.errorsLast24Hours }}</div><div class="l">Server errors, last 24 hours</div></div></div>
                        <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ h.appVersion }}</div><div class="l">Version · built {{ h.buildDateUtc | farmDateTime }}</div></div></div>
                    </div>
                    <div class="card">
                        <div class="card-header"><h3 class="card-title"><i class="fas fa-database" aria-hidden="true"></i>Last successful runs</h3></div>
                        <div class="card-body p-0 table-responsive">
                            <table class="table table-sm mb-0">
                                <thead><tr><th>Job</th><th>Last success</th><th>Expected</th></tr></thead>
                                <tbody>
                                    <tr><td>Full backup</td><td>{{ h.lastFullBackupUtc ? (h.lastFullBackupUtc | farmDateTime) : 'Never recorded' }}</td><td>Daily</td></tr>
                                    <tr><td>Transaction-log backup</td><td>{{ h.lastLogBackupUtc ? (h.lastLogBackupUtc | farmDateTime) : 'Never recorded' }}</td><td>Every 30 minutes</td></tr>
                                    <tr><td>Integrity check (CHECKDB)</td><td>{{ h.lastIntegrityCheckUtc ? (h.lastIntegrityCheckUtc | farmDateTime) : 'Never recorded' }}</td><td>Weekly</td></tr>
                                    <tr><td>Off-site copy (encrypted)</td><td>{{ h.lastOffsiteCopyUtc ? (h.lastOffsiteCopyUtc | farmDateTime) : 'Never recorded' }}</td><td>Weekly</td></tr>
                                    <tr><td>Restore test</td><td>{{ h.lastRestoreTestUtc ? (h.lastRestoreTestUtc | farmDateTime) : 'Never recorded' }}</td><td>After each release and quarterly</td></tr>
                                    <tr><td>HTTPS certificate</td><td colspan="2">{{ h.certificateExpiresUtc ? 'Expires ' + (h.certificateExpiresUtc | farmDateTime) : 'Not reported by this server' }}</td></tr>
                                </tbody>
                            </table>
                        </div>
                        <div class="card-footer small text-muted">Server time {{ h.serverTimeUtc | farmDateTime }} ({{ h.timeZone }}).</div>
                    </div>
                }
                <div class="card">
                    <div class="card-header d-flex flex-wrap align-items-center">
                        <h3 class="card-title mr-auto"><i class="fas fa-history" aria-hidden="true"></i>Backup and maintenance history</h3>
                        <select class="form-control form-control-sm w-auto mr-2" aria-label="Filter by job" [(ngModel)]="kind" (ngModelChange)="pager.reset(); loadRuns()">
                            <option [ngValue]="null">All jobs</option>
                            @for (k of kinds; track k.value) {
                                <option [ngValue]="k.value">{{ k.label }}</option>
                            }
                        </select>
                        <div class="custom-control custom-checkbox">
                            <input type="checkbox" class="custom-control-input" id="fo" [(ngModel)]="failedOnly" (ngModelChange)="pager.reset(); loadRuns()" />
                            <label class="custom-control-label small" for="fo">Failures only</label>
                        </div>
                    </div>
                    <div class="card-body p-0 table-responsive">
                        @if (!runs()) {
                            <fpms-loading></fpms-loading>
                        } @else if (!runs().length) {
                            <fpms-empty text="No runs recorded. The backup scripts write a row after each run (see the runbook)." icon="fas fa-database"></fpms-empty>
                        } @else {
                            <table class="table table-sm mb-0">
                                <thead>
                                    <tr>
                                        <th fpmsSort="startedAt" [pager]="pager" (sorted)="loadRuns()">Started</th><th fpmsSort="kind" [pager]="pager" (sorted)="loadRuns()">Job</th>
                                        <th fpmsSort="succeeded" [pager]="pager" (sorted)="loadRuns()">Result</th><th class="text-right" fpmsSort="sizeBytes" [pager]="pager" (sorted)="loadRuns()">Size</th><th>File</th><th>Host</th><th>Message</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    @for (r of runs(); track r.id) {
                                        <tr>
                                            <td class="text-nowrap">{{ r.startedAtUtc | farmDateTime }}</td>
                                            <td>{{ kindLabel(r.kind) }}</td>
                                            <td><fpms-badge [text]="r.succeeded ? 'OK' : 'Failed'" [tone]="r.succeeded ? 'success' : 'danger'"></fpms-badge></td>
                                            <td class="text-right text-nowrap">{{ r.sizeBytes != null ? (r.sizeBytes / 1048576 | num: 1) + ' MB' : '—' }}</td>
                                            <td class="small">{{ r.fileName }}</td>
                                            <td class="small">{{ r.host }}</td>
                                            <td class="small">{{ r.message }}</td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        }
                    </div>
                    <fpms-pager [state]="pager" (changed)="loadRuns()"></fpms-pager>
                </div>
            </div>
        </section>
    `,
})
export class HealthComponent extends FpmsPage implements OnInit, OnDestroy {
    health = signal<any>(null);
    runs = signal<any[] | null>(null);
    readonly kinds = KINDS;
    kind: number | null = null;
    failedOnly = false;
    pager = new PagerState('startedAt desc');
    private timer: ReturnType<typeof setInterval> | undefined;

    ngOnInit(): void {
        this.reload();
        this.timer = setInterval(() => this.reload(), 60_000);
    }

    ngOnDestroy(): void {
        clearInterval(this.timer);
    }

    reload(): void {
        this.run(async () => this.health.set(await this.api.get<any>('SystemHealth', 'GetHealth')));
        this.loadRuns();
    }

    async loadRuns(): Promise<void> {
        const r = await this.api.get<any>('SystemHealth', 'GetBackupRuns', { kind: this.kind, failedOnly: this.failedOnly, ...this.pager.params });
        this.runs.set(r.items);
        if (this.pager.update(r.totalCount)) {
            await this.loadRuns();
        }
    }

    hasCritical(): boolean {
        return (this.health()?.alerts ?? []).some((a: any) => a.severity === 2);
    }

    kindLabel(kind: number): string {
        return KINDS.find((k) => k.value === kind)?.label ?? String(kind);
    }
}
