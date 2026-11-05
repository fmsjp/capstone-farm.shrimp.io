import { Component, ChangeDetectionStrategy, OnInit, OnDestroy, Renderer2 } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FpmsLogoComponent } from '@shared/fpms/fpms-logo.component';

@Component({
    selector: 'fpms-welcome',
    templateUrl: './welcome.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [RouterLink, FpmsLogoComponent],
})
export class WelcomeComponent implements OnInit, OnDestroy {
    constructor(private renderer: Renderer2) {}

    ngOnInit(): void {
        this.renderer.removeClass(document.body, 'login-page');
        this.renderer.removeClass(document.body, 'fpms-account');
        this.renderer.addClass(document.body, 'fpms-welcome-page');
    }

    ngOnDestroy(): void {
        this.renderer.removeClass(document.body, 'fpms-welcome-page');
    }
}
