import { Component, ChangeDetectionStrategy, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter } from 'rxjs/operators';
import { PermissionCheckerService } from 'abp-ng2-module';
import { activeTabRoute, effectiveUrl, pathOf, sectionOfUrl } from '@shared/layout/sections';

@Component({
    selector: 'fpms-section-tabs',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [RouterLink],
    template: `
        @if (tabs().length > 1 && activeRoute()) {
            <nav class="fpms-section-tabs no-print" [attr.aria-label]="label() + ' pages'">
                @for (t of tabs(); track t.route) {
                    <a
                        class="fpms-section-tab"
                        [routerLink]="t.route"
                        [class.active]="t.route === activeRoute()"
                        [attr.aria-current]="t.route === activeRoute() ? 'page' : null">
                        {{ t.label }}
                    </a>
                }
                @if (action(); as a) {
                    <a class="btn btn-primary fpms-section-action" [routerLink]="a.route">
                        <i [class]="a.icon + ' mr-1'" aria-hidden="true"></i>
                        {{ a.label }}
                    </a>
                }
            </nav>
        }
    `,
})
export class SectionTabsComponent {
    private router = inject(Router);
    private permission = inject(PermissionCheckerService);
    private url = signal(pathOf(effectiveUrl(this.router.url)));

    readonly section = computed(() => sectionOfUrl(this.url()));
    readonly label = computed(() => this.section()?.label ?? '');
    readonly tabs = computed(() => {
        const section = this.section();
        return section ? section.tabs.filter((t) => !t.permission || this.permission.isGranted(t.permission)) : [];
    });
    readonly action = computed(() => {
        const a = this.section()?.action;
        const active = this.activeRoute();
        return a && active && a.onTabs.includes(active) && this.permission.isGranted(a.permission) ? a : null;
    });
    readonly activeRoute = computed(() => {
        const section = this.section();
        return section ? activeTabRoute(section, this.url()) : undefined;
    });

    constructor() {
        this.router.events
            .pipe(
                filter((e): e is NavigationEnd => e instanceof NavigationEnd),
                takeUntilDestroyed()
            )
            .subscribe((e) => this.url.set(pathOf(effectiveUrl(e.urlAfterRedirects))));
    }
}
