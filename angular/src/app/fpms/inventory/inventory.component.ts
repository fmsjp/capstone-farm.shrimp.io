import { Component, OnInit, signal } from '@angular/core';
import { FpmsPage } from '@shared/fpms/fpms-page';
import { EMPTY_GUID } from '@shared/fpms/farm-time';
import { InventoryUnits } from '@shared/fpms/labels';
import { FPMS_IMPORTS } from '../shared-imports';
import { ClientListFilter } from '@shared/fpms/client-list';

@Component({
    standalone: true,
    imports: FPMS_IMPORTS,
    template: `
        <fpms-page-header title="Inventory" subtitle="Feed, supplements and chemicals: stock on hand per lot.">
            @if (canManage) {
                <button class="btn btn-primary mr-1" (click)="startReceipt()"><i class="fas fa-truck-loading mr-1"></i>Record receipt</button>
                <button class="btn btn-outline-primary" (click)="startItem()"><i class="fas fa-plus mr-1"></i>New item</button>
            }
        </fpms-page-header>
        <section class="content px-2 pb-3">
            <div class="container-fluid">
                @if (itemForm) {
                    <div class="fpms-dialog-backdrop"><div class="fpms-dialog" role="dialog" aria-modal="true"><div class="fpms-dialog-body p-0">
                        <div class="card card-outline card-primary">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-boxes" aria-hidden="true"></i>{{ itemForm.id === empty ? 'New inventory item' : 'Edit ' + itemForm.displayName }}</h3></div>
                            <form #itf="ngForm" (ngSubmit)="saveItem()" novalidate>
                                <div class="card-body">
                                    <div class="form-row">
                                        <div class="form-group col-md-3">
                                            <label for="cat">Category <span class="text-danger">*</span></label>
                                            <select id="cat" class="form-control" name="category" [(ngModel)]="itemForm.category">
                                                @for (o of Options.inventoryCategory; track o.value) {
                                                    <option [ngValue]="o.value">{{ o.label }}</option>
                                                }
                                            </select>
                                        </div>
                                        <div class="form-group col-md-3">
                                            <label for="br">Brand</label>
                                            <input id="br" class="form-control" name="brand" [(ngModel)]="itemForm.brand" maxlength="100" />
                                        </div>
                                        <div class="form-group col-md-4">
                                            <label for="pn">Product name <span class="text-danger">*</span></label>
                                            <input id="pn" class="form-control" name="productName" [(ngModel)]="itemForm.productName" required maxlength="150" />
                                        </div>
                                        <div class="form-group col-md-2">
                                            <label for="un">Unit <span class="text-danger">*</span></label>
                                            <select id="un" class="form-control" name="unit" [(ngModel)]="itemForm.unit">
                                                @for (u of units; track u) {
                                                    <option [value]="u">{{ u }}</option>
                                                }
                                            </select>
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        @if (itemForm.category === 1) {
                                            <div class="form-group col-md-3">
                                                <label for="ft">Feed type</label>
                                                <input id="ft" class="form-control" name="feedType" [(ngModel)]="itemForm.feedType" maxlength="50" list="feedTypes" placeholder="starter, grower, finisher" />
                                                <datalist id="feedTypes"><option value="Starter"></option><option value="Grower"></option><option value="Finisher"></option></datalist>
                                            </div>
                                        }
                                        <div class="form-group col-md-3">
                                            <label for="bai">BAI registration no.</label>
                                            <input id="bai" class="form-control" name="baiRegistrationNo" [(ngModel)]="itemForm.baiRegistrationNo" maxlength="100" />
                                        </div>
                                        <div class="form-group col-md-3">
                                            <label for="rl">Reorder level ({{ itemForm.unit }})</label>
                                            <input id="rl" type="number" min="0" step="0.001" class="form-control" name="reorderLevel" [(ngModel)]="itemForm.reorderLevel" />
                                            <small class="form-text text-muted">Stock at or below this shows as Low.</small>
                                        </div>
                                        <div class="form-group col-md-3">
                                            <label for="ps">Pack size ({{ itemForm.unit }} per pack)</label>
                                            <input id="ps" type="number" min="0.001" step="0.001" class="form-control" name="packSize" [(ngModel)]="itemForm.packSize" placeholder="e.g. 25 per bag" />
                                            <small class="form-text text-muted">Lets receipts be entered in packs.</small>
                                        </div>
                                        <div class="form-group col-md-3 d-flex align-items-end">
                                            <div class="custom-control custom-checkbox mb-2">
                                                <input type="checkbox" class="custom-control-input" id="iact" name="isActive" [(ngModel)]="itemForm.isActive" />
                                                <label class="custom-control-label" for="iact">Active</label>
                                            </div>
                                        </div>
                                    </div>
                                    @if (itemForm.category !== 1) {
                                        <div class="form-row">
                                            <div class="form-group col-md-6">
                                                <label for="ai">Active ingredient</label>
                                                <input id="ai" class="form-control" name="activeIngredient" [(ngModel)]="itemForm.activeIngredient" maxlength="150" placeholder="for drugs and chemicals" />
                                            </div>
                                            <div class="form-group col-md-3">
                                                <label for="wdd">Withdrawal period (days)</label>
                                                <input id="wdd" type="number" min="0" max="365" step="1" class="form-control" name="defaultWithdrawalDays" [(ngModel)]="itemForm.defaultWithdrawalDays" />
                                                <small class="form-text text-muted">Pre-filled when the product is applied.</small>
                                            </div>
                                        </div>
                                    }
                                </div>
                                <div class="card-footer">
                                    <button type="submit" class="btn btn-primary mr-2" [disabled]="busy() || !itf.form.valid">Save item</button>
                                    <button type="button" class="btn btn-default" (click)="itemForm = null">Cancel</button>
                                    <fpms-form-hint [form]="itf"></fpms-form-hint>
                                </div>
                            </form>
                        </div>
                    </div></div></div>
                }

                @if (receipt) {
                    <div class="fpms-dialog-backdrop"><div class="fpms-dialog" role="dialog" aria-modal="true"><div class="fpms-dialog-body p-0">
                        <div class="card card-outline card-primary">
                            <div class="card-header"><h3 class="card-title"><i class="fas fa-boxes" aria-hidden="true"></i>Record stock receipt</h3></div>
                            <form #rf="ngForm" (ngSubmit)="saveReceipt()" novalidate>
                                <div class="card-body">
                                    <div class="form-row">
                                        <div class="form-group col-md-4">
                                            <label for="ri">Item <span class="text-danger">*</span></label>
                                            <select id="ri" class="form-control" name="itemId" [(ngModel)]="receipt.itemId" required>
                                                @for (i of activeItems(); track i.id) {
                                                    <option [value]="i.id">{{ i.displayName }} ({{ i.unit }})</option>
                                                }
                                            </select>
                                        </div>
                                        <div class="form-group col-md-3">
                                            <label for="rl2">Lot number <span class="text-danger">*</span></label>
                                            <input id="rl2" class="form-control" name="lotNumber" [(ngModel)]="receipt.lotNumber" required maxlength="100" />
                                            <small class="form-text text-muted">An existing lot number adds to that lot.</small>
                                        </div>
                                        <div class="form-group col-md-2">
                                            <label for="rd">Received <span class="text-danger">*</span></label>
                                            <input id="rd" type="date" class="form-control" name="receivedDate" [(ngModel)]="receipt.receivedDate" required [max]="today" />
                                        </div>
                                        <div class="form-group col-md-3">
                                            <label for="re">Lot expiry</label>
                                            <input id="re" type="date" class="form-control" name="expiryDate" [(ngModel)]="receipt.expiryDate" />
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        @if (receiptPackSize && receipt.inPacks) {
                                            <div class="form-group col-md-3">
                                                <label for="rpc">Packs <span class="text-danger">*</span></label>
                                                <input id="rpc" type="number" min="1" step="1" class="form-control" name="packCount" [(ngModel)]="receipt.packCount" required />
                                                <small class="form-text text-muted">= {{ (receipt.packCount || 0) * receiptPackSize | num: 3 }} {{ unitOf(receipt.itemId) }} ({{ receiptPackSize }} per pack)</small>
                                            </div>
                                            <div class="form-group col-md-3">
                                                <label for="rpk">Cost per pack (₱)</label>
                                                <input id="rpk" type="number" min="0" step="0.01" class="form-control" name="packCost" [(ngModel)]="receipt.packCost" />
                                                <small class="form-text text-muted">Sets the lot's cost for usage and cycle reports.</small>
                                            </div>
                                        } @else {
                                            <div class="form-group col-md-3">
                                                <label for="rq">Quantity ({{ unitOf(receipt.itemId) }}) <span class="text-danger">*</span></label>
                                                <input id="rq" type="number" min="0.001" step="0.001" class="form-control" name="quantity" [(ngModel)]="receipt.quantity" required />
                                            </div>
                                            <div class="form-group col-md-3">
                                                <label for="rc">Unit cost (₱ per {{ unitOf(receipt.itemId) }})</label>
                                                <input id="rc" type="number" min="0" step="0.01" class="form-control" name="unitCost" [(ngModel)]="receipt.unitCost" />
                                                <small class="form-text text-muted">Sets the lot's cost for usage and cycle reports.</small>
                                            </div>
                                        }
                                        <div class="form-group col-md-3">
                                            <label for="rs">Supplier</label>
                                            <input id="rs" class="form-control" name="supplier" [(ngModel)]="receipt.supplier" maxlength="150" />
                                        </div>
                                        <div class="form-group col-md-3">
                                            <label for="rn">Note</label>
                                            <input id="rn" class="form-control" name="note" [(ngModel)]="receipt.note" maxlength="300" />
                                        </div>
                                    </div>
                                    @if (receiptPackSize) {
                                        <div class="custom-control custom-switch mb-2">
                                            <input type="checkbox" class="custom-control-input" id="inPacks" name="inPacks" [(ngModel)]="receipt.inPacks" />
                                            <label class="custom-control-label" for="inPacks">Enter in packs</label>
                                        </div>
                                    }
                                    @if (receiptPackSize && receipt.inPacks) {
                                        @if (receipt.packCount > 0 && receipt.packCost > 0) {
                                            <p class="mb-0">Total cost: <strong>{{ receipt.packCount * receipt.packCost | php }}</strong></p>
                                        }
                                    } @else if (receipt.quantity > 0 && receipt.unitCost > 0) {
                                        <p class="mb-0">Total cost: <strong>{{ receipt.quantity * receipt.unitCost | php }}</strong></p>
                                    }
                                </div>
                                <div class="card-footer">
                                    <button type="submit" class="btn btn-primary mr-2" [disabled]="busy() || !rf.form.valid">Save receipt</button>
                                    <button type="button" class="btn btn-default" (click)="receipt = null">Cancel</button>
                                    <fpms-form-hint [form]="rf"></fpms-form-hint>
                                </div>
                            </form>
                        </div>
                    </div></div></div>
                }

                <div class="card">
                    <div class="card-header">
                        <fpms-list-toolbar
                            placeholder="Search by item, brand or BAI no."
                            [search]="f.search()"
                            [resultText]="f.resultText()"
                            [chips]="f.chips()"
                            [chip]="f.chip()"
                            (searchChange)="f.search.set($event)"
                            (chipChange)="f.chip.set($event)"></fpms-list-toolbar>
                    </div>
                    <div class="card-body p-0 table-responsive">
                        @if (!items()) {
                            <fpms-loading></fpms-loading>
                        } @else if (f.noMatch()) {
                            <fpms-empty text="No items match. Clear the search or the filter." icon="fas fa-boxes"></fpms-empty>
                        } @else if (!items().length) {
                            <fpms-empty text="No inventory items yet. Add feed and supplement items, then record opening stock." icon="fas fa-boxes"></fpms-empty>
                        } @else {
                            <table class="table table-hover mb-0">
                                <thead>
                                    <tr><th>Item</th><th>Category</th><th>BAI reg. no.</th><th class="text-right">On hand</th><th>Stock</th><th><span class="sr-only">Actions</span></th></tr>
                                </thead>
                                <tbody>
                                    @for (i of f.visible(); track i.id) {
                                        <tr [class.table-active]="selectedItem?.id === i.id" [class.text-muted]="!i.isActive">
                                            <td>
                                                <a href="javascript:;" (click)="selectItem(i)">{{ i.displayName }}</a>
                                                @if (i.feedType) { <span class="small text-muted"> · {{ i.feedType }}</span> }
                                                @if (!i.isActive) { <span class="small"> (inactive)</span> }
                                            </td>
                                            <td>{{ i.category | label: L.InventoryCategories }}</td>
                                            <td>{{ i.baiRegistrationNo }}</td>
                                            <td class="text-right">{{ i.quantityOnHand | num: 3 }} {{ i.unit }}</td>
                                            <td>
                                                @if (i.isNegativeStock) {
                                                    <fpms-badge text="Negative"></fpms-badge>
                                                } @else if (i.isLowStock) {
                                                    <fpms-badge text="Low"></fpms-badge>
                                                } @else {
                                                    <fpms-badge text="OK" tone="success"></fpms-badge>
                                                }
                                            </td>
                                            <td class="text-right text-nowrap">
                                                <button class="btn btn-sm btn-outline-secondary mr-1" (click)="selectItem(i)">Lots</button>
                                                @if (canManage) {
                                                    <button class="btn btn-sm btn-outline-primary" (click)="startItem(i)">Edit</button>
                                                }
                                            </td>
                                        </tr>
                                    }
                                </tbody>
                            </table>
                        }
                    </div>
                </div>

                @if (selectedItem) {
                    <div class="card">
                        <div class="card-header d-flex">
                            <h3 class="card-title mr-auto"><i class="fas fa-boxes" aria-hidden="true"></i>Lots of {{ selectedItem.displayName }}</h3>
                            <button class="btn btn-sm btn-default" (click)="selectedItem = null; lots.set(null); movements.set(null)">Close</button>
                        </div>
                        <div class="card-body p-0 table-responsive">
                            <table class="table table-sm mb-0">
                                <thead>
                                    <tr><th>Lot</th><th>Received</th><th>Expiry</th><th>Supplier</th><th class="text-right">Unit cost</th><th class="text-right">On hand</th><th>Flags</th><th><span class="sr-only">Actions</span></th></tr>
                                </thead>
                                <tbody>
                                    @for (l of lots() ?? []; track l.id) {
                                        <tr [class.table-active]="selectedLot?.id === l.id">
                                            <td><a href="javascript:;" (click)="selectLot(l)">{{ l.lotNumber }}</a></td>
                                            <td>{{ l.receivedDate | farmDate }}</td>
                                            <td [class.text-danger]="l.isExpired">{{ l.expiryDate | farmDate }} @if (l.isExpired) { (expired) }</td>
                                            <td>{{ l.supplier }}</td>
                                            <td class="text-right">{{ l.unitCost | php }}</td>
                                            <td class="text-right" [class.text-danger]="l.isNegative">{{ l.quantityOnHand | num: 3 }} {{ l.unit }}</td>
                                            <td>
                                                @if (l.isNegative) { <fpms-badge text="Negative"></fpms-badge> }
                                                @if (l.needsReconciliation) { <fpms-badge text="Reconcile"></fpms-badge> }
                                            </td>
                                            <td class="text-right text-nowrap">
                                                <button class="btn btn-sm btn-outline-secondary mr-1" (click)="selectLot(l)">Movements</button>
                                                @if (canManage) {
                                                    <button class="btn btn-sm btn-outline-primary mr-1" (click)="startAdjust(l)">Adjust</button>
                                                    @if (l.needsReconciliation) {
                                                        <button class="btn btn-sm btn-outline-primary" (click)="clearFlag(l)">Mark reconciled</button>
                                                    }
                                                }
                                            </td>
                                        </tr>
                                    } @empty {
                                        <tr><td colspan="8" class="text-muted text-center py-3">No lots yet. Record a receipt to add stock.</td></tr>
                                    }
                                </tbody>
                            </table>
                        </div>
                    </div>
                }

                @if (adjust) {
                    <div class="card card-outline card-primary">
                        <div class="card-header"><h3 class="card-title"><i class="fas fa-boxes" aria-hidden="true"></i>Adjust lot {{ adjust.lot.lotNumber }} (on hand {{ adjust.lot.quantityOnHand | num: 3 }} {{ adjust.lot.unit }})</h3></div>
                        <form #af="ngForm" (ngSubmit)="saveAdjust()" novalidate>
                            <div class="card-body">
                                <div class="form-row">
                                    <div class="form-group col-md-3">
                                        <label for="aq">Quantity change ({{ adjust.lot.unit }}) <span class="text-danger">*</span></label>
                                        <input id="aq" type="number" step="0.001" class="form-control" name="quantity" [(ngModel)]="adjust.quantity" required />
                                        <small class="form-text text-muted">Positive adds stock; negative removes it.</small>
                                    </div>
                                    <div class="form-group col-md-3">
                                        <label for="ad">Date</label>
                                        <input id="ad" type="date" class="form-control" name="date" [(ngModel)]="adjust.date" [max]="today" />
                                    </div>
                                    <div class="form-group col-md-6">
                                        <label for="ar">Reason <span class="text-danger">*</span></label>
                                        <input id="ar" class="form-control" name="reason" [(ngModel)]="adjust.reason" required maxlength="300" placeholder="e.g. physical count, spoilage, opening balance" />
                                    </div>
                                </div>
                                @if (adjust.quantity) {
                                    <p class="mb-0">New balance: <strong>{{ adjust.lot.quantityOnHand + adjust.quantity | num: 3 }} {{ adjust.lot.unit }}</strong></p>
                                }
                            </div>
                            <div class="card-footer">
                                <button type="submit" class="btn btn-primary mr-2" [disabled]="busy() || !af.form.valid || !adjust.quantity">Save adjustment</button>
                                <button type="button" class="btn btn-default" (click)="adjust = null">Cancel</button>
                                <fpms-form-hint [form]="af"></fpms-form-hint>
                            </div>
                        </form>
                    </div>
                }

                @if (selectedLot && movements()) {
                    <div class="card">
                        <div class="card-header"><h3 class="card-title"><i class="fas fa-exchange-alt" aria-hidden="true"></i>Movement ledger: lot {{ selectedLot.lotNumber }}</h3></div>
                        <div class="card-body p-0 table-responsive">
                            <table class="table table-sm mb-0">
                                <thead>
                                    <tr><th>Date</th><th>Type</th><th class="text-right">Quantity</th><th class="text-right">Unit cost</th><th class="text-right">Balance</th><th>Batch</th><th>Source / reason</th><th>By</th></tr>
                                </thead>
                                <tbody>
                                    @for (m of movements(); track m.id) {
                                        <tr>
                                            <td class="text-nowrap">{{ m.movementDateUtc | farmDateTime }}</td>
                                            <td>{{ m.type | label: L.MovementTypes }}</td>
                                            <td class="text-right" [class.text-danger]="m.quantity < 0">{{ m.quantity | num: 3 }}</td>
                                            <td class="text-right">{{ m.unitCost | php }}</td>
                                            <td class="text-right">{{ m.runningBalance | num: 3 }}</td>
                                            <td>
                                                @if (m.batchId) {
                                                    <a [routerLink]="['/app/production/batches', m.batchId]">{{ m.batchCode }}</a>
                                                }
                                            </td>
                                            <td class="small">{{ m.source }}{{ m.source && m.reason ? ' · ' : '' }}{{ m.reason }}</td>
                                            <td class="small text-muted">{{ m.recordedBy }}</td>
                                        </tr>
                                    } @empty {
                                        <tr><td colspan="8" class="text-center text-muted py-3">No movements.</td></tr>
                                    }
                                </tbody>
                            </table>
                        </div>
                        <div class="card-footer small text-muted">Movements are never edited. Corrections are recorded as adjustments with a reason.</div>
                    </div>
                }
            </div>
        </section>
    `,
})
export class InventoryComponent extends FpmsPage implements OnInit {
    readonly empty = EMPTY_GUID;
    readonly units = InventoryUnits;
    items = signal<any[] | null>(null);
    f = new ClientListFilter<any>(
        () => this.items(),
        (i) => [i.displayName, i.brand, i.productName, i.baiRegistrationNo, i.feedType],
        (i) => i.category,
        Object.entries(this.L.InventoryCategories).map(([value, label]) => ({ value: Number(value), label: label as string })),
        'All items',
        ['item', 'items']
    );
    activeItems = signal<any[]>([]);
    lots = signal<any[] | null>(null);
    movements = signal<any[] | null>(null);
    selectedItem: any = null;
    selectedLot: any = null;
    itemForm: any = null;
    receipt: any = null;
    adjust: any = null;

