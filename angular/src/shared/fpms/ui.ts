import { PagerComponent, SortHeaderComponent } from './pager';
export { PagerComponent, SortHeaderComponent, PagerState } from './pager';
import { Component, ElementRef, EventEmitter, HostListener, Input, OnDestroy, Output, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule, NgForm } from '@angular/forms';
import { NavHistory } from './nav-history';
import { toneFor } from './labels';

@Component({
    selector: 'fpms-badge',
    standalone: true,
    template: `
        <span class="badge fpms-badge badge-{{ tone || toneOf(text) }}">{{ text }}</span>
    `,
})
export class StatusBadgeComponent {
    @Input() text = '';
    @Input() tone?: string;

    toneOf(label: string): string {
        return toneFor(label);
    }
}

@Component({
    selector: 'fpms-page-header',
    standalone: true,
    imports: [RouterLink],
    template: `
        <section class="content-header fpms-page-header no-print">
            <div class="container-fluid" [class.fpms-narrow]="narrow">
                <div class="d-flex flex-wrap align-items-center justify-content-between">
                    <div class="mb-2">
                        @if (backLink) {
                            @if (previous(); as prev) {
                                <a
                                    [href]="prev"
                                    (click)="goBack($event, prev)"
                                    class="text-muted small d-inline-block mb-1">
                                    <i class="fas fa-arrow-left mr-1"></i>
                                    {{ isParent(prev) ? backLabel : 'Back' }}
                                </a>
                            } @else {
                                <a [routerLink]="backLink" class="text-muted small d-inline-block mb-1">
                                    <i class="fas fa-arrow-left mr-1"></i>
                                    {{ backLabel }}
                                </a>
                            }
                        }
                        <h1 class="m-0">{{ title }}</h1>
                        @if (subtitle) {
                            <div class="text-muted">{{ subtitle }}</div>
                        }
                    </div>
                    <div class="mb-2 fpms-actions"><ng-content></ng-content></div>
                </div>
            </div>
        </section>
    `,
})
export class PageHeaderComponent {
    @Input() title = '';
    @Input() subtitle?: string;
    @Input() backLink?: string | any[];
    @Input() backLabel = 'Back';
    @Input() narrow = false;

    private history = inject(NavHistory);
    private router = inject(Router);

    parentUrl(): string {
        return this.router.serializeUrl(
            this.router.createUrlTree(Array.isArray(this.backLink) ? this.backLink : [this.backLink ?? ''])
        );
    }

