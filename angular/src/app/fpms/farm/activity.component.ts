import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime } from '@shared/fpms/farm-time';
import { PagerState } from '@shared/fpms/pager';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Activity log" subtitle="Who did what in this farm."></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <div class="card">
                    <div class="card-body pb-1">
                        <div class="form-row align-items-end">
                            <div class="form-group col-6 col-md-2">
                                <label for="from">From</label>
                                <input id="from" type="date" class="form-control" [(ngModel)]="f.from" />
                            </div>
                            <div class="form-group col-6 col-md-2">
                                <label for="to">To</label>
                                <input id="to" type="date" class="form-control" [(ngModel)]="f.to" />
                            </div>
                            <div class="form-group col-6 col-md-3">
                                <label for="user">User</label>
                                <select id="user" class="form-control" [(ngModel)]="f.userId">
                                    <option [ngValue]="null">All users</option>
                                    @for (u of users(); track u.id) {
                                        <option [ngValue]="u.id">{{ u.name }} {{ u.surname }}</option>
                                    }
                                </select>
                            </div>
                            <div class="form-group col-6 col-md-3">
                                <label for="rt">Record type</label>
                                <select id="rt" class="form-control" [(ngModel)]="f.recordType">
                                    <option [ngValue]="null">All record types</option>
                                    @for (t of recordTypes(); track t) {
                                        <option [ngValue]="t">{{ t }}</option>
                                    }
                                </select>
                            </div>
                            <div class="form-group col-md-2">
                                <button class="btn btn-primary btn-block" (click)="apply()" [disabled]="busy()">Apply</button>
                            </div>
                        </div>
                    </div>
                </div>
                <ul class="nav nav-tabs mb-0">
                    <li class="nav-item"><a class="nav-link" href="javascript:;" [class.active]="tab === 'changes'" (click)="tab = 'changes'">Data changes</a></li>
                    <li class="nav-item"><a class="nav-link" href="javascript:;" [class.active]="tab === 'actions'" (click)="tab = 'actions'">User actions</a></li>
                </ul>
                <div class="card card-tabs-body">
                    <div class="card-body p-0 table-responsive">
                        @if (!changes() || !actions()) {
                            <fpms-loading></fpms-loading>
                        } @else if (tab === 'changes') {
                            <table class="table table-sm mb-0">
                                <thead><tr><th>Time</th><th>User</th><th>Record</th><th>Change</th><th>Values</th></tr></thead>
                                <tbody>
                                    @for (c of changes(); track $index) {
                                        <tr>
                                            <td class="text-nowrap">{{ c.changeTime | farmDateTime }}</td>
                                            <td>{{ c.userName }}</td>
                                            <td>{{ c.recordType }}</td>
                                            <td>{{ c.changeType }}</td>
                                            <td class="small text-muted">{{ c.changes.join('; ') }}</td>
                                        </tr>
                                    } @empty {
                                        <tr><td colspan="5" class="text-center text-muted py-3">No data changes in this range.</td></tr>
                                    }
                                </tbody>
                            </table>
                        } @else {
                            <table class="table table-sm mb-0">
                                <thead><tr><th>Time</th><th>User</th><th>Area</th><th>Action</th><th>Result</th></tr></thead>
                                <tbody>
                                    @for (a of actions(); track $index) {
                                        <tr>
                                            <td class="text-nowrap">{{ a.executionTime | farmDateTime }}</td>
                                            <td>{{ a.userName }}</td>
                                            <td>{{ a.area }}</td>
                                            <td>{{ a.action }}</td>
                                            <td><fpms-badge [text]="a.failed ? 'Failed' : 'OK'" [tone]="a.failed ? 'danger' : 'success'"></fpms-badge></td>
                                        </tr>
                                    } @empty {
                                        <tr><td colspan="5" class="text-center text-muted py-3">No user actions in this range.</td></tr>
                                    }
                                </tbody>
                            </table>
                        }
                    </div>
                    @if (tab === 'changes') {
                        <fpms-pager [state]="changePager" (changed)="loadChanges()"></fpms-pager>
                    } @else {
                        <fpms-pager [state]="actionPager" (changed)="loadActions()"></fpms-pager>
                    }
                </div>
            </div>
        </section>
    `,
})
export class ActivityComponent extends FpmsPage implements OnInit {
    changes = signal<any[] | null>(null);
    actions = signal<any[] | null>(null);
    recordTypes = signal<string[]>([]);
    changePager = new PagerState();
    actionPager = new PagerState();
    users = signal<any[]>([]);
    tab: 'changes' | 'actions' = 'changes';
    f: any = { from: FarmTime.addDays(FarmTime.today(), -7), to: FarmTime.today(), userId: null, recordType: null };

    ngOnInit(): void {
        this.api.get<any[]>('FarmUser', 'GetAll').then((u) => this.users.set(u ?? []));
        this.api.get<string[]>('ActivityLog', 'GetRecordTypes').then((t) => this.recordTypes.set(t ?? []));
        this.load();
    }

    apply(): void {
        this.changePager.reset();
        this.actionPager.reset();
        this.load();
    }

    async load(): Promise<void> {
        await Promise.all([this.loadChanges(), this.loadActions()]);
    }

    async loadChanges(): Promise<void> {
        await this.run(async () => {
            const r = await this.api.get<any>('ActivityLog', 'GetDataChanges', { ...this.f, ...this.changePager.params });
            this.changes.set(r.items);
            if (this.changePager.update(r.totalCount)) {
                await this.loadChanges();
            }
        });
    }

    async loadActions(): Promise<void> {
        await this.run(async () => {
            const r = await this.api.get<any>('ActivityLog', 'GetActions', { ...this.f, ...this.actionPager.params });
            this.actions.set(r.items);
            if (this.actionPager.update(r.totalCount)) {
                await this.loadActions();
            }
        });
    }
}
