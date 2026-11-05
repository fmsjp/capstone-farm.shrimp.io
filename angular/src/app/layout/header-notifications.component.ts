import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { BsDropdownDirective, BsDropdownToggleDirective, BsDropdownMenuDirective } from 'ngx-bootstrap/dropdown';
import { FpmsApiService } from '@shared/fpms/api.service';
import { FarmDateTimePipe } from '@shared/fpms/pipes';
import { PermissionCheckerService } from 'abp-ng2-module';

interface NotificationDto {
    id: string;
    type: number;
    message: string;
    createdAtUtc: string;
    isRead: boolean;
}

@Component({
    selector: 'header-notifications',
    standalone: true,
    imports: [BsDropdownDirective, BsDropdownToggleDirective, BsDropdownMenuDirective, FarmDateTimePipe],
    template: `
        @if (enabled) {
            <li class="nav-item dropdown" dropdown (onShown)="load()">
                <a class="nav-link" href="javascript:;" dropdownToggle aria-label="Notifications">
                    <i class="far fa-bell"></i>
                    @if (unread() > 0) {
                        <span class="badge badge-danger navbar-badge">{{ unread() }}</span>
                    }
                </a>
                <div class="dropdown-menu dropdown-menu-lg dropdown-menu-right fpms-notifications" *dropdownMenu>
                    <span class="dropdown-item dropdown-header">Notifications</span>
                    <div class="dropdown-divider"></div>
                    @for (n of items(); track n.id) {
                        <div class="dropdown-item small" [class.font-weight-bold]="!n.isRead">
                            <div class="text-wrap">{{ n.message }}</div>
                            <div class="text-muted">{{ n.createdAtUtc | farmDateTime }}</div>
                        </div>
                        <div class="dropdown-divider"></div>
                    } @empty {
                        <span class="dropdown-item small text-muted">No notifications.</span>
                    }
                    @if (unread() > 0) {
                        <a href="javascript:;" class="dropdown-item dropdown-footer" (click)="markAll()">Mark all as read</a>
                    }
                </div>
            </li>
        }
    `,
})
export class HeaderNotificationsComponent implements OnInit, OnDestroy {
    private api = inject(FpmsApiService);
    readonly enabled = inject(PermissionCheckerService).isGranted('Farm.Dashboard');
    items = signal<NotificationDto[]>([]);
    unread = signal(0);
    private timer: any;

    ngOnInit(): void {
        if (!this.enabled) {
            return;
        }
        this.refreshCount();
        this.timer = setInterval(() => this.refreshCount(), 120000);
    }

    ngOnDestroy(): void {
        clearInterval(this.timer);
    }

    async refreshCount(): Promise<void> {
        try {
            this.unread.set((await this.api.get<number>('Notification', 'GetUnreadCount')) ?? 0);
        } catch {
        }
    }

    async load(): Promise<void> {
        this.items.set((await this.api.get<NotificationDto[]>('Notification', 'GetMine', { maxResultCount: 20 })) ?? []);
    }

    async markAll(): Promise<void> {
        await this.api.post('Notification', 'MarkAllRead');
        this.unread.set(0);
        await this.load();
    }
}
