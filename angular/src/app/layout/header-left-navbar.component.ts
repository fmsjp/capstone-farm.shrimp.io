import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { LayoutStoreService } from '@shared/layout/layout-store.service';
import { AppSessionService } from '@shared/session/app-session.service';
import { effectiveUrl, sectionOfUrl } from '@shared/layout/sections';

function pageTitleOf(snapshot: ActivatedRouteSnapshot): string {
    let route = snapshot;
    while (route.firstChild) {
        route = route.firstChild;
    }
    return (route.title as string) || '';
}

@Component({
    selector: 'header-left-navbar',
    templateUrl: './header-left-navbar.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
})
export class HeaderLeftNavbarComponent implements OnInit {
    sidebarExpanded: boolean;
    private session = inject(AppSessionService);
    private router = inject(Router);
    readonly isHost = !this.session.tenant;
    readonly farmName = this.session.tenant?.farmName || this.session.tenant?.name;

    private url = signal(effectiveUrl(this.router.url));
    private title = signal(pageTitleOf(this.router.routerState.snapshot.root));

    readonly crumbs = computed(() => {
        const first = this.isHost ? 'System Administration' : this.farmName;
        const section = sectionOfUrl(this.url());
        const title = this.title();
        const out = [first];
        if (section) {
            out.push(section.label);
        }
        if (title && title !== section?.label && title !== first) {
            out.push(title);
        }
        return out.filter(Boolean);
    });

    constructor(private _layoutStore: LayoutStoreService) {
        this.router.events
            .pipe(
                filter((e): e is NavigationEnd => e instanceof NavigationEnd),
                takeUntilDestroyed()
            )
            .subscribe((e) => {
                this.url.set(effectiveUrl(e.urlAfterRedirects));
                this.title.set(pageTitleOf(this.router.routerState.snapshot.root));
            });
    }

    ngOnInit(): void {
        this._layoutStore.sidebarExpanded.subscribe((value) => {
            this.sidebarExpanded = value;
        });
    }

    toggleSidebar(): void {
        this._layoutStore.setSidebarExpanded(!this.sidebarExpanded);
    }
}
