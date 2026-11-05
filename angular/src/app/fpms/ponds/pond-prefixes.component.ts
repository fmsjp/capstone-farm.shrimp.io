import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Pond name prefixes" subtitle="Pond names are a prefix plus a number; codes use the short code." backLink="/app/ponds" backLabel="Ponds"></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <div class="row">
                    <div class="col-xl-8">
                        <div class="card">
                            <div class="card-body p-0 table-responsive">
                                @if (!prefixes()) {
                                    <fpms-loading></fpms-loading>
                                } @else {
                                    <table class="table mb-0">
                                        <thead>
                                            <tr><th>Name</th><th>Short code</th><th class="text-right">Order</th><th class="text-right">Ponds</th><th>Example</th><th>Active</th><th><span class="sr-only">Actions</span></th></tr>
                                        </thead>
                                        <tbody>
                                            @for (x of prefixes(); track x.id) {
                                                <tr [class.text-muted]="!x.isActive">
                                                    <td>
                                                        @if (canManage) {
                                                            <input class="form-control form-control-sm" [(ngModel)]="x.name" maxlength="60" [attr.aria-label]="'Name of prefix ' + x.shortCode" />
                                                        } @else {
                                                            {{ x.name }}
                                                        }
                                                    </td>
                                                    <td><code>{{ x.shortCode }}</code></td>
                                                    <td class="text-right" style="width: 6rem">
                                                        @if (canManage) {
                                                            <input type="number" min="0" max="999" class="form-control form-control-sm text-right" [(ngModel)]="x.sortOrder" aria-label="Order" />
                                                        } @else {
                                                            {{ x.sortOrder }}
                                                        }
                                                    </td>
                                                    <td class="text-right">{{ x.pondCount }}</td>
                                                    <td class="small text-nowrap">{{ example(x.shortCode, x.nextNumber) }} · {{ x.name }} {{ x.nextNumber }}</td>
                                                    <td>
                                                        <input type="checkbox" [(ngModel)]="x.isActive" [disabled]="!canManage" [attr.aria-label]="'Active: ' + x.name" />
                                                    </td>
                                                    <td class="text-right">
                                                        @if (canManage) {
                                                            <button class="btn btn-sm btn-outline-primary" [disabled]="busy() || !x.name?.trim()" (click)="update(x)">Save</button>
                                                        }
                                                    </td>
                                                </tr>
                                            }
                                        </tbody>
                                    </table>
                                }
                            </div>
                        </div>
                        <p class="small text-muted">
                            Renaming a prefix changes how pond names read ("Grow-out Pond 3"), but pond codes ("GP-03") never change. An inactive prefix is hidden when adding a pond; existing ponds keep it.
                        </p>
                    </div>
                    @if (canManage) {
                        <div class="col-xl-4">
                            <div class="card">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-tag" aria-hidden="true"></i>Add a prefix</h3></div>
                                <form class="card-body" #f="ngForm" (ngSubmit)="create()" novalidate>
                                    <div class="form-group">
                                        <label for="pn">Name <span class="text-danger">*</span></label>
                                        <input id="pn" class="form-control" name="name" [(ngModel)]="draft.name" required maxlength="60" placeholder="e.g. Settling Pond" />
                                    </div>
                                    <div class="form-group">
                                        <label for="sc">Short code</label>
                                        <input id="sc" class="form-control text-uppercase" name="shortCode" [(ngModel)]="draft.shortCode" maxlength="6" pattern="[A-Za-z]{1,6}" [placeholder]="suggested || 'e.g. SP'" />
                                        <small class="form-text text-muted">1–6 letters, used in pond codes. Left empty, the initials are used{{ suggested ? ' (' + suggested + ')' : '' }}. It cannot be changed later.</small>
                                    </div>
                                    <button type="submit" class="btn btn-primary" [disabled]="busy() || !f.form.valid">Add prefix</button>
                                    <fpms-form-hint [form]="f"></fpms-form-hint>
                                </form>
                            </div>
                        </div>
                    }
                </div>
            </div>
        </section>
    `,
})
export class PondPrefixesComponent extends FpmsPage implements OnInit {
    prefixes = signal<any[] | null>(null);
    draft: any = { name: '', shortCode: '' };

    get canManage(): boolean {
        return this.can('Farm.Ponds.Manage');
    }

    get suggested(): string {
        return (this.draft.name ?? '')
            .split(/\s+/)
            .map((w: string) => (w.match(/[A-Za-z]/) ?? [''])[0])
            .join('')
            .toUpperCase()
            .slice(0, 6);
    }

    example(shortCode: string, n: number): string {
        return `${shortCode}-${String(n).padStart(2, '0')}`;
    }

    ngOnInit(): void {
        this.load();
    }

    async load(): Promise<void> {
        this.prefixes.set(await this.api.get<any[]>('Pond', 'GetPrefixes'));
    }

    async create(): Promise<void> {
        if (await this.act(() => this.api.post('Pond', 'CreatePrefix', { name: this.draft.name, shortCode: this.draft.shortCode || null }))) {
            this.notifySaved('Prefix added.');
            this.draft = { name: '', shortCode: '' };
            await this.load();
        }
    }

    async update(x: any): Promise<void> {
        if (await this.act(() => this.api.put('Pond', 'UpdatePrefix', x))) {
            this.notifySaved('Prefix saved.');
            await this.load();
        }
    }
}
