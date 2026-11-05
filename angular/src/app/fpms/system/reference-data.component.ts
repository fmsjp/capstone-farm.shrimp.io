import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Reference data" subtitle="Water-quality parameters and their national-standard defaults (GAqP, PNS/BAFS 197:2017). System Administration."></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <p class="small text-muted">Readings outside the optimal range are saved and flagged. Readings outside the physical limits are rejected as entry errors. Changes apply to new readings only.</p>
                @if (edit) {
                    <div class="fpms-dialog-backdrop"><div class="fpms-dialog fpms-dialog-sm" role="dialog" aria-modal="true"><div class="fpms-dialog-body p-0">
                        <div class="card card-outline card-primary">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-sliders-h" aria-hidden="true"></i>Edit {{ edit.code }}</h3></div>
                            <div class="card-body">
                                <div class="form-row">
                                    <div class="form-group col-md-4">
                                        <label for="n">Name</label>
                                        <input id="n" class="form-control" [(ngModel)]="edit.name" maxlength="100" />
                                    </div>
                                    <div class="form-group col-md-2">
                                        <label for="u">Unit</label>
                                        <input id="u" class="form-control" [(ngModel)]="edit.unitCode" maxlength="30" />
                                    </div>
                                    <div class="form-group col-md-3">
                                        <label for="pmin">Physical min</label>
                                        <input id="pmin" type="number" step="any" class="form-control" [(ngModel)]="edit.physicalMin" />
                                    </div>
                                    <div class="form-group col-md-3">
                                        <label for="pmax">Physical max</label>
                                        <input id="pmax" type="number" step="any" class="form-control" [(ngModel)]="edit.physicalMax" />
                                    </div>
                                </div>
                                <div class="form-row">
                                    <div class="form-group col-md-3">
                                        <label for="omin">Optimal min</label>
                                        <input id="omin" type="number" step="any" class="form-control" [(ngModel)]="edit.optimalMin" />
                                        <div class="custom-control custom-checkbox">
                                            <input type="checkbox" class="custom-control-input" id="omine" [(ngModel)]="edit.optimalMinExclusive" />
                                            <label class="custom-control-label small" for="omine">Exclusive (&gt;)</label>
                                        </div>
                                    </div>
                                    <div class="form-group col-md-3">
                                        <label for="omax">Optimal max</label>
                                        <input id="omax" type="number" step="any" class="form-control" [(ngModel)]="edit.optimalMax" />
                                        <div class="custom-control custom-checkbox">
                                            <input type="checkbox" class="custom-control-input" id="omaxe" [(ngModel)]="edit.optimalMaxExclusive" />
                                            <label class="custom-control-label small" for="omaxe">Exclusive (&lt;)</label>
                                        </div>
                                    </div>
                                    <div class="form-group col-md-4">
                                        <label for="g">Monitoring frequency (guidance)</label>
                                        <input id="g" class="form-control" [(ngModel)]="edit.guidanceFrequency" maxlength="100" />
                                    </div>
                                    <div class="form-group col-md-2 d-flex align-items-end">
                                        <div class="custom-control custom-checkbox mb-2">
                                            <input type="checkbox" class="custom-control-input" id="act" [(ngModel)]="edit.isActive" />
                                            <label class="custom-control-label" for="act">Active</label>
                                        </div>
                                    </div>
                                </div>
                                <div class="form-row">
                                    <div class="form-group col-md-6">
                                        <label for="rlow">Recommendation when below the range</label>
                                        <textarea id="rlow" rows="3" class="form-control" [(ngModel)]="edit.lowRecommendation" maxlength="500"></textarea>
                                    </div>
                                    <div class="form-group col-md-6">
                                        <label for="rhigh">Recommendation when above the range</label>
                                        <textarea id="rhigh" rows="3" class="form-control" [(ngModel)]="edit.highRecommendation" maxlength="500"></textarea>
                                    </div>
                                </div>
                                <p class="small text-muted mb-0">Leave an optimal bound empty for no limit on that side. Recommendations are shown to farm staff with the out-of-range warning.</p>
                            </div>
                            <div class="card-footer">
                                <button class="btn btn-primary mr-2" [disabled]="busy()" (click)="save()">Save</button>
                                <button class="btn btn-default" (click)="edit = null">Cancel</button>
                            </div>
                        </div>
                    </div></div></div>
                }
                <div class="card">
                    <div class="card-body p-0 table-responsive">
                        @if (!list()) {
                            <fpms-loading></fpms-loading>
                        } @else {
                            <table class="table table-hover mb-0">
                                <thead><tr><th>Code</th><th>Parameter</th><th>Unit</th><th>Optimal range</th><th>Physical limits</th><th>Frequency</th><th>At stocking</th><th>Status</th><th><span class="sr-only">Actions</span></th></tr></thead>
                                <tbody>
                                    @for (p of list(); track p.code) {
                                        <tr [class.text-muted]="!p.isActive">
                                            <td><code>{{ p.code }}</code></td>
                                            <td>{{ p.name }}</td>
                                            <td>{{ p.unitCode }}</td>
                                            <td>{{ p.optimalRange || 'recorded without a range' }}</td>
                                            <td>{{ p.physicalMin }} – {{ p.physicalMax }}</td>
                                            <td>{{ p.guidanceFrequency }}</td>
                                            <td>{{ p.requiredAtStocking ? 'Required' : '' }}</td>
                                            <td><fpms-badge [text]="p.isActive ? 'Active' : 'Inactive'" [tone]="p.isActive ? 'success' : 'secondary'"></fpms-badge></td>
                                            <td class="text-right"><button class="btn btn-sm btn-outline-primary" (click)="startEdit(p)">Edit</button></td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        }
                    </div>
                </div>
            </div>
        </section>
    `,
})
export class ReferenceDataComponent extends FpmsPage implements OnInit {
    list = signal<any[] | null>(null);
    edit: any = null;

    ngOnInit(): void {
        this.load();
    }

    async load(): Promise<void> {
        this.list.set(await this.api.get<any[]>('SystemReferenceData', 'GetParameters'));
    }

    startEdit(p: any): void {
        this.edit = { ...p };
    }

    async save(): Promise<void> {
        const e = this.edit;
        const body = {
            ...e,
            optimalMin: e.optimalMin === '' || e.optimalMin === undefined ? null : e.optimalMin,
            optimalMax: e.optimalMax === '' || e.optimalMax === undefined ? null : e.optimalMax,
        };
        if (await this.act(() => this.api.put('SystemReferenceData', 'UpdateParameter', body))) {
            this.notifySaved('Parameter saved.');
            this.edit = null;
            this.load();
        }
    }
}
