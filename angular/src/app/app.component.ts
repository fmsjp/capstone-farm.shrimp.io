import { Component, Injector, OnInit, Renderer2 } from '@angular/core';
import { AppComponentBase } from '@shared/app-component-base';
import { SignalRAspNetCoreHelper } from '@shared/helpers/SignalRAspNetCoreHelper';
import { LayoutStoreService } from '@shared/layout/layout-store.service';
import { HeaderComponent } from './layout/header.component';
import { SidebarComponent } from './layout/sidebar.component';
import { RouterOutlet } from '@angular/router';
import { FooterComponent } from './layout/footer.component';
import { HealthBannerComponent } from './layout/health-banner.component';
import { FarmTime } from '@shared/fpms/farm-time';
import { BRAND } from '@shared/fpms/brand';
import { ProgressBarComponent } from './layout/progress-bar.component';
import { SectionTabsComponent } from './layout/section-tabs.component';
import { LogDrawerComponent } from './layout/log-drawer.component';

@Component({
    templateUrl: './app.component.html',
    standalone: true,
    imports: [HeaderComponent, SidebarComponent, RouterOutlet, FooterComponent, HealthBannerComponent, ProgressBarComponent, SectionTabsComponent, LogDrawerComponent],
})
export class AppComponent extends AppComponentBase implements OnInit {
    sidebarExpanded: boolean;

    constructor(
        injector: Injector,
        private renderer: Renderer2,
        private _layoutStore: LayoutStoreService
    ) {
        super(injector);
    }

    ngOnInit(): void {
        this.renderer.addClass(document.body, 'sidebar-mini');
        this.renderer.addClass(document.body, 'layout-fixed');
        this.renderer.addClass(document.body, 'layout-navbar-fixed');
        this.renderer.removeClass(document.body, 'login-page');
        this.renderer.removeClass(document.body, 'fpms-account');
        FarmTime.setZone(this.appSession.tenant?.timeZone);
        FarmTime.setDisplay(this.appSession.tenant?.dateOrder, this.appSession.tenant?.use24HourTime);

        SignalRAspNetCoreHelper.initSignalR();

        abp.event.on('abp.notifications.received', (userNotification) => {
            abp.notifications.showUiNotifyForUserNotification(userNotification);

            Push.create(BRAND.name, {
                body: userNotification.notification.data.message,
                icon: abp.appPath + 'assets/img/brand/icon-192.png',
                timeout: 6000,
                onClick: function () {
                    window.focus();
                    this.close();
                },
            });
        });

        this._layoutStore.sidebarExpanded.subscribe((value) => {
            this.sidebarExpanded = value;
        });
    }

    toggleSidebar(): void {
        this._layoutStore.setSidebarExpanded(!this.sidebarExpanded);
    }
}
