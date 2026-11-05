import { Component, Input, Output, EventEmitter } from '@angular/core';
import { FormsModule } from '@angular/forms';

export class PagerState {
    page = 1;
    size = 25;
    sort = '';
    total = 0;

    private readonly defaultSort: string;

    constructor(defaultSort = '') {
        this.defaultSort = defaultSort;
        this.sort = defaultSort;
    }

    fromQuery(get: (name: string) => string | null): void {
        const page = Number(get('page'));
        const size = Number(get('size'));
        if (Number.isInteger(page) && page >= 1) {
            this.page = page;
        }
        if (size === 25 || size === 50 || size === 100) {
            this.size = size;
        }
        const sort = get('sort');
        if (sort && /^[A-Za-z][A-Za-z0-9.]*( (asc|desc))?$/.test(sort)) {
            this.sort = sort;
        }
    }

    toQuery(): { page: number | null; size: number | null; sort: string | null } {
        return {
            page: this.page > 1 ? this.page : null,
            size: this.size !== 25 ? this.size : null,
            sort: this.sort !== this.defaultSort ? this.sort : null,
        };
    }

    get pages(): number {
        return Math.max(1, Math.ceil(this.total / this.size));
    }

    get from(): number {
        return this.total === 0 ? 0 : (this.page - 1) * this.size + 1;
    }

    get to(): number {
        return Math.min(this.total, this.page * this.size);
    }

    get params(): { skipCount: number; maxResultCount: number; sorting?: string } {
        return { skipCount: (this.page - 1) * this.size, maxResultCount: this.size, sorting: this.sort || undefined };
    }

    update(total: number): boolean {
        this.total = total;
        if (this.page > this.pages) {
            this.page = this.pages;
            return true;
        }
        return false;
    }

    reset(): void {
        this.page = 1;
    }

    direction(key: string): 'asc' | 'desc' | '' {
        const [k, d] = this.sort.split(' ');
        return k === key ? (d === 'desc' ? 'desc' : 'asc') : '';
    }

    toggle(key: string): void {
        this.sort = this.direction(key) === 'asc' ? `${key} desc` : `${key} asc`;
        this.page = 1;
    }
}

@Component({
    selector: 'fpms-pager',
    standalone: true,
    template: `
        @if (state.total > 0) {
            <div class="d-flex flex-wrap align-items-center justify-content-between px-3 py-2 border-top no-print fpms-pager">
                <div class="text-muted small mb-1" role="status">{{ state.from }}–{{ state.to }} of {{ state.total }}</div>
                <div class="d-flex align-items-center mb-1">
                    <label class="small text-muted mb-0 mr-1" [attr.for]="'ps-' + id">Per page</label>
                    <select class="form-control form-control-sm mr-3" style="width:auto" [attr.id]="'ps-' + id" [ngModel]="state.size" (ngModelChange)="size($event)">
                        <option [ngValue]="25">25</option>
                        <option [ngValue]="50">50</option>
                        <option [ngValue]="100">100</option>
                    </select>
                    <button type="button" class="btn btn-sm btn-outline-secondary mr-1" [disabled]="state.page <= 1" (click)="go(state.page - 1)" aria-label="Previous page">
                        <i class="fas fa-chevron-left"></i>
                    </button>
                    <span class="small mx-1">Page {{ state.page }} of {{ state.pages }}</span>
                    <button type="button" class="btn btn-sm btn-outline-secondary ml-1" [disabled]="state.page >= state.pages" (click)="go(state.page + 1)" aria-label="Next page">
                        <i class="fas fa-chevron-right"></i>
                    </button>
                </div>
            </div>
        }
    `,
    imports: [FormsModule],
})
export class PagerComponent {
    private static next = 0;
    readonly id = PagerComponent.next++;
    @Input({ required: true }) state!: PagerState;
    @Output() changed = new EventEmitter<void>();

    go(page: number): void {
        this.state.page = page;
        this.changed.emit();
    }

    size(n: number): void {
        this.state.size = Number(n);
        this.state.page = 1;
        this.changed.emit();
    }
}

@Component({
    selector: 'th[fpmsSort]',
    standalone: true,
    host: {
        '[attr.aria-sort]': 'aria()',
        '[style.cursor]': '"pointer"',
        '(click)': 'toggle()',
        '(keydown.enter)': 'toggle()',
        tabindex: '0',
        class: 'text-nowrap user-select-none',
    },
    template: `<ng-content></ng-content><span class="fpms-sortmark" [attr.data-dir]="dir() || 'none'" aria-hidden="true"></span>`,
    styles: [
        `
            .fpms-sortmark {
                display: inline-block;
                position: relative;
                width: 0.7rem;
                height: 1rem;
                margin-left: 0.4rem;
                vertical-align: -0.2rem;
            }
            .fpms-sortmark::before,
            .fpms-sortmark::after {
                content: '';
                position: absolute;
                left: 50%;
                transform: translateX(-50%);
                border-left: 0.3rem solid transparent;
                border-right: 0.3rem solid transparent;
                opacity: 0.3;
            }
            .fpms-sortmark::before {
                top: 0.1rem;
                border-bottom: 0.36rem solid currentColor;
            }
            .fpms-sortmark::after {
                bottom: 0.1rem;
                border-top: 0.36rem solid currentColor;
            }
            .fpms-sortmark[data-dir='asc']::before,
            .fpms-sortmark[data-dir='desc']::after {
                opacity: 1;
            }
        `,
    ],
})
export class SortHeaderComponent {
    @Input({ required: true }) fpmsSort!: string;
    @Input({ required: true }) pager!: PagerState;
    @Output() sorted = new EventEmitter<void>();

    dir(): string {
        return this.pager.direction(this.fpmsSort);
    }

    aria(): string {
        const d = this.dir();
        return d === 'asc' ? 'ascending' : d === 'desc' ? 'descending' : 'none';
    }

    toggle(): void {
        this.pager.toggle(this.fpmsSort);
        this.sorted.emit();
    }
}
