import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { FarmTime } from '@shared/fpms/farm-time';
import { FPMS_IMPORTS } from '../shared-imports';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header [narrow]="true" title="Farm profile" subtitle="Your farm, how dates show, and the production settings."></fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid fpms-narrow">
                @if (!p) {
                    <fpms-loading></fpms-loading>
                } @else {
                    <ul class="nav nav-tabs mb-3" role="tablist">
                        <li class="nav-item">
                            <a class="nav-link" href="javascript:;" role="tab" [class.active]="tab() === 'details'" [attr.aria-selected]="tab() === 'details'" (click)="tab.set('details')">
                                <i class="fas fa-home mr-1"></i>Farm details
                            </a>
                        </li>
                        @if (canSeePonds) {
                            <li class="nav-item">
                                <a class="nav-link" href="javascript:;" role="tab" [class.active]="tab() === 'ponds'" [attr.aria-selected]="tab() === 'ponds'" (click)="openPonds()">
                                    <i class="fas fa-water mr-1"></i>Ponds
                                    @if (ponds()?.length) { <span class="badge badge-secondary ml-1">{{ ponds()!.length }}</span> }
                                </a>
                            </li>
                        }
                    </ul>

                    @if (tab() === 'details' && !canEdit) {
                        <div class="card">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-warehouse" aria-hidden="true"></i>Farm details</h3></div>
                            <div class="card-body">
                                <dl class="row mb-0 fpms-dl">
                                    <dt class="col-sm-4">Farm code</dt><dd class="col-sm-8">{{ p.farmCode || '—' }}</dd>
                                    <dt class="col-sm-4">Farm name</dt><dd class="col-sm-8">{{ p.farmName }}</dd>
                                    <dt class="col-sm-4">Total farm area</dt>
                                    <dd class="col-sm-8">
                                        @if (p.totalAreaHectares) { {{ p.totalAreaHectares | num: 2 }} ha } @else { <span class="fpms-none">—</span> }
                                        <span class="fpms-none ml-1">· ponds on record {{ p.pondAreaHectares | num: 2 }} ha</span>
                                    </dd>
                                    <dt class="col-sm-4">Address</dt><dd class="col-sm-8">{{ p.fullAddress || '—' }}</dd>
                                    <dt class="col-sm-4">Coordinates</dt>
                                    <dd class="col-sm-8">
                                        @if (p.latitude != null && p.longitude != null) { {{ p.latitude | num: 6 }}, {{ p.longitude | num: 6 }} } @else { <span class="fpms-none">—</span> }
                                    </dd>
                                </dl>
                            </div>
                        </div>
                        <div class="card">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-phone" aria-hidden="true"></i>Contact</h3></div>
                            <div class="card-body">
                                <dl class="row mb-0 fpms-dl">
                                    <dt class="col-sm-4">Contact person</dt><dd class="col-sm-8">{{ p.contactName || '—' }}</dd>
                                    <dt class="col-sm-4">Phone</dt><dd class="col-sm-8">{{ p.contactPhone || '—' }}</dd>
                                    <dt class="col-sm-4">Email</dt><dd class="col-sm-8">{{ p.contactEmail || '—' }}</dd>
                                </dl>
                            </div>
                        </div>
                        <div class="card">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-cog" aria-hidden="true"></i>Settings</h3></div>
                            <div class="card-body">
                                <dl class="row mb-0 fpms-dl">
                                    <dt class="col-sm-4">Time zone</dt><dd class="col-sm-8">{{ p.timeZone }}</dd>
                                    <dt class="col-sm-4">Farming system</dt><dd class="col-sm-8">{{ p.farmingSystem | label: L.FarmingSystems }}</dd>
                                    <dt class="col-sm-4">Document alert window</dt><dd class="col-sm-8">{{ p.documentExpiryAlertDays }} days</dd>
                                    <dt class="col-sm-4">Date display</dt><dd class="col-sm-8">{{ p.dateOrder === 2 ? 'Day first (22 Oct 2026)' : 'Month first (Oct 22, 2026)' }}</dd>
                                    <dt class="col-sm-4">Time display</dt><dd class="col-sm-8">{{ p.use24HourTime ? '24-hour (14:30)' : '12-hour (2:30 PM)' }}</dd>
                                </dl>
                            </div>
                        </div>
                        <p class="text-muted small"><i class="fas fa-lock mr-1"></i>Only a Farm Owner can change the farm profile.</p>
                    }

                    @if (tab() === 'details' && canEdit) {
                        <form #f="ngForm" (ngSubmit)="save()" novalidate>
                            <fieldset [disabled]="!canEdit">
                                <div class="card">
                                    <div class="card-header"><h3 class="card-title"><i class="fas fa-warehouse" aria-hidden="true"></i>Farm details</h3></div>
                                    <div class="card-body">
                                        <div class="form-row">
                                            <div class="form-group col-md-3">
                                                <label for="farmCode">Farm code <span class="text-danger">*</span></label>
                                                <input id="farmCode" class="form-control" name="farmCode" [(ngModel)]="p.farmCode" required maxlength="64" pattern="^[a-zA-Z][a-zA-Z0-9_-]{1,63}$" autocapitalize="characters" />
                                                <small class="form-text text-muted">This also serves as the farm login code.</small>
                                            </div>
                                            <div class="form-group col-md-5">
                                                <label for="farmName">Farm name <span class="text-danger">*</span></label>
                                                <input id="farmName" class="form-control" name="farmName" [(ngModel)]="p.farmName" required maxlength="150" />
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label for="area">Total farm area (hectares)</label>
                                                <input id="area" type="number" min="0.01" max="100000" step="0.01" class="form-control" name="totalAreaHectares" [(ngModel)]="p.totalAreaHectares" />
                                                <small class="form-text text-muted">Ponds on record: {{ p.pondAreaHectares | num: 2 }} ha.</small>
                                            </div>
                                        </div>
                                        @if (areaExceeded) {
                                            <div class="alert alert-warning py-2 small" role="status">
                                                <i class="fas fa-exclamation-triangle mr-1"></i>The ponds add up to {{ p.pondAreaHectares | num: 2 }} ha, more than the total farm area.
                                                Check the total area or the pond areas. You can still save.
                                            </div>
                                        }
                                    </div>
                                </div>
                                <div class="card">
                                    <div class="card-header"><h3 class="card-title"><i class="fas fa-map-marker-alt" aria-hidden="true"></i>Address</h3></div>
                                    <div class="card-body">
                                        <div class="form-row">
                                            <div class="form-group col-md-6">
                                                <label for="line1">Address line 1 <span class="text-danger">*</span></label>
                                                <input id="line1" class="form-control" name="addressLine1" [(ngModel)]="p.addressLine1" required maxlength="200" />
                                                <small class="form-text text-muted">House number, street, purok or sitio, apartment/unit.</small>
                                            </div>
                                            <div class="form-group col-md-6">
                                                <label for="line2">Address line 2</label>
                                                <input id="line2" class="form-control" name="addressLine2" [(ngModel)]="p.addressLine2" maxlength="200" />
                                                <small class="form-text text-muted">Building, suite or floor; or the barangay.</small>
                                            </div>
                                        </div>
                                        <div class="form-row">
                                            <div class="form-group col-md-4">
                                                <label for="city">City / Municipality <span class="text-danger">*</span></label>
                                                <input id="city" class="form-control" name="cityMunicipality" [(ngModel)]="p.cityMunicipality" required maxlength="100" />
                                            </div>
                                            <div class="form-group col-md-3">
                                                <label for="province">Province <span class="text-danger">*</span></label>
                                                <input id="province" class="form-control" name="province" [(ngModel)]="p.province" required maxlength="100" />
                                            </div>
                                            <div class="form-group col-md-2">
                                                <label for="postal">Postal code</label>
                                                <input id="postal" class="form-control" name="postalCode" [(ngModel)]="p.postalCode" maxlength="10" inputmode="numeric" />
                                                @if (!postalOk) {
                                                    <small class="text-danger">4 digits, e.g. 9502.</small>
                                                }
                                            </div>
                                            <div class="form-group col-md-3">
                                                <label for="country">Country <span class="text-danger">*</span></label>
                                                <input id="country" class="form-control" name="country" [(ngModel)]="p.country" required maxlength="100" />
                                            </div>
                                        </div>
                                        <div class="form-row">
                                            <div class="form-group col-md-3">
                                                <label for="lat">Latitude</label>
                                                <input id="lat" type="number" step="0.000001" min="-90" max="90" class="form-control" name="latitude" [(ngModel)]="p.latitude" />
                                            </div>
                                            <div class="form-group col-md-3">
                                                <label for="lng">Longitude</label>
                                                <input id="lng" type="number" step="0.000001" min="-180" max="180" class="form-control" name="longitude" [(ngModel)]="p.longitude" />
                                            </div>
                                        </div>
                                        @if (p.fullAddress) {
                                            <p class="small text-muted mb-0">Printed on reports and sales records as: <span class="text-body">{{ p.fullAddress }}</span></p>
                                        }
                                    </div>
                                </div>
                                <div class="card">
                                    <div class="card-header"><h3 class="card-title"><i class="fas fa-phone" aria-hidden="true"></i>Contact</h3></div>
                                    <div class="card-body">
                                        <div class="form-row">
                                            <div class="form-group col-md-4">
                                                <label for="cname">Contact person</label>
                                                <input id="cname" class="form-control" name="contactName" [(ngModel)]="p.contactName" maxlength="100" />
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label for="cphone">Phone</label>
                                                <input id="cphone" class="form-control" name="contactPhone" [(ngModel)]="p.contactPhone" maxlength="50" />
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label for="cemail">Email</label>
                                                <input id="cemail" type="email" email class="form-control" name="contactEmail" [(ngModel)]="p.contactEmail" maxlength="256" />
                                            </div>
                                        </div>
                                    </div>
                                </div>
                                <div class="card">
                                    <div class="card-header"><h3 class="card-title"><i class="fas fa-cog" aria-hidden="true"></i>Settings</h3></div>
                                    <div class="card-body">
                                        <div class="form-row">
                                            <div class="form-group col-md-4">
                                                <label for="tz">Time zone</label>
                                                <input id="tz" class="form-control" name="timeZone" [(ngModel)]="p.timeZone" required maxlength="64" />
                                                <small class="form-text text-muted">IANA name. Default Asia/Manila (UTC+08:00).</small>
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label for="alert">Document alert window (days)</label>
                                                <input id="alert" type="number" min="1" max="365" class="form-control" name="documentExpiryAlertDays" [(ngModel)]="p.documentExpiryAlertDays" required />
                                                <small class="form-text text-muted">Permits expiring within this many days show "Expiring soon".</small>
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label for="fsys">Farming system</label>
                                                <select id="fsys" class="form-control" name="farmingSystem" [(ngModel)]="p.farmingSystem">
                                                    @for (o of Options.farmingSystem; track o.value) {
                                                        <option [ngValue]="o.value">{{ o.label }}</option>
                                                    }
                                                </select>
                                                <small class="form-text text-muted">Every pond uses this unless the pond sets its own.</small>
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label for="dord">Date display</label>
                                                <select id="dord" class="form-control" name="dateOrder" [(ngModel)]="p.dateOrder">
                                                    <option [ngValue]="1">Month first (Oct 22, 2026)</option>
                                                    <option [ngValue]="2">Day first (22 Oct 2026)</option>
                                                </select>
                                            </div>
                                            <div class="form-group col-md-4">
                                                <label for="tfmt">Time display</label>
                                                <select id="tfmt" class="form-control" name="use24HourTime" [(ngModel)]="p.use24HourTime">
                                                    <option [ngValue]="false">12-hour (2:30 PM)</option>
                                                    <option [ngValue]="true">24-hour (14:30)</option>
                                                </select>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </fieldset>
                            @if (canEdit) {
                                <div class="d-flex">
                                    <button type="submit" class="btn btn-primary mr-2" [disabled]="busy() || !f.form.valid || !postalOk">
                                        @if (busy()) {
                                            <i class="fas fa-spinner fa-spin mr-1"></i>
                                        }
                                        Save profile
                                    </button>
                                    <button type="button" class="btn btn-default" (click)="f.form.markAsPristine(); load()">Cancel</button>
                                    <fpms-form-hint [form]="f"></fpms-form-hint>
                                </div>
                            } @else {
                                <p class="text-muted small"><i class="fas fa-lock mr-1"></i>Only a Farm Owner can change the farm profile.</p>
                            }
                        </form>
                    }

                    @if (tab() === 'ponds') {
                        <div class="card">
                            <div class="card-header d-flex align-items-center">
                                <h3 class="card-title mr-auto"><i class="fas fa-water" aria-hidden="true"></i>Pond specifications</h3>
                                <a routerLink="/app/ponds" class="btn btn-sm btn-outline-primary">Manage ponds</a>
                            </div>
                            @if (ponds()?.length) {
                                <div class="card-body pb-0">
                                    <div class="row">
                                        <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ ponds()!.length | num: 0 }}</div><div class="l">Ponds</div></div></div>
                                        <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ p.pondAreaHectares | num: 2 }} ha</div><div class="l">Total pond area</div></div></div>
                                        <div class="col-6 col-md-3"><div class="fpms-kpi"><div class="v">{{ inProduction | num: 0 }}</div><div class="l">In production now</div></div></div>
                                        <div class="col-6 col-md-3">
                                            <div class="fpms-kpi" [class.fpms-kpi-warn]="areaExceeded">
                                                @if (p.totalAreaHectares) {
                                                    <div class="v">{{ areaShare | num: 0 }}%</div>
                                                    <div class="l">of {{ p.totalAreaHectares | num: 2 }} ha farm area</div>
                                                } @else {
                                                    <div class="v fpms-none">—</div>
                                                    <div class="l">Farm area not set</div>
                                                }
                                            </div>
                                        </div>
                                    </div>
                                    @if (p.totalAreaHectares) {
                                        <div class="progress mb-2" style="height: 0.5rem" role="progressbar" aria-label="Share of the farm area used by ponds" [attr.aria-valuenow]="areaShare" aria-valuemin="0" aria-valuemax="100">
                                            <div class="progress-bar" [class.bg-warning]="areaExceeded" [style.width.%]="areaShare > 100 ? 100 : areaShare"></div>
                                        </div>
                                        @if (areaExceeded) {
                                            <div class="small text-warning-dark mb-2"><i class="fas fa-exclamation-triangle mr-1"></i>The ponds add up to more than the total farm area. Check the total area or the pond areas.</div>
                                        }
                                    } @else {
                                        <div class="small text-muted mb-2">Add the total farm area on the Farm details tab to compare it with the ponds.</div>
                                    }
                                </div>
                            }
                            <div class="card-body p-0 table-responsive">
                                @if (!ponds()) {
                                    <fpms-loading></fpms-loading>
                                } @else if (!ponds()!.length) {
                                    <fpms-empty text="No ponds yet." icon="fas fa-water">
                                        <a routerLink="/app/ponds" class="btn btn-sm btn-primary">Add ponds</a>
                                    </fpms-empty>
                                } @else {
                                    <table class="table table-sm table-hover mb-0">
                                        <caption class="sr-only">Pond specifications</caption>
                                        <thead>
                                            <tr>
                                                <th>Pond</th>
                                                <th>Purpose</th>
                                                <th>Construction</th>
                                                <th class="text-right">Area (m²)</th>
                                                <th class="text-right">Avg depth (m)</th>
                                                <th class="text-right">Max depth (m)</th>
                                                <th>Farming system</th>
                                                <th>Status</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            @for (x of ponds(); track x.id) {
                                                <tr>
                                                    <td class="text-nowrap">
                                                        <a [routerLink]="['/app/ponds', x.id]">{{ x.pondCode }}</a>
                                                        <div class="small text-muted">{{ x.name }}</div>
                                                    </td>
                                                    <td>{{ x.purpose | label: L.PondPurposes }}</td>
                                                    <td>{{ x.constructionType | label: L.ConstructionTypes }}</td>
                                                    <td class="text-right">{{ x.areaM2 | num: 0 }}</td>
                                                    <td class="text-right">{{ x.averageDepthM | num: 2 }}</td>
                                                    <td class="text-right">{{ x.maxDepthM | num: 2 }}</td>
                                                    <td>
                                                        {{ x.farmingSystem | label: L.FarmingSystems }}
                                                        @if (x.farmingSystemOverride == null) {
                                                            <span class="small text-muted">(farm setting)</span>
                                                        }
                                                        @if (x.systemWarning) {
                                                            <div class="small text-warning-dark"><i class="fas fa-exclamation-triangle mr-1"></i>{{ x.systemWarning }}</div>
                                                        }
                                                    </td>
                                                    <td><fpms-badge [text]="x.status | label: L.PondDisplayStatuses"></fpms-badge></td>
                                                </tr>
                                            }
                                        </tbody>
                                        <tfoot>
                                            <tr>
                                                <td colspan="3">{{ ponds()!.length === 1 ? '1 pond' : ponds()!.length + ' ponds' }}</td>
                                                <td class="text-right">{{ totalAreaM2 | num: 0 }}</td>
                                                <td colspan="4"></td>
                                            </tr>
                                        </tfoot>
                                    </table>
                                }
                            </div>
                        </div>
                    }
                }
            </div>
        </section>
    `,
})
export class FarmProfileComponent extends FpmsPage implements OnInit {
    p: any = null;
    tab = signal<'details' | 'ponds'>('details');
    ponds = signal<any[] | null>(null);

    get canEdit(): boolean {
        return this.can('Farm.Profile.Edit');
    }

    get canSeePonds(): boolean {
        return this.can('Farm.Ponds');
    }

    get totalAreaM2(): number {
        return (this.ponds() ?? []).reduce((n, x) => n + Number(x.areaM2 || 0), 0);
    }

    get inProduction(): number {
        return (this.ponds() ?? []).filter((x) => x.status === 3).length;
    }

    get areaShare(): number {
        const total = Number(this.p?.totalAreaHectares);
        return total > 0 ? (Number(this.p.pondAreaHectares) / total) * 100 : 0;
    }

    get areaExceeded(): boolean {
        return !!this.p?.totalAreaHectares && Number(this.p.pondAreaHectares) > Number(this.p.totalAreaHectares);
    }

    get postalOk(): boolean {
        const code = this.p?.postalCode?.trim();
        return !code || (this.p?.country ?? '').trim().toLowerCase() !== 'philippines' || /^[0-9]{4}$/.test(code);
    }

    ngOnInit(): void {
        this.load();
    }

    async load(): Promise<void> {
        this.p = await this.api.get<any>('FarmProfile', 'Get');
    }

    async openPonds(): Promise<void> {
        this.tab.set('ponds');
        if (!this.ponds()) {
            this.ponds.set(await this.api.get<any[]>('Pond', 'GetAll'));
        }
    }

    async save(): Promise<void> {
        let warnings: any[] = [];
        if (await this.act(async () => (warnings = (await this.api.put<any[]>('FarmProfile', 'Update', this.p)) ?? []))) {
            FarmTime.setZone(this.p.timeZone);
            FarmTime.setDisplay(this.p.dateOrder, this.p.use24HourTime);
            this.notifySaved('Farm profile saved.');
            if (warnings.length) {
                this.showWarnings(warnings);
            }
            await this.load();
        }
    }
}
