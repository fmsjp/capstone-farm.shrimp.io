import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FPMS_IMPORTS } from '../shared-imports';
import { ClientListFilter } from '@shared/fpms/client-list';

const ROLES = [
    { value: 'FarmOwner', label: 'Farm Owner' },
    { value: 'FarmTechnician', label: 'Farm Technician' },
];

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Farm users" subtitle="Farm Owner and Farm Technician accounts of this farm.">
            <button class="btn btn-primary" (click)="startCreate()" [disabled]="!!form"><i class="fas fa-user-plus mr-1"></i>Add user</button>
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (form) {
                    <div class="fpms-dialog-backdrop"><div class="fpms-dialog" role="dialog" aria-modal="true"><div class="fpms-dialog-body p-0">
                        <div class="card card-outline card-primary">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-user" aria-hidden="true"></i>{{ form.id ? 'Edit account' : 'New account' }}</h3></div>
                            <form #f="ngForm" (ngSubmit)="save()" novalidate autocomplete="off">
                                <div class="card-body">
                                    <div class="form-row">
                                        <div class="form-group col-md-3">
                                            <label for="name">First name <span class="text-danger">*</span></label>
                                            <input id="name" class="form-control" name="name" [(ngModel)]="form.name" (blur)="suggestUserName()" required maxlength="64" />
                                        </div>
                                        <div class="form-group col-md-3">
                                            <label for="surname">Surname <span class="text-danger">*</span></label>
                                            <input id="surname" class="form-control" name="surname" [(ngModel)]="form.surname" (blur)="suggestUserName()" required maxlength="64" />
                                        </div>
                                        <div class="form-group col-md-3">
                                            <label for="role">Role <span class="text-danger">*</span></label>
                                            <select id="role" class="form-control" name="roleName" [(ngModel)]="form.roleName" required>
                                                @for (r of roles; track r.value) {
                                                    <option [value]="r.value">{{ r.label }}</option>
                                                }
                                            </select>
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group col-md-3">
                                            <label for="userName">Username <span class="text-danger">*</span></label>
                                            <input id="userName" class="form-control" name="userName" [(ngModel)]="form.userName" (ngModelChange)="userNameEdited = !!$event" required maxlength="256" autocapitalize="off" [disabled]="!!form.id" />
                                            @if (!form.id) {
                                                <small class="form-text text-muted">Filled in from the name; you can change it. Fixed once saved.</small>
                                            }
                                        </div>
                                        <div class="form-group col-md-3">
                                            <label for="email">Email <span class="text-danger">*</span></label>
                                            <input id="email" type="email" email class="form-control" name="emailAddress" [(ngModel)]="form.emailAddress" required maxlength="256" />
                                        </div>
                                        @if (!form.id) {
                                            <div class="form-group col-md-3">
                                                <label for="pw">Temporary password <span class="text-danger">*</span></label>
                                                <div class="input-group">
                                                    <input id="pw" class="form-control" name="temporaryPassword" [(ngModel)]="form.temporaryPassword" required minlength="8" autocomplete="new-password" />
                                                    <div class="input-group-append">
                                                        <button type="button" class="btn btn-outline-secondary" (click)="form.temporaryPassword = generate()" title="Generate">
                                                            <i class="fas fa-dice"></i>
                                                        </button>
                                                    </div>
                                                </div>
                                                <small class="form-text text-muted">Give it to the person; they must change it at first sign-in.</small>
                                            </div>
                                        }
                                    </div>
                                </div>
                                <div class="card-footer">
                                    <button type="submit" class="btn btn-primary mr-2" [disabled]="busy() || !f.form.valid">Save</button>
                                    <button type="button" class="btn btn-default" (click)="form = null">Cancel</button>
                                    <fpms-form-hint [form]="f"></fpms-form-hint>
                                </div>
                            </form>
                        </div>
                    </div></div></div>
                }

                @if (reset) {
                    <div class="card card-outline card-warning">
                        <div class="card-header"><h3 class="card-title"><i class="fas fa-key" aria-hidden="true"></i>Reset password for {{ reset.user.name }} {{ reset.user.surname }}</h3></div>
                        <div class="card-body">
                            <div class="form-row align-items-end">
                                <div class="form-group col-md-4">
                                    <label for="rpw">New temporary password</label>
                                    <div class="input-group">
                                        <input id="rpw" class="form-control" name="rpw" [(ngModel)]="reset.password" minlength="8" autocomplete="new-password" />
                                        <div class="input-group-append">
                                            <button type="button" class="btn btn-outline-secondary" (click)="reset.password = generate()" aria-label="Generate a password" title="Generate a password"><i class="fas fa-dice" aria-hidden="true"></i></button>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            <p class="small text-muted mb-0">The account is unlocked and must set a new password at next sign-in.</p>
                        </div>
                        <div class="card-footer">
                            <button class="btn btn-warning mr-2" [disabled]="busy() || (reset.password?.length ?? 0) < 8" (click)="doReset()">Reset password</button>
                            <button class="btn btn-default" (click)="reset = null">Cancel</button>
                        </div>
                    </div>
                }

                <div class="card">
                    <div class="card-header">
                        <fpms-list-toolbar
                            placeholder="Search by name, username or email"
                            [search]="f.search()"
                            [resultText]="f.resultText()"
                            [chips]="f.chips()"
                            [chip]="f.chip()"
                            (searchChange)="f.search.set($event)"
                            (chipChange)="f.chip.set($event)"></fpms-list-toolbar>
                    </div>
                    <div class="card-body p-0 table-responsive">
                        @if (!users()) {
                            <fpms-loading></fpms-loading>
                        } @else if (f.noMatch()) {
                            <fpms-empty text="No accounts match. Clear the search or the filter." icon="fas fa-users"></fpms-empty>
                        } @else {
                            <table class="table table-hover mb-0">
                                <thead>
                                    <tr><th>Name</th><th>Username</th><th>Email</th><th>Role</th><th>Status</th><th><span class="sr-only">Actions</span></th></tr>
                                </thead>
                                <tbody>
                                    @for (u of f.visible(); track u.id) {
                                        <tr [class.text-muted]="!u.isActive">
                                            <td>{{ u.name }} {{ u.surname }} @if (u.isCurrentUser) { <span class="badge badge-light">you</span> }</td>
                                            <td>{{ u.userName }}</td>
                                            <td>{{ u.emailAddress }}</td>
                                            <td>{{ roleLabel(u.roleName) }}</td>
                                            <td>
                                                <fpms-badge [text]="u.isActive ? 'Active' : 'Deactivated'" [tone]="u.isActive ? 'success' : 'secondary'"></fpms-badge>
                                                @if (u.isLockedOut) { <fpms-badge text="Locked out" tone="danger"></fpms-badge> }
                                                @if (u.mustChangePassword) { <fpms-badge text="Must change password" tone="warning"></fpms-badge> }
                                            </td>
                                            <td class="text-right text-nowrap">
                                                <button class="btn btn-sm btn-outline-primary mr-1" (click)="startEdit(u)">Edit</button>
                                                @if (!u.isCurrentUser) {
                                                    <button class="btn btn-sm btn-outline-secondary mr-1" (click)="reset = { user: u, password: generate() }">Reset password</button>
                                                    <button class="btn btn-sm" [class.btn-outline-danger]="u.isActive" [class.btn-outline-success]="!u.isActive" (click)="toggle(u)">
                                                        {{ u.isActive ? 'Deactivate' : 'Reactivate' }}
                                                    </button>
                                                }
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        }
                    </div>
                </div>
            </div>
        </section>
    `,
})
export class FarmUsersComponent extends FpmsPage implements OnInit {
    readonly roles = ROLES;
    users = signal<any[] | null>(null);
    f = new ClientListFilter<any>(
        () => this.users(),
        (u) => [u.name, u.surname, u.userName, u.emailAddress],
        (u) => (u.isActive ? 'active' : 'deactivated'),
        [{ value: 'active', label: 'Active' }, { value: 'deactivated', label: 'Deactivated' }],
        'All accounts',
        ['account', 'accounts']
    );
    form: any = null;
    reset: { user: any; password: string } | null = null;

    userNameEdited = false;

    ngOnInit(): void {
        this.load();
    }

    async load(): Promise<void> {
        this.users.set(await this.api.get<any[]>('FarmUser', 'GetAll'));
    }

    roleLabel(name: string): string {
        return ROLES.find((r) => r.value === name)?.label ?? name ?? '—';
    }

    startCreate(): void {
        this.reset = null;
        this.userNameEdited = false;
        this.form = { name: '', surname: '', userName: '', emailAddress: '', roleName: 'FarmTechnician', temporaryPassword: this.generate() };
    }

    async suggestUserName(): Promise<void> {
        const f = this.form;
        if (!f || f.id || this.userNameEdited || (!f.name?.trim() && !f.surname?.trim())) {
            return;
        }
        const r = await this.api
            .get<{ userName?: string }>('FarmUser', 'GetSuggestedUserName', { name: (f.name ?? '').trim(), surname: (f.surname ?? '').trim() })
            .catch(() => null);
        if (r?.userName && this.form === f && !this.userNameEdited) {
            f.userName = r.userName;
        }
    }

    startEdit(u: any): void {
        this.reset = null;
        this.form = { id: u.id, name: u.name, surname: u.surname, userName: u.userName, emailAddress: u.emailAddress, roleName: u.roleName };
    }

    async save(): Promise<void> {
        const f = this.form;
        const ok = await this.act(() =>
            f.id
                ? this.api.put('FarmUser', 'Update', { id: f.id, name: f.name, surname: f.surname, emailAddress: f.emailAddress, roleName: f.roleName })
                : this.api.post('FarmUser', 'Create', f)
        );
        if (ok) {
            if (!f.id) {
                abp.message.info(`Account "${f.userName}" created. Temporary password: ${f.temporaryPassword}`, 'Account created');
            } else {
                this.notifySaved();
            }
            this.form = null;
            this.load();
        }
    }

    async doReset(): Promise<void> {
        const r = this.reset;
        if (await this.act(() => this.api.post('FarmUser', 'ResetPassword', { id: r.user.id, temporaryPassword: r.password }))) {
            abp.message.info(`Temporary password for "${r.user.userName}": ${r.password}`, 'Password reset');
            this.reset = null;
            this.load();
        }
    }

    async toggle(u: any): Promise<void> {
        const action = u.isActive ? 'Deactivate' : 'Reactivate';
        const message = u.isActive
            ? `${u.name} ${u.surname} will no longer be able to sign in. Their records are kept.`
            : `${u.name} ${u.surname} will be able to sign in again.`;
        if (!(await this.confirm(message, `${action} account?`))) {
            return;
        }
        if (await this.act(() => this.api.post('FarmUser', 'SetActive', { id: u.id, isActive: !u.isActive }))) {
            this.load();
        }
    }

    generate(): string {
        const upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
        const lower = 'abcdefghijkmnpqrstuvwxyz';
        const digits = '23456789';
        const all = upper + lower + digits;
        const pick = (set: string) => set[Math.floor(Math.random() * set.length)];
        const chars = [pick(upper), pick(lower), pick(digits)];
        while (chars.length < 10) {
            chars.push(pick(all));
        }
        return chars.sort(() => Math.random() - 0.5).join('');
    }
}
