import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { EMPTY_GUID } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Size grades" subtitle="Count grades (pieces per kg) used on harvests and sales." backLink="/app/harvests" backLabel="Harvests"></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <div class="row">
                    <div class="col-xl-8">
                        <div class="card">
                            <div class="card-body p-0 table-responsive">
                                @if (!grades()) {
                                    <fpms-loading></fpms-loading>
                                } @else {
                                    <table class="table mb-0">
                                        <thead>
                                            <tr><th>Label</th><th class="text-right">Min pcs/kg</th><th class="text-right">Max pcs/kg</th><th class="text-right">Order</th><th>Active</th><th></th></tr>
                                        </thead>
                                        <tbody>
                                            @for (g of grades(); track g.id) {
                                                <tr [class.text-muted]="!g.isActive">
                                                    @if (canManage) {
                                                        <td><input class="form-control form-control-sm" [(ngModel)]="g.label" maxlength="20" [attr.aria-label]="'Label ' + g.label" /></td>
                                                        <td><input type="number" min="1" max="1000" class="form-control form-control-sm text-right" [(ngModel)]="g.minCountPerKg" aria-label="Minimum pieces per kg" /></td>
                                                        <td><input type="number" min="1" max="1000" class="form-control form-control-sm text-right" [(ngModel)]="g.maxCountPerKg" aria-label="Maximum pieces per kg" /></td>
                                                        <td style="width: 6rem"><input type="number" min="0" max="999" class="form-control form-control-sm text-right" [(ngModel)]="g.sortOrder" aria-label="Order" /></td>
                                                        <td><input type="checkbox" [(ngModel)]="g.isActive" [attr.aria-label]="'Active: ' + g.label" /></td>
                                                        <td class="text-right"><button class="btn btn-sm btn-outline-primary" [disabled]="busy() || !g.label?.trim()" (click)="save(g)">Save</button></td>
                                                    } @else {
                                                        <td>{{ g.label }}</td>
                                                        <td class="text-right">{{ g.minCountPerKg ?? '—' }}</td>
                                                        <td class="text-right">{{ g.maxCountPerKg ?? '—' }}</td>
                                                        <td class="text-right">{{ g.sortOrder }}</td>
                                                        <td>{{ g.isActive ? 'Yes' : 'No' }}</td>
                                                        <td></td>
                                                    }
                                                </tr>
                                            }
                                        </tbody>
                                    </table>
                                }
                            </div>
                        </div>
                        <p class="small text-muted">Deactivate a grade you no longer use; harvests that already use it keep it.</p>
                    </div>
                    @if (canManage) {
                        <div class="col-xl-4">
                            <div class="card">
                                <div class="card-header"><h3 class="card-title"><i class="fas fa-ruler-horizontal" aria-hidden="true"></i>Add a size grade</h3></div>
                                <form class="card-body" #f="ngForm" (ngSubmit)="save(draft, true)" novalidate>
                                    <div class="form-group">
                                        <label for="gl">Label <span class="text-danger">*</span></label>
                                        <input id="gl" class="form-control" name="label" [(ngModel)]="draft.label" required maxlength="20" placeholder="e.g. 20/30" />
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group col-6">
                                            <label for="gmin">Min pcs/kg</label>
                                            <input id="gmin" type="number" min="1" max="1000" class="form-control" name="min" [(ngModel)]="draft.minCountPerKg" />
                                        </div>
                                        <div class="form-group col-6">
                                            <label for="gmax">Max pcs/kg</label>
                                            <input id="gmax" type="number" min="1" max="1000" class="form-control" name="max" [(ngModel)]="draft.maxCountPerKg" />
                                        </div>
                                    </div>
                                    <button type="submit" class="btn btn-primary" [disabled]="busy() || !f.form.valid">Add grade</button>
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
export class SizeGradesComponent extends FpmsPage implements OnInit {
    grades = signal<any[] | null>(null);
    draft: any = this.blank();

    get canManage(): boolean {
        return this.can('Farm.Harvests.EditAny');
    }

    blank(): any {
        return { id: EMPTY_GUID, label: '', minCountPerKg: null, maxCountPerKg: null, sortOrder: 0, isActive: true };
    }

    ngOnInit(): void {
        this.load();
    }

    async load(): Promise<void> {
        this.grades.set(await this.api.get<any[]>('SizeGrade', 'GetAll'));
    }

    async save(g: any, isNew = false): Promise<void> {
        const body = { ...g, minCountPerKg: g.minCountPerKg || null, maxCountPerKg: g.maxCountPerKg || null };
        if (await this.act(() => this.api.post('SizeGrade', 'Save', body))) {
            this.notifySaved(isNew ? 'Size grade added.' : 'Size grade saved.');
            if (isNew) {
                this.draft = this.blank();
            }
            await this.load();
        }
    }
}
