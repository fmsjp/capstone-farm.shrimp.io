import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime } from '@shared/fpms/farm-time';
import { PagerState } from '@shared/fpms/pager';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Security and error logs" subtitle="Sign-in attempts and the errors the system recorded. No farm records are shown. System Administration."></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <div class="card">
                    <div class="card-body pb-1">
                        <div class="form-row align-items-end">
                            <div class="form-group col-6 col-md-2">
                                <label for="from">From</label>
                                <input id="from" type="date" class="form-control" [(ngModel)]="from" />
                            </div>
                            <div class="form-group col-6 col-md-2">
                                <label for="to">To</label>
                                <input id="to" type="date" class="form-control" [(ngModel)]="to" />
                            </div>
                            <div class="form-group col-md-3">
                                <label for="farm">Farm</label>
                                <select id="farm" class="form-control" [(ngModel)]="tenantId">
                                    <option [ngValue]="null">All (including host)</option>
                                    @for (x of farms(); track x.tenantId) {
                                        <option [ngValue]="x.tenantId">{{ x.farmCode }}</option>
                                    }
                                </select>
                            </div>
                            <div class="form-group col-md-3">
                                <div class="custom-control custom-checkbox mb-2">
                                    <input type="checkbox" class="custom-control-input" id="fo" [(ngModel)]="failuresOnly" />
                                    <label class="custom-control-label" for="fo">Failures only</label>
                                </div>
                            </div>
                            <div class="form-group col-md-2">
                                <button class="btn btn-primary btn-block" (click)="apply()" [disabled]="busy()">Apply</button>
                            </div>
                        </div>
                    </div>
                </div>
                <ul class="nav nav-tabs">
                    <li class="nav-item"><a class="nav-link" href="javascript:;" [class.active]="tab === 'login'" (click)="tab = 'login'">Sign-in attempts</a></li>
                    <li class="nav-item"><a class="nav-link" href="javascript:;" [class.active]="tab === 'events'" (click)="tab = 'events'">System events</a></li>
                </ul>
                <div class="card">
                    <div class="card-body p-0 table-responsive">
                        @if (tab === 'login') {
                            <table class="table table-sm mb-0">
                                <thead><tr><th>Time</th><th>Farm code</th><th>Username / email</th><th>Result</th><th>IP address</th></tr></thead>
                                <tbody>
                                    @for (a of attempts(); track $index) {
                                        <tr>
                                            <td class="text-nowrap">{{ a.creationTime | farmDateTime }}</td>
                                            <td>{{ a.farmCode || '(host)' }}</td>
                                            <td>{{ a.userNameOrEmail }}</td>
                                            <td><fpms-badge [text]="a.result" [tone]="a.result === 'Success' ? 'success' : 'danger'"></fpms-badge></td>
                                            <td class="small">{{ a.clientIpAddress }}</td>
                                        </tr>
                                    } @empty {
                                        <tr><td colspan="5" class="text-center text-muted py-3">No sign-in attempts in this range.</td></tr>
                                    }
                                </tbody>
                            </table>
                        } @else {
                            <table class="table table-sm mb-0">
                                <thead><tr><th>Time</th><th>Farm</th><th>User ID</th><th>Service</th><th>Method</th><th class="text-right">ms</th><th>Result</th></tr></thead>
                                <tbody>
                                    @for (e of events(); track $index) {
                                        <tr>
                                            <td class="text-nowrap">{{ e.executionTime | farmDateTime }}</td>
                                            <td>{{ farmCode(e.tenantId) }}</td>
                                            <td>{{ e.userId ?? '' }}</td>
                                            <td>{{ e.service }}</td>
                                            <td>{{ e.method }}</td>
                                            <td class="text-right">{{ e.durationMs }}</td>
                                            <td>
                                                @if (e.failed) {
                                                    <fpms-badge text="Failed" tone="danger"></fpms-badge> <span class="small">{{ e.errorType }}</span>
                                                } @else {
                                                    <fpms-badge text="OK" tone="success"></fpms-badge>
                                                }
                                            </td>
                                        </tr>
                                    } @empty {
                                        <tr><td colspan="7" class="text-center text-muted py-3">No events in this range.</td></tr>
                                    }
                                </tbody>
                            </table>
                        }
                    </div>
                    @if (tab === 'login') {
                        <fpms-pager [state]="loginPager" (changed)="load()"></fpms-pager>
                    } @else {
                        <fpms-pager [state]="eventPager" (changed)="load()"></fpms-pager>
                    }
                </div>
                <p class="small text-muted">These logs never include farm record contents, request parameters or passwords.</p>
            </div>
        </section>
    `,
})
export class SecurityLogsComponent extends FpmsPage implements OnInit {
    attempts = signal<any[]>([]);
    events = signal<any[]>([]);
    farms = signal<any[]>([]);
    tab: 'login' | 'events' = 'login';
    from = FarmTime.addDays(FarmTime.today(), -7);
    to = FarmTime.today();
    tenantId: number | null = null;
    failuresOnly = false;
    loginPager = new PagerState();
    eventPager = new PagerState();

    ngOnInit(): void {
        this.api.get<any[]>('SystemFarm', 'GetFarms').then((f) => this.farms.set(f ?? []));
        this.load();
    }

    farmCode(tenantId: number | null): string {
        return tenantId ? (this.farms().find((f) => f.tenantId === tenantId)?.farmCode ?? String(tenantId)) : '(host)';
    }

    apply(): void {
        this.loginPager.reset();
        this.eventPager.reset();
        this.load();
    }

    async load(): Promise<void> {
        const input = {
            fromUtc: this.from ? FarmTime.fromLocalInput(`${this.from}T00:00`) : null,
            toUtc: this.to ? FarmTime.fromLocalInput(`${FarmTime.addDays(this.to, 1)}T00:00`) : null,
            tenantId: this.tenantId,
            failuresOnly: this.failuresOnly,
        };
        await this.run(async () => {
            const [a, e] = await Promise.all([
                this.api.get<any>('SecurityLog', 'GetLoginAttempts', { ...input, ...this.loginPager.params }),
                this.api.get<any>('SecurityLog', 'GetSystemEvents', { ...input, ...this.eventPager.params }),
            ]);
            this.attempts.set(a.items);
            this.events.set(e.items);
            const stale = this.loginPager.update(a.totalCount) || this.eventPager.update(e.totalCount);
            if (stale) {
                await this.load();
            }
        });
    }
}
