import { Component, ChangeDetectionStrategy, Input } from '@angular/core';

@Component({
    selector: 'fpms-logo',
    templateUrl: './fpms-logo.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
})
export class FpmsLogoComponent {
    @Input() variant: 'mark' | 'wordmark' = 'mark';
    @Input() alt = '';
}
