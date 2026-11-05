import { Component, OnInit, ViewEncapsulation, Renderer2 } from '@angular/core';
import { AccountHeaderComponent } from './layout/account-header.component';
import { RouterOutlet } from '@angular/router';
import { AccountFooterComponent } from './layout/account-footer.component';

@Component({
    templateUrl: './account.component.html',
    encapsulation: ViewEncapsulation.None,
    standalone: true,
    imports: [AccountHeaderComponent, RouterOutlet, AccountFooterComponent],
})
export class AccountComponent implements OnInit {
    constructor(private renderer: Renderer2) {}

    ngOnInit(): void {
        this.renderer.addClass(document.body, 'login-page');
        this.renderer.addClass(document.body, 'fpms-account');
    }
}
