import { Component, OnInit } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FPMS_IMPORTS } from '../shared-imports';

interface Sheet {
    short: string;
    key: string;
    title: string;
    columns: string[];
    rows: number;
}

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Printable log sheets" subtitle="Print one sheet per pond, fill it in at the pond, and enter it later at a desk. Write the time you actually observed each entry.">
            <button class="btn btn-primary" (click)="print()"><i class="fas fa-print mr-1"></i>Print</button>
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                <div class="card no-print">
                    <div class="card-body">
                        <div class="mb-3" role="group" aria-label="Sheet">
                            <button type="button" class="ls-chip" [attr.aria-pressed]="selected === 'all'" (click)="selected = 'all'">All sheets</button>
                            @for (s of sheets; track s.key) {
                                <button type="button" class="ls-chip" [attr.aria-pressed]="selected === s.key" (click)="selected = s.key">{{ s.short }}</button>
                            }
                        </div>
                        <div class="form-row align-items-end">
                            <div class="form-group col-md-4">
                                <label for="pond">Pond</label>
                                <select id="pond" class="form-control" [(ngModel)]="pond">
                                    <option value="">Blank (write in)</option>
                                    @for (p of ponds; track p.id) {
                                        <option [value]="p.pondCode + (p.currentBatchCode ? ' · ' + p.currentBatchCode : '')">{{ p.pondCode }}{{ p.currentBatchCode ? ' · ' + p.currentBatchCode : '' }}</option>
                                    }
                                </select>
                            </div>
                        </div>
                    </div>
                </div>
                @for (s of visibleSheets(); track s.key) {
                    <div class="fpms-paper">
                        <div class="d-flex justify-content-between align-items-end mb-2">
                            <div>
                                <div class="small text-muted">{{ session.tenant?.name }} · FPMS paper log</div>
                                <h4 class="mb-0">{{ s.title }}</h4>
                            </div>
                            <div class="text-right small">
                                Pond / batch: <span class="fpms-blank">{{ pond }}</span><br />
                                Date: <span class="fpms-blank"></span> &nbsp; Sheet no.: <span class="fpms-blank short"></span>
                            </div>
                        </div>
                        @if (s.key === 'wq') {
                            <p class="small mb-1">Optimal ranges (GAqP): @for (p of params; track p.code) { <span class="mr-2"><b>{{ p.name }}</b> {{ p.optimalRange }} ({{ p.guidanceFrequency }})</span> }</p>
                        }
                        <div class="table-responsive">
                        <table class="table table-bordered table-sm fpms-paper-table">
                            <thead>
                                <tr>
                                    @for (c of s.columns; track c) {
                                        <th>{{ c }}</th>
                                    }
                                </tr>
                            </thead>
                            <tbody>
                                @for (r of rowsOf(s); track r) {
                                    <tr>
                                        @for (c of s.columns; track c) {
                                            <td>&nbsp;</td>
                                        }
                                    </tr>
                                }
                            </tbody>
                        </table>
                        </div>
                        <div class="small d-flex justify-content-between">
                            <span>Recorded by: <span class="fpms-blank"></span></span>
                            <span>Entered into FPMS on: <span class="fpms-blank"></span> by <span class="fpms-blank"></span></span>
                        </div>
                    </div>
                }
            </div>
        </section>
    `,
    styles: [
        `
            .ls-chip {
                min-height: 36px;
                padding: 0 0.9rem;
                margin: 0 0.4rem 0.4rem 0;
                border-radius: 18px;
                border: 1px solid var(--fpms-line-strong);
                background: var(--fpms-surface);
                font-size: 0.9rem;
                cursor: pointer;
            }
            .ls-chip[aria-pressed='true'] {
                background: var(--fpms-primary);
                border-color: var(--fpms-primary);
                color: #fff;
            }
        `,
    ],
})
export class LogSheetsComponent extends FpmsPage implements OnInit {
    ponds: any[] = [];
    params: any[] = [];
    selected = 'all';
    pond = '';

    sheets: Sheet[] = [
        { key: 'feeding', short: 'Feeding (AM/PM)', title: 'Feeding log (AM / PM)', columns: ['Date', 'AM ✓', 'AM kg', 'AM feed lot', 'PM ✓', 'PM kg', 'PM feed lot', 'Notes'], rows: 14 },
        { key: 'wq', short: 'Water quality', title: 'Water quality log', columns: ['Time', 'DO (ppm)', 'Temp (°C)', 'pH', 'Salinity (ppt)', 'TAN (ppm)', 'Nitrite (ppm)', 'Nitrate (ppm)', 'Alkalinity (ppm)', 'Notes'], rows: 12 },
        { key: 'growth', short: 'Growth sampling', title: 'Growth sampling log', columns: ['Date / time', 'Sample size (pcs)', 'Total weight (g)', 'ABW (g)', 'Notes'], rows: 10 },
        { key: 'chemical', short: 'Chemical and supplement', title: 'Chemical and supplement application log', columns: ['Date / time', 'Product', 'Lot', 'Quantity (unit)', 'Reason', 'Notes'], rows: 10 },
        { key: 'mortality', short: 'Mortality', title: 'Mortality log', columns: ['Date / time', 'Count', 'Cause (Disease / Water quality / Others)', 'Disease name or other cause', 'Symptoms', 'Action taken'], rows: 10 },
        { key: 'prep', short: 'Pond preparation', title: 'Pond preparation log', columns: ['Date / time', 'What was done (description)', 'Recorded by'], rows: 10 },
    ];

    async ngOnInit(): Promise<void> {
        const [ponds, params] = await Promise.all([this.api.get<any[]>('Pond', 'GetAll'), this.api.get<any[]>('Log', 'GetParameters')]);
        this.ponds = ponds.filter((p) => p.purpose === 1 && p.manualStatus !== 3);
        this.params = params.filter((p) => p.isActive && p.optimalRange);
        const sheet = this.query('sheet');
        if (sheet && this.sheets.some((s) => s.key === sheet)) {
            this.selected = sheet;
        }
    }

    visibleSheets(): Sheet[] {
        return this.selected === 'all' ? this.sheets : this.sheets.filter((s) => s.key === this.selected);
    }

    rowsOf(s: Sheet): number[] {
        return Array.from({ length: s.rows }, (_, i) => i);
    }
}
