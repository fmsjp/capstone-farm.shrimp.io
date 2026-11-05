import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AppAuthService } from '@shared/auth/app-auth.service';
import { FpmsApiService } from '@shared/fpms/api.service';

@Component({
    standalone: true,
    imports: [FormsModule],
    template: `
        <h4 class="text-center mb-2">Choose a new password</h4>
        <p class="text-muted small text-center">Your account has a temporary password. Set your own password to continue.</p>
        @if (!auth.pendingPasswordChange) {
            <div class="alert alert-info small">Sign in with your temporary password first.</div>
            <button class="btn btn-primary btn-block" (click)="router.navigate(['account/login'])">Go to sign in</button>
        } @else {
            <form #f="ngForm" (ngSubmit)="save()" novalidate>
                <div class="form-group">
                    <label for="current">Temporary password</label>
                    <input id="current" type="password" class="form-control" name="current" [(ngModel)]="current" required />
                </div>
                <div class="form-group">
                    <label for="next">New password</label>
                    <input id="next" type="password" class="form-control" name="next" [(ngModel)]="next" required minlength="8" />
                    <small class="form-text text-muted">At least 8 characters with upper- and lower-case letters and a digit.</small>
                </div>
                <div class="form-group">
                    <label for="confirm">Confirm new password</label>
                    <input id="confirm" type="password" class="form-control" name="confirm" [(ngModel)]="confirm" required />
                    @if (confirm && confirm !== next) {
                        <small class="text-danger">Passwords do not match.</small>
                    }
                </div>
                <button type="submit" class="btn btn-primary btn-block" [disabled]="saving() || !current || next.length < 8 || next !== confirm">
                    Save and continue
                </button>
                <button type="button" class="btn btn-link btn-block" (click)="auth.logout()">Cancel and sign out</button>
            </form>
        }
    `,
})
export class ChangePasswordComponent {
    auth = inject(AppAuthService);
    router = inject(Router);
    private api = inject(FpmsApiService);
    saving = signal(false);
    current = '';
    next = '';
    confirm = '';

    async save(): Promise<void> {
        this.saving.set(true);
        try {
            await this.api.post('Profile', 'ChangeMyPassword', { currentPassword: this.current, newPassword: this.next });
            abp.notify.success('Password changed.');
            const pending = this.auth.pendingPasswordChange;
            this.auth.pendingPasswordChange = null;
            this.auth.farmCode = pending.farmCode;
            this.auth.authenticateModel.userNameOrEmailAddress = pending.userName;
            this.auth.authenticateModel.password = this.next;
            await this.auth.authenticate();
        } catch {
        } finally {
            this.saving.set(false);
        }
    }
}
