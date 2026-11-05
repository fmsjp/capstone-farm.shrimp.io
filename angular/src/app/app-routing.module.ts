import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AppRouteGuard } from '@shared/auth/auth-route-guard';
import { unsavedChangesGuard } from '@shared/fpms/unsaved-changes.guard';
import { AppComponent } from './app.component';

const page = (permission?: string) => ({ canActivate: [AppRouteGuard], canDeactivate: [unsavedChangesGuard], data: permission ? { permission } : {} });

const routes: Routes = [
    {
        path: '',
        component: AppComponent,
        children: [
            { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
            { path: 'dashboard', title: 'Dashboard', loadComponent: () => import('./fpms/dashboard/dashboard.component').then((m) => m.DashboardComponent), ...page('Farm.Dashboard') },
            { path: 'help', title: 'Help', loadComponent: () => import('./fpms/account/help.component').then((m) => m.HelpComponent), ...page() },
            { path: 'account', title: 'My account', loadComponent: () => import('./fpms/account/my-account.component').then((m) => m.MyAccountComponent), ...page() },

            { path: 'farm/profile', title: 'Farm profile', loadComponent: () => import('./fpms/farm/profile.component').then((m) => m.FarmProfileComponent), ...page('Farm.Profile') },
            { path: 'farm/standards', title: 'Farm standards', loadComponent: () => import('./fpms/farm/standards.component').then((m) => m.FarmStandardsComponent), ...page('Farm.Profile') },
            { path: 'farm/users', title: 'Farm users', loadComponent: () => import('./fpms/farm/users.component').then((m) => m.FarmUsersComponent), ...page('Farm.Users') },
            { path: 'farm/documents', title: 'Permits and licenses', loadComponent: () => import('./fpms/farm/documents.component').then((m) => m.DocumentsComponent), ...page('Farm.Compliance') },
            { path: 'farm/activity', title: 'Activity log', loadComponent: () => import('./fpms/farm/activity.component').then((m) => m.ActivityComponent), ...page('Farm.ActivityLog') },

            { path: 'ponds', title: 'Ponds', loadComponent: () => import('./fpms/ponds/ponds.component').then((m) => m.PondsComponent), ...page('Farm.Ponds') },
            { path: 'ponds/prefixes', title: 'Pond name prefixes', loadComponent: () => import('./fpms/ponds/pond-prefixes.component').then((m) => m.PondPrefixesComponent), ...page('Farm.Ponds') },
            { path: 'ponds/new', title: 'New pond', loadComponent: () => import('./fpms/ponds/pond-detail.component').then((m) => m.PondDetailComponent), ...page('Farm.Ponds.Manage') },
            { path: 'ponds/:id', title: 'Pond', loadComponent: () => import('./fpms/ponds/pond-detail.component').then((m) => m.PondDetailComponent), ...page('Farm.Ponds') },

            { path: 'resources/fry-deliveries', title: 'Fry deliveries', loadComponent: () => import('./fpms/resources/fry-deliveries.component').then((m) => m.FryDeliveriesComponent), ...page('Farm.FryDeliveries') },
            { path: 'resources/fry-deliveries/:id', title: 'Fry delivery', loadComponent: () => import('./fpms/resources/fry-delivery-detail.component').then((m) => m.FryDeliveryDetailComponent), ...page('Farm.FryDeliveries') },
            { path: 'resources/hatcheries', title: 'Hatchery sources', loadComponent: () => import('./fpms/resources/hatcheries.component').then((m) => m.HatcheriesComponent), ...page('Farm.Hatcheries') },
            { path: 'resources/buyers', title: 'Buyers', loadComponent: () => import('./fpms/resources/buyers.component').then((m) => m.BuyersComponent), ...page('Farm.Buyers') },
            { path: 'inventory', title: 'Inventory', loadComponent: () => import('./fpms/inventory/inventory.component').then((m) => m.InventoryComponent), ...page('Farm.Inventory') },

            { path: 'production/cycles', title: 'Production cycles', loadComponent: () => import('./fpms/production/cycles.component').then((m) => m.CyclesComponent), ...page('Farm.Production') },
            { path: 'production/cycles/:id', title: 'Cycle', loadComponent: () => import('./fpms/production/cycle-detail.component').then((m) => m.CycleDetailComponent), ...page('Farm.Production') },
            { path: 'production/batches', title: 'Pond batches', loadComponent: () => import('./fpms/production/batches.component').then((m) => m.BatchesComponent), ...page('Farm.Production') },
            { path: 'production/batches/:id', title: 'Batch', loadComponent: () => import('./fpms/production/batch-detail.component').then((m) => m.BatchDetailComponent), ...page('Farm.Production') },
            { path: 'production/create-batch', title: 'Create Batch', loadComponent: () => import('./fpms/production/stock-batch.component').then((m) => m.CreateBatchComponent), ...page('Farm.Production.Stock') },

            { path: 'logbook', title: 'Logbooks', loadComponent: () => import('./fpms/logbook/logbook.component').then((m) => m.LogbookComponent), ...page('Farm.Logs') },
            { path: 'logbook/new', title: 'Record operations', loadComponent: () => import('./fpms/logbook/log-entry.component').then((m) => m.LogEntryComponent), ...page('Farm.Logs.Create') },
            { path: 'logbook/feeding', title: 'Feeding', loadComponent: () => import('./fpms/logbook/feeding-sheet.component').then((m) => m.FeedingSheetComponent), ...page('Farm.Logs') },
            { path: 'logbook/feeding-sheet', redirectTo: 'logbook/feeding' },
            { path: 'logbook/status', title: 'Daily log status', loadComponent: () => import('./fpms/logbook/daily-status.component').then((m) => m.DailyStatusComponent), ...page('Farm.Logs') },
            { path: 'logbook/sheets', title: 'Printable log sheets', loadComponent: () => import('./fpms/logbook/log-sheets.component').then((m) => m.LogSheetsComponent), ...page('Farm.Logs') },
            { path: 'logbook/:id', title: 'Log entry', loadComponent: () => import('./fpms/logbook/log-entry.component').then((m) => m.LogEntryComponent), ...page('Farm.Logs') },

            { path: 'harvests', title: 'Harvests', loadComponent: () => import('./fpms/harvest/harvests.component').then((m) => m.HarvestsComponent), ...page('Farm.Harvests') },
            { path: 'harvests/size-grades', title: 'Size grades', loadComponent: () => import('./fpms/harvest/size-grades.component').then((m) => m.SizeGradesComponent), ...page('Farm.Harvests') },
            { path: 'harvests/new', title: 'Record harvest', loadComponent: () => import('./fpms/harvest/harvest-entry.component').then((m) => m.HarvestEntryComponent), ...page('Farm.Harvests.Create') },
            { path: 'harvests/:id', title: 'Harvest', loadComponent: () => import('./fpms/harvest/harvest-entry.component').then((m) => m.HarvestEntryComponent), ...page('Farm.Harvests') },

            { path: 'sales', title: 'Sales', loadComponent: () => import('./fpms/sales/sales.component').then((m) => m.SalesComponent), ...page('Farm.Sales') },
            { path: 'sales/new', title: 'Record sale', loadComponent: () => import('./fpms/sales/sale-entry.component').then((m) => m.SaleEntryComponent), ...page('Farm.Sales') },
            { path: 'sales/:id/edit', title: 'Edit sale', loadComponent: () => import('./fpms/sales/sale-entry.component').then((m) => m.SaleEntryComponent), ...page('Farm.Sales') },
            { path: 'sales/:id', title: 'Dispatch receipt', loadComponent: () => import('./fpms/sales/sale-print.component').then((m) => m.SalePrintComponent), ...page('Farm.Sales') },
            { path: 'reports/trace/:id', title: 'Trace report', loadComponent: () => import('./fpms/reports/trace-report.component').then((m) => m.TraceReportComponent), ...page('Farm.Reports') },
            { path: 'reports', title: 'Reports', loadComponent: () => import('./fpms/reports/reports.component').then((m) => m.ReportsComponent), ...page('Farm.Reports') },

            { path: 'system/farms', title: 'Farms', loadComponent: () => import('./fpms/system/system-farms.component').then((m) => m.SystemFarmsComponent), ...page('System.Farms') },
            { path: 'system/recovery', title: 'Account recovery', loadComponent: () => import('./fpms/system/recovery.component').then((m) => m.RecoveryComponent), ...page('System.AccountRecovery') },
            { path: 'system/reference', title: 'Reference data', loadComponent: () => import('./fpms/system/reference-data.component').then((m) => m.ReferenceDataComponent), ...page('System.ReferenceData') },
            { path: 'system/health', title: 'System health', loadComponent: () => import('./fpms/system/health.component').then((m) => m.HealthComponent), ...page('System.Health') },
            { path: 'system/logs', title: 'Security and error logs', loadComponent: () => import('./fpms/system/security-logs.component').then((m) => m.SecurityLogsComponent), ...page('System.Logs') },
        ],
    },
];

@NgModule({
    imports: [RouterModule.forChild(routes)],
    exports: [RouterModule],
})
export class AppRoutingModule {}