    get canManage(): boolean {
        return this.can('Farm.Inventory.Manage');
    }

    ngOnInit(): void {
        this.loadItems();
    }

    async loadItems(): Promise<void> {
        const list = await this.api.get<any[]>('Inventory', 'GetItems');
        this.items.set(list);
        this.activeItems.set(list.filter((i) => i.isActive));
        if (this.selectedItem) {
            this.selectedItem = list.find((i) => i.id === this.selectedItem.id) ?? null;
        }
    }

    unitOf(itemId: string): string {
        return this.items()?.find((i) => i.id === itemId)?.unit ?? '';
    }

    async selectItem(i: any): Promise<void> {
        this.selectedItem = i;
        this.selectedLot = null;
        this.movements.set(null);
        this.lots.set(await this.api.get<any[]>('Inventory', 'GetLots', { itemId: i.id }));
    }

    async selectLot(l: any): Promise<void> {
        this.selectedLot = l;
        this.movements.set(await this.api.get<any[]>('Inventory', 'GetMovements', { lotId: l.id }));
    }

    startItem(i?: any): void {
        this.receipt = null;
        this.itemForm = i ? { ...i } : { id: EMPTY_GUID, category: 1, brand: '', productName: '', unit: 'kg', isActive: true };
    }

    async saveItem(): Promise<void> {
        if (await this.act(() => this.api.post('Inventory', 'SaveItem', this.itemForm))) {
            this.notifySaved('Item saved.');
            this.itemForm = null;
            this.loadItems();
        }
    }

