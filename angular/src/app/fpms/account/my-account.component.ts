import { Component, OnInit } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FPMS_IMPORTS } from '../shared-imports';
import { signOutLocally } from '@shared/fpms/session';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header [narrow]="true" title="My account" subtitle="Your own sign-in details."></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid fpms-narrow">
                <div class="row">
                    <div class="col-lg-6">
                        <div class="card">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-id-card" aria-hidden="true"></i>Profile</h3></div>
                            @if (!p) {
                                <fpms-loading></fpms-loading>
                            } @else {
                                <form #pf="ngForm" (ngSubmit)="saveProfile()" novalidate>
                                    <div class="card-body">
                                        <div class="form-group">
                                            <label for="un">Username</label>
                                            <input id="un" class="form-control" [value]="p.userName" disabled />
                                        </div>
                                        <div class="form-row">
                                            <div class="form-group col-6">
                                                <label for="n">First name</label>
                                                <input id="n" class="form-control" name="name" [(ngModel)]="p.name" required maxlength="64" />
                                            </div>
                                            <div class="form-group col-6">
                                                <label for="s">Surname</label>
                                                <input id="s" class="form-control" name="surname" [(ngModel)]="p.surname" required maxlength="64" />
                                            </div>
                                        </div>
                                        <div class="form-group">
                                            <label for="e">Email</label>
                                            <input id="e" type="email" email class="form-control" name="emailAddress" [(ngModel)]="p.emailAddress" required maxlength="256" />
                                        </div>
                                    </div>
                                    <div class="card-footer">
                                        <button class="btn btn-primary" type="submit" [disabled]="busy() || !pf.form.valid">Save profile</button>
                                        <fpms-form-hint [form]="pf"></fpms-form-hint>
                                    </div>
                                </form>
                            }
                        </div>
                    </div>
                    <div class="col-lg-6">
                        <div class="card">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-key" aria-hidden="true"></i>Change password</h3></div>
                            <form #wf="ngForm" (ngSubmit)="changePassword()" novalidate>
                                <div class="card-body">
                                    <div class="form-group">
                                        <label for="cur">Current password</label>
                                        <input id="cur" type="password" class="form-control" name="current" [(ngModel)]="pw.current" required autocomplete="current-password" />
                                    </div>
                                    <div class="form-group">
                                        <label for="nw">New password</label>
                                        <input id="nw" type="password" class="form-control" name="next" [(ngModel)]="pw.next" required minlength="8" autocomplete="new-password" />
                                        <small class="form-text text-muted">At least 8 characters with upper- and lower-case letters and a digit.</small>
                                    </div>
                                    <div class="form-group">
                                        <label for="cf">Confirm new password</label>
                                        <input id="cf" type="password" class="form-control" name="confirm" [(ngModel)]="pw.confirm" required autocomplete="new-password" />
                                        @if (pw.confirm && pw.confirm !== pw.next) {
                                            <small class="text-danger">Passwords do not match.</small>
                                        }
                                    </div>
                                </div>
                                <div class="card-footer">
                                    <button class="btn btn-primary" type="submit" [disabled]="busy() || !wf.form.valid || pw.next.length < 8 || pw.next !== pw.confirm">Change password</button>
                                    <fpms-form-hint [form]="wf"></fpms-form-hint>
                                </div>
                            </form>
                        </div>
                    </div>
                </div>
            </div>
        </section>
    `,
})
export class MyAccountComponent extends FpmsPage implements OnInit {
    p: any = null;
    pw = { current: '', next: '', confirm: '' };

    async ngOnInit(): Promise<void> {
        this.p = await this.api.get<any>('Profile', 'GetMyProfile');
    }

    async saveProfile(): Promise<void> {
        if (await this.act(() => this.api.put('Profile', 'UpdateMyProfile', this.p))) {
            this.notifySaved('Profile saved.');
        }
    }

    async changePassword(): Promise<void> {
        if (await this.act(() => this.api.post('Profile', 'ChangeMyPassword', { currentPassword: this.pw.current, newPassword: this.pw.next }))) {
            Promise.resolve(abp.message.info('Password changed. Please sign in again with your new password.', 'Password changed')).then(() => signOutLocally());
        }
    }
}
