import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FpmsLogoComponent } from '@shared/fpms/fpms-logo.component';

@Component({
    selector: 'sidebar-logo',
    templateUrl: './sidebar-logo.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [RouterLink, FpmsLogoComponent],
})
export class SidebarLogoComponent {}
