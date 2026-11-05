import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FpmsLogoComponent } from '@shared/fpms/fpms-logo.component';

@Component({
    selector: 'account-header',
    templateUrl: './account-header.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [FpmsLogoComponent, RouterLink],
})
export class AccountHeaderComponent {}
