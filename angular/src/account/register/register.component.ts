import { ChangeDetectorRef, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { WizardSteps } from '@shared/fpms/wizard-steps';
import { accountModuleAnimation } from '@shared/animations/routerTransition';
import { AppAuthService } from '@shared/auth/app-auth.service';
import { FpmsApiService } from '@shared/fpms/api.service';

@Component({
    templateUrl: './register.component.html',
    animations: [accountModuleAnimation()],
    standalone: true,
    imports: [FormsModule, RouterLink],
})
export class RegisterComponent {
    private api = inject(FpmsApiService);
    private auth = inject(AppAuthService);
    private cdr = inject(ChangeDetectorRef);

    farmCodeEdited = false;
    ownerUserNameEdited = false;

    enabled = signal<boolean | null>(null);
    step = signal<1 | 2 | 3>(1);
    saving = signal(false);
    confirmPassword = '';

    model = {
        farmCode: '',
        farmName: '',
        addressLine1: '',
        addressLine2: '',
        cityMunicipality: '',
        province: '',
        postalCode: '',
        country: 'Philippines',
        totalAreaHectares: null as number | null,
        contactName: '',
        contactPhone: '',
        contactEmail: '',

        ownerName: '',
        ownerSurname: '',
        ownerUserName: '',
        ownerEmailAddress: '',
        ownerPassword: '',
    };

    readonly wizard = new WizardSteps(1, () => this.furthestStep(), (n) => this.step.set(n as 1 | 2 | 3));

    constructor() {
        inject(ActivatedRoute)
            .queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef)))
            .subscribe((q) => this.wizard.sync(q));
        this.api
            .get<{ isEnabled: boolean }>('Account', 'GetRegistrationStatus')
            .then((r) => this.enabled.set(!!r?.isEnabled))
            .catch(() => this.enabled.set(false));
    }

    async suggestFarmCode(): Promise<void> {
        if (this.farmCodeEdited || !this.model.farmName?.trim()) {
            return;
        }
        const r = await this.suggestions();
        if (r?.farmCode && !this.farmCodeEdited) {
            this.model.farmCode = r.farmCode;
            this.cdr.markForCheck();
        }
    }

    async suggestOwnerUserName(): Promise<void> {
        if (this.ownerUserNameEdited || (!this.model.ownerName?.trim() && !this.model.ownerSurname?.trim())) {
            return;
        }
        const r = await this.suggestions();
        if (r?.ownerUserName && !this.ownerUserNameEdited) {
            this.model.ownerUserName = r.ownerUserName;
            this.cdr.markForCheck();
        }
    }

    private suggestions(): Promise<{ farmCode?: string; ownerUserName?: string } | null> {
        const m = this.model;
        return this.api
            .get<{ farmCode?: string; ownerUserName?: string }>('Account', 'GetRegistrationSuggestions', {
                farmName: m.farmName?.trim(),
                ownerName: m.ownerName?.trim(),
                ownerSurname: m.ownerSurname?.trim(),
            })
            .catch(() => null);
    }

    private furthestStep(): number {
        const m = this.model;
        if (!m.farmCode || !m.farmName || !this.addressComplete) {
            return 1;
        }
        const ownerDone = !!m.ownerName && !!m.ownerSurname && !!m.ownerUserName && !!m.ownerEmailAddress && m.ownerPassword.length >= 8 && this.passwordsMatch;
        return ownerDone ? 3 : 2;
    }

    get addressComplete(): boolean {
        const m = this.model;
        return !!m.addressLine1?.trim() && !!m.cityMunicipality?.trim() && !!m.province?.trim() && !!m.country?.trim() && this.postalOk;
    }

    get postalOk(): boolean {
        const code = this.model.postalCode?.trim();
        return !code || this.model.country?.trim().toLowerCase() !== 'philippines' || /^[0-9]{4}$/.test(code);
    }

    get fullAddress(): string {
        const m = this.model;
        const prov = [m.province, m.postalCode].map((s) => s?.trim()).filter((s) => !!s).join(' ');
        return [m.addressLine1, m.addressLine2, m.cityMunicipality, prov, m.country].map((s) => s?.trim()).filter((s) => !!s).join(', ');
    }

    get passwordsMatch(): boolean {
        return this.model.ownerPassword === this.confirmPassword;
    }

    async submit(): Promise<void> {
        this.saving.set(true);
        try {
            abp.multiTenancy.setTenantIdCookie(undefined);
            await this.api.post('Account', 'RegisterFarm', this.model);
            abp.notify.success('Farm account created. Signing you in…');
            this.auth.farmCode = this.model.farmCode;
            this.auth.authenticateModel.userNameOrEmailAddress = this.model.ownerUserName;
            this.auth.authenticateModel.password = this.model.ownerPassword;
            await this.auth.authenticate();
        } catch {
        } finally {
            this.saving.set(false);
        }
    }
}
