import { Component, OnInit } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header [narrow]="true" title="Account recovery" subtitle="Unlock an account or issue a temporary password. System Administration."></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid fpms-narrow">
                <div class="alert alert-info small">
                    <i class="fas fa-user-shield mr-1"></i>Before acting, verify the person's identity through a second Farm Owner of the same farm or the pre-agreed security question.
                    Every action is recorded in the security log.
                </div>
                <div class="card">
                    <div class="card-body">
                        <div class="form-group">
                            <label for="farm">Farm</label>
                            <select id="farm" class="form-control" [(ngModel)]="tenantId" (ngModelChange)="loadUsers()">
                                <option [ngValue]="null">Choose a farm</option>
                                @for (x of farms; track x.tenantId) {
                                    <option [ngValue]="x.tenantId">{{ x.farmCode }} · {{ x.name }}</option>
                                }
                            </select>
                        </div>
                        @if (users) {
                            <div class="table-responsive">
                            <table class="table table-sm mb-0">
                                <thead><tr><th></th><th>Username</th><th>Name</th><th>Role</th><th>Status</th></tr></thead>
                                <tbody>
                                    @for (u of users; track u.userId) {
                                        <tr [class.table-active]="selected?.userId === u.userId" (click)="select(u)" style="cursor: pointer">
                                            <td><input type="radio" name="user" [checked]="selected?.userId === u.userId" [attr.aria-label]="'Select ' + u.userName" /></td>
                                            <td>{{ u.userName }}</td>
                                            <td>{{ u.fullName }}</td>
                                            <td>{{ u.roleName }}</td>
                                            <td>
                                                @if (!u.isActive) { <fpms-badge text="Deactivated" tone="secondary"></fpms-badge> }
                                                @if (u.isLockedOut) { <fpms-badge text="Locked out" tone="danger"></fpms-badge> }
                                                @if (u.mustChangePassword) { <fpms-badge text="Must change password" tone="warning"></fpms-badge> }
                                                @if (u.isActive && !u.isLockedOut && !u.mustChangePassword) { <fpms-badge text="Active" tone="success"></fpms-badge> }
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                            </div>
                        }
                    </div>
                </div>

                @if (selected) {
                    <div class="card card-outline card-warning">
                        <div class="card-header"><h3 class="card-title"><i class="fas fa-key" aria-hidden="true"></i>Recover {{ selected.userName }}</h3></div>
                        <div class="card-body">
                            <div class="form-group">
                                <label for="vm">Identity verified through</label>
                                <select id="vm" class="form-control" [(ngModel)]="method">
                                    @for (o of Options.verification; track o.value) {
                                        <option [ngValue]="o.value">{{ o.label }}</option>
                                    }
                                </select>
                            </div>
                            <div class="form-group">
                                <label for="note">Note</label>
                                <input id="note" class="form-control" [(ngModel)]="note" maxlength="300" placeholder="e.g. confirmed by second owner J. Cruz by phone" />
                            </div>
                            <div class="custom-control custom-checkbox">
                                <input type="checkbox" class="custom-control-input" id="ver" [(ngModel)]="verified" />
                                <label class="custom-control-label" for="ver">I verified this person's identity.</label>
                            </div>
                        </div>
                        <div class="card-footer">
                            <button class="btn btn-outline-primary mr-2" [disabled]="busy() || !verified" (click)="unlock()">Unlock account</button>
                            <button class="btn btn-warning" [disabled]="busy() || !verified" (click)="issue()">Issue temporary password</button>
                        </div>
                    </div>
                }

                @if (temp) {
                    <div class="card card-outline card-success">
                        <div class="card-body">
                            <p class="mb-1">Temporary password for <strong>{{ temp.userName }}</strong> (shown once):</p>
                            <p class="h3 font-weight-bold mb-2"><code>{{ temp.temporaryPassword }}</code></p>
                            <p class="small text-muted mb-0">Give it to the person directly. They must choose a new password at their next sign-in.</p>
                        </div>
                        <div class="card-footer"><button class="btn btn-default btn-sm" (click)="temp = null">Done</button></div>
                    </div>
                }
            </div>
        </section>
    `,
})
export class RecoveryComponent extends FpmsPage implements OnInit {
    farms: any[] = [];
    users: any[] | null = null;
    tenantId: number | null = null;
    selected: any = null;
    method = 1;
    note = '';
    verified = false;
    temp: any = null;

    async ngOnInit(): Promise<void> {
        this.farms = await this.api.get<any[]>('SystemFarm', 'GetFarms');
        const id = Number(this.query('tenantId'));
        if (id) {
            this.tenantId = id;
            this.loadUsers();
        }
    }

    async loadUsers(): Promise<void> {
        this.selected = null;
        this.temp = null;
        this.users = this.tenantId ? await this.api.get<any[]>('AccountRecovery', 'GetFarmUsers', { tenantId: this.tenantId }) : null;
    }

    select(u: any): void {
        this.selected = u;
        this.verified = false;
        this.note = '';
        this.temp = null;
    }

    private input(): any {
        return { tenantId: this.tenantId, userId: this.selected.userId, identityVerified: this.verified, verificationMethod: this.method, note: this.note };
    }

    async unlock(): Promise<void> {
        if (await this.act(() => this.api.post('AccountRecovery', 'Unlock', this.input()))) {
            this.notifySaved('Account unlocked.');
            this.loadUsers();
        }
    }

    async issue(): Promise<void> {
        let result: any;
        if (await this.act(async () => (result = await this.api.post<any>('AccountRecovery', 'IssueTemporaryPassword', this.input())))) {
            await this.loadUsers();
            this.temp = result;
        }
    }
}
