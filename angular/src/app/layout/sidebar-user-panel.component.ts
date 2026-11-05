import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { AppSessionService } from '@shared/session/app-session.service';

@Component({
    selector: 'sidebar-user-panel',
    templateUrl: './sidebar-user-panel.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
})
export class SidebarUserPanelComponent {
    private session = inject(AppSessionService);
    readonly name = `${this.session.user?.name ?? ''} ${this.session.user?.surname ?? ''}`.trim() || (this.session.user?.userName ?? '');
    readonly role = this.session.user?.roleDisplayName ?? '';
    readonly initials = (
        `${(this.session.user?.name ?? '').charAt(0)}${(this.session.user?.surname ?? '').charAt(0)}` || (this.session.user?.userName ?? '').charAt(0)
    ).toUpperCase();
}
