import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PermissionCheckerService } from 'abp-ng2-module';
import { FpmsApiService } from '@shared/fpms/api.service';

@Component({
    selector: 'fpms-health-banner',
    standalone: true,
    imports: [RouterLink],
    template: `
        @if (banner(); as b) {
            <div class="alert alert-danger mb-0 rounded-0 no-print" role="alert">
                <i class="fas fa-exclamation-triangle mr-2"></i><strong>{{ b.message }}</strong>
                @if (isAdmin) {
                    <a routerLink="/app/system/health" class="alert-link ml-2">Open system health</a>
                    <ul class="mb-0 mt-1 pl-4">
                        @for (d of b.details; track $index) {
                            <li>{{ d }}</li>
                        }
                    </ul>
                }
            </div>
        }
    `,
})
export class HealthBannerComponent implements OnInit, OnDestroy {
    private api = inject(FpmsApiService);
    private permissions = inject(PermissionCheckerService);
    private timer: ReturnType<typeof setInterval> | undefined;

    banner = signal<{ message: string; details: string[] } | null>(null);

    get isAdmin(): boolean {
        return this.permissions.isGranted('System.Health');
    }

    ngOnInit(): void {
        if (!this.isAdmin && !this.permissions.isGranted('Farm.Dashboard.Full')) {
            return;
        }
        this.load();
        this.timer = setInterval(() => this.load(), 5 * 60 * 1000);
    }

    ngOnDestroy(): void {
        clearInterval(this.timer);
    }

    private async load(): Promise<void> {
        try {
            const r = await this.api.get<{ needsAttention: boolean; message: string; details: string[] }>('HealthBanner', 'Get');
            this.banner.set(r?.needsAttention ? { message: r.message, details: r.details ?? [] } : null);
        } catch {
            this.banner.set(null);
        }
    }
}