    isParent(url: string): boolean {
        return url.split(/[?#]/)[0] === this.parentUrl();
    }

    previous(): string | null {
        const prev = this.history.previous();
        return prev && prev.split(/[?#]/)[0] === '/app/dashboard' ? null : prev;
    }

    goBack(event: Event, url: string): void {
        event.preventDefault();
        this.router.navigateByUrl(url);
    }
}

@Component({
    selector: 'fpms-empty',
    standalone: true,
    template: `
        <div class="fpms-empty text-center text-muted py-4">
            <i class="{{ icon }} fa-2x mb-2 d-block" aria-hidden="true"></i>
            <div class="mb-2">{{ text }}</div>
            <ng-content></ng-content>
        </div>
    `,
})
export class EmptyStateComponent {
    @Input() text = 'Nothing here yet.';
    @Input() icon = 'far fa-folder-open';
}

@Component({
    selector: 'fpms-warnings',
    standalone: true,
    template: `
        @if (warnings?.length) {
            <div class="alert alert-warning fpms-warning" role="status">
                <i class="fas fa-exclamation-triangle mr-1" aria-hidden="true"></i>
                <strong>
                    {{ warnings.length === 1 ? 'Saved with a warning' : 'Saved with ' + warnings.length + ' warnings' }}
                </strong>
                <ul class="mb-0 mt-1 pl-3">
                    @for (w of warnings; track $index) {
                        <li>{{ w.message }}</li>
                    }
                </ul>
            </div>
        }
    `,
})
export class WarningsComponent {
    @Input() warnings: { kind: string; message: string }[] = [];
}

@Component({
    selector: 'fpms-loading',
    standalone: true,
    template: `
        <div class="text-center text-muted py-4 fpms-loading" role="status" aria-live="polite">
            <i class="fas fa-spinner fa-spin mr-2" aria-hidden="true"></i>
            Loading…
        </div>
    `,
})
export class LoadingComponent {}

@Component({
    selector: 'fpms-form-hint',
    standalone: true,
    template: `
        @if (form?.invalid) {
            <span class="fpms-form-hint" role="status">
                <i class="fas fa-info-circle mr-1" aria-hidden="true"></i>
                Some required fields are empty or not valid.
                <button type="button" class="btn btn-link p-0 align-baseline fpms-form-hint-link" (click)="show()">
                    Show them
                </button>
            </span>
        }
    `,
})
export class FormHintComponent {
    @Input({ required: true }) form!: NgForm | null;

    show(): void {
        this.form?.form.markAllAsTouched();
        setTimeout(() =>
            (
                document.querySelector(
                    'form .form-control.ng-invalid, form .custom-select.ng-invalid'
                ) as HTMLElement | null
            )?.focus()
        );
    }
}

@Component({
    selector: 'fpms-more',
    standalone: true,
    template: `
        <details class="fpms-more" #d>
            <summary class="btn btn-outline-secondary">
                {{ label }}
                <i class="fas fa-chevron-down ml-2" aria-hidden="true"></i>
            </summary>
            <div class="fpms-more-menu" role="menu"><ng-content></ng-content></div>
        </details>
    `,
})
export class MoreMenuComponent {
    @Input() label = 'More';
    private host = inject(ElementRef<HTMLElement>);

    private close(): void {
        const d = this.host.nativeElement.querySelector('details');
        if (d?.open) {
            d.open = false;
        }
    }

    @HostListener('document:click', ['$event'])
    onDocumentClick(e: Event): void {
        const target = e.target as HTMLElement;
        if (!this.host.nativeElement.contains(target) || target.closest('.fpms-more-menu .dropdown-item')) {
            this.close();
        }
    }

    @HostListener('keydown.escape')
    onEscape(): void {
        this.close();
        (this.host.nativeElement.querySelector('summary') as HTMLElement | null)?.focus();
    }
}

export interface ToolbarChip {
    value: unknown;
    label: string;
    count: number | null;
}

@Component({
    selector: 'fpms-list-toolbar',
    standalone: true,
    imports: [FormsModule],
    template: `
        <div class="d-flex flex-wrap align-items-center fpms-lt">
            <label class="fpms-lt-search mb-0 mr-3">
                <i class="fas fa-search" aria-hidden="true"></i>
                <input
                    type="text"
                    [attr.aria-label]="placeholder"
                    [placeholder]="placeholder"
                    [ngModel]="search"
                    (ngModelChange)="typed($event)" />
            </label>
            <ng-content></ng-content>
            <span class="small text-muted ml-auto" role="status">{{ resultText }}</span>
        </div>
        @if (chips.length) {
            <div class="mt-2 d-flex flex-wrap align-items-center">
                @if (chips.length <= maxChips) {
                    @for (c of chips; track c.label) {
                        <button
                            type="button"
                            class="fpms-lt-chip"
                            [attr.aria-pressed]="c.value === chip"
                            (click)="chipChange.emit(c.value)">
                            {{ c.label }}
                            @if (c.count !== null) {
                                · {{ fmt(c.count) }}
                            }
                        </button>
                    }
                } @else {
                    <label class="fpms-lt-select mb-0 mr-2">
                        <span class="sr-only">Filter</span>
                        <select [ngModel]="chip" (ngModelChange)="chipChange.emit($event)" aria-label="Filter">
                            @for (c of chips; track c.label) {
                                <option [ngValue]="c.value">
                                    {{ c.label }}{{ c.count !== null ? ' (' + fmt(c.count) + ')' : '' }}
                                </option>
                            }
                        </select>
                    </label>
                    @if (chip !== null) {
                        <button type="button" class="btn btn-link btn-sm p-0" (click)="chipChange.emit(chips[0].value)">
                            Clear filter
                        </button>
                    }
                }
            </div>
        }
    `,
    styles: [
        `
            .fpms-lt-search {
                display: flex;
                align-items: center;
                gap: 0.5rem;
                min-height: 38px;
                padding: 0 0.7rem;
                border: 1px solid var(--fpms-line-strong);
                border-radius: 8px;
                background: var(--fpms-surface);
                flex: 1 1 240px;
                max-width: 340px;
                color: var(--fpms-muted);
            }
            .fpms-lt-search input {
                border: 0;
                outline: 0;
                flex: 1;
                min-width: 0;
                background: transparent;
                color: var(--fpms-text);
            }
            .fpms-lt-select {
                display: inline-flex;
                max-width: 100%;
            }
            .fpms-lt-select select {
                min-height: 38px;
                max-width: 100%;
                padding: 0 2rem 0 0.7rem;
                border: 1px solid var(--fpms-line-strong);
                border-radius: 8px;
                background-color: var(--fpms-surface);
                color: var(--fpms-text);
                font-size: 0.95rem;
            }
            .fpms-lt-chip {
                min-height: 36px;
                padding: 0 0.9rem;
                margin: 0 0.4rem 0.3rem 0;
                border-radius: 18px;
                border: 1px solid var(--fpms-line-strong);
                background: var(--fpms-surface);
                font-size: 0.9rem;
                cursor: pointer;
            }
            .fpms-lt-chip[aria-pressed='true'] {
                background: var(--fpms-primary);
                border-color: var(--fpms-primary);
                color: #fff;
            }
        `,
    ],
})
export class ListToolbarComponent implements OnDestroy {
    @Input() placeholder = 'Search';
    @Input() search = '';
    @Input() resultText = '';
    @Input() chips: ToolbarChip[] = [];
    @Input() chip: unknown = null;

    @Input() maxChips = 5;
    @Output() searchChange = new EventEmitter<string>();
    @Output() chipChange = new EventEmitter<unknown>();

    private timer: ReturnType<typeof setTimeout> | null = null;

    fmt(n: number): string {
        return n.toLocaleString('en-PH');
    }

    typed(value: string): void {
        if (this.timer) {
            clearTimeout(this.timer);
        }
        this.timer = setTimeout(() => this.searchChange.emit((value ?? '').trim()), 300);
    }

    ngOnDestroy(): void {
        if (this.timer) {
            clearTimeout(this.timer);
        }
    }
}

export const FPMS_UI = [
    StatusBadgeComponent,
    PageHeaderComponent,
    EmptyStateComponent,
    WarningsComponent,
    LoadingComponent,
    PagerComponent,
    SortHeaderComponent,
    FormHintComponent,
    MoreMenuComponent,
    ListToolbarComponent,
];
