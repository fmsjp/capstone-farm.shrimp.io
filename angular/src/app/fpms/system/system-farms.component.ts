import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Farms" subtitle="Registered farms and their accounts. System Administration."></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <div class="card">
                    <div class="card-header"><h3 class="card-title"><i class="fas fa-warehouse" aria-hidden="true"></i>Farm registration</h3></div>
                    <div class="card-body d-flex align-items-center flex-wrap">
                        @if (registration() !== null) {
                            <span class="mr-3">
                                Registration is <fpms-badge [text]="registration() ? 'Open' : 'Closed'" [tone]="registration() ? 'success' : 'secondary'"></fpms-badge>
                            </span>
                            <button class="btn btn-sm" [class.btn-outline-danger]="registration()" [class.btn-outline-success]="!registration()" [disabled]="busy()" (click)="toggleRegistration()">
                                {{ registration() ? 'Close registration' : 'Open registration' }}
                            </button>
                            <span class="small text-muted ml-3">Open it only while onboarding a farm; the sign-in page shows the link when open.</span>
                        }
                    </div>
                </div>
                <div class="card">
                    <div class="card-body p-0 table-responsive">
                        @if (!farms()) {
                            <fpms-loading></fpms-loading>
                        } @else {
                            <table class="table table-hover mb-0">
                                <thead><tr><th>Farm code</th><th>Name</th><th>Registered</th><th class="text-right">User accounts</th><th>Status</th><th><span class="sr-only">Actions</span></th></tr></thead>
                                <tbody>
                                    @for (x of farms(); track x.tenantId) {
                                        <tr [class.text-muted]="!x.isActive">
                                            <td><code>{{ x.farmCode }}</code></td>
                                            <td>{{ x.name }}</td>
                                            <td>{{ x.creationTime | farmDate }}</td>
                                            <td class="text-right">{{ x.userCount }}</td>
                                            <td><fpms-badge [text]="x.isActive ? 'Active' : 'Inactive'" [tone]="x.isActive ? 'success' : 'secondary'"></fpms-badge></td>
                                            <td class="text-right text-nowrap">
                                                <a class="btn btn-sm btn-outline-primary mr-1" routerLink="/app/system/recovery" [queryParams]="{ tenantId: x.tenantId }">Account recovery</a>
                                                <button class="btn btn-sm" [class.btn-outline-danger]="x.isActive" [class.btn-outline-success]="!x.isActive" (click)="toggleFarm(x)">
                                                    {{ x.isActive ? 'Deactivate' : 'Activate' }}
                                                </button>
                                            </td>
                                        </tr>
                                    } @empty {
                                        <tr><td colspan="6" class="text-center text-muted py-3">No farms registered.</td></tr>
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
export class SystemFarmsComponent extends FpmsPage implements OnInit {
    farms = signal<any[] | null>(null);
    registration = signal<boolean | null>(null);

    ngOnInit(): void {
        this.load();
    }

    async load(): Promise<void> {
        const [farms, reg] = await Promise.all([
            this.api.get<any[]>('SystemFarm', 'GetFarms'),
            this.api.get<any>('SystemFarm', 'GetRegistrationSetting'),
        ]);
        this.farms.set(farms);
        this.registration.set(!!reg?.isEnabled);
    }

    async toggleRegistration(): Promise<void> {
        if (await this.act(() => this.api.post('SystemFarm', 'SetRegistrationSetting', { isEnabled: !this.registration() }))) {
            this.load();
        }
    }

    async toggleFarm(x: any): Promise<void> {
        const message = x.isActive ? `Users of ${x.name} will not be able to sign in until the farm is activated again. No data is deleted.` : `Users of ${x.name} can sign in again.`;
        if (!(await this.confirm(message, x.isActive ? 'Deactivate farm?' : 'Activate farm?'))) {
            return;
        }
        if (await this.act(() => this.api.post('SystemFarm', 'SetFarmActive', { tenantId: x.tenantId, isActive: !x.isActive }))) {
            this.load();
        }
    }
}
