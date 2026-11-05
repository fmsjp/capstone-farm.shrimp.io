import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { accountModuleAnimation } from '@shared/animations/routerTransition';
import { AppAuthService } from '@shared/auth/app-auth.service';
import { FpmsApiService } from '@shared/fpms/api.service';

@Component({
    templateUrl: './login.component.html',
    animations: [accountModuleAnimation()],
    standalone: true,
    imports: [FormsModule, RouterLink],
})
export class LoginComponent {
    authService = inject(AppAuthService);
    private api = inject(FpmsApiService);
    submitting = signal(false);
    registrationEnabled = signal(false);

    constructor() {
        this.api
            .get<{ isEnabled: boolean }>('Account', 'GetRegistrationStatus')
            .then((r) => this.registrationEnabled.set(!!r?.isEnabled))
            .catch(() => this.registrationEnabled.set(false));
    }

    login(): void {
        this.submitting.set(true);
        this.authService.authenticate(() => this.submitting.set(false));
    }
}