    startReceipt(): void {
        this.itemForm = null;
        this.receipt = {
            itemId: this.selectedItem?.id ?? this.activeItems()[0]?.id ?? '',
            lotNumber: '',
            receivedDate: this.today,
            expiryDate: '',
            supplier: '',
            quantity: null,
            unitCost: null,
            note: '',
            inPacks: true,
            packCount: null,
            packCost: null,
        };
    }

    get receiptPackSize(): number | null {
        return this.items()?.find((i) => i.id === this.receipt?.itemId)?.packSize ?? null;
    }

    async saveReceipt(): Promise<void> {
        const packs = !!this.receiptPackSize && this.receipt.inPacks;
        const body = {
            ...this.receipt,
            expiryDate: this.receipt.expiryDate || null,
            quantity: packs ? 0 : this.receipt.quantity,
            unitCost: packs ? null : this.receipt.unitCost,
            packCount: packs ? this.receipt.packCount : null,
            packCost: packs ? this.receipt.packCost : null,
        };
        let result: any;
        if (await this.act(async () => (result = await this.api.post<any>('Inventory', 'RecordReceipt', body)))) {
            this.notifySaved('Receipt saved.');
            const item = this.items()?.find((i) => i.id === body.itemId);
            this.receipt = null;
            await this.loadItems();
            if (item) {
                this.selectItem(this.items().find((i) => i.id === item.id));
            }
        }
    }

    startAdjust(l: any): void {
        this.adjust = { lot: l, quantity: null, reason: '', date: this.today };
    }

    async saveAdjust(): Promise<void> {
        const a = this.adjust;
        if (await this.act(() => this.api.post('Inventory', 'RecordAdjustment', { lotId: a.lot.id, quantity: a.quantity, reason: a.reason, date: a.date || null }))) {
            this.notifySaved('Adjustment recorded.');
            this.adjust = null;
            await this.loadItems();
            await this.selectItem(this.selectedItem);
            const lot = this.lots()?.find((x) => x.id === a.lot.id);
            if (lot) {
                this.selectLot(lot);
            }
        }
    }

    async clearFlag(l: any): Promise<void> {
        if (await this.act(() => this.api.post('Inventory', 'ClearReconciliationFlag', { id: l.id }))) {
            this.selectItem(this.selectedItem);
        }
    }
}
