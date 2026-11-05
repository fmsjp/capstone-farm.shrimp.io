import { Injectable, inject } from '@angular/core';
import { NavigationEnd, NavigationStart, Router } from '@angular/router';

@Injectable({ providedIn: 'root' })
export class NavHistory {
    private router = inject(Router);
    private trail: string[] = [this.router.url];
    private replacing = false;
    private popped = false;

    constructor() {
        this.router.events.subscribe((e) => {
            if (e instanceof NavigationStart) {
                this.replacing = !!this.router.getCurrentNavigation()?.extras.replaceUrl;
                this.popped = e.navigationTrigger === 'popstate';
            } else if (e instanceof NavigationEnd) {
                this.visit(e.urlAfterRedirects);
            }
        });
    }

    private visit(url: string): void {
        const path = NavHistory.path(url);
        const last = this.trail.length - 1;
        if (path === NavHistory.path(this.trail[last])) {
            this.trail[last] = url;
            return;
        }
        if (this.replacing) {
            this.trail[last] = url;
            return;
        }
        const at = this.trail.map(NavHistory.path).lastIndexOf(path);
        if (at >= 0) {
            this.trail = this.trail.slice(0, at);
        } else if (this.popped) {
            this.trail = [];
        }
        this.trail.push(url);
        if (this.trail.length > 20) {
            this.trail = this.trail.slice(-20);
        }
    }

    previous(): string | null {
        for (let i = this.trail.length - 2; i >= 0; i--) {
            const p = this.trail[i];
            const path = NavHistory.path(p);
            if (/\/(new|edit|create-batch)$/.test(path)) {
                continue;
            }
            return p.startsWith('/app/') ? p : null;
        }
        return null;
    }

    private static path(url: string): string {
        return url.split(/[?#]/)[0];
    }
}
