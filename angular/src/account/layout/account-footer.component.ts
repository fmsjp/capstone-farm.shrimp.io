import { Component, ChangeDetectionStrategy } from '@angular/core';

@Component({
    selector: 'account-footer',
    templateUrl: './account-footer.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
})
export class AccountFooterComponent {}
