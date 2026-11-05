import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { signedOutGuard } from '@shared/auth/signed-out-guard';

const routes: Routes = [
    {
        path: '',
        pathMatch: 'full',
        canActivate: [signedOutGuard],
        loadComponent: () => import('welcome/welcome.component').then((m) => m.WelcomeComponent),
    },
    { path: 'welcome', redirectTo: '', pathMatch: 'full' },
    {
        path: 'account',
        loadChildren: () => import('account/account.module').then((m) => m.AccountModule),
        data: { preload: true },
    },
    {
        path: 'app',
        loadChildren: () => import('app/app.module').then((m) => m.AppModule),
        data: { preload: true },
    },
    { path: '**', redirectTo: '' },
];

@NgModule({
    imports: [RouterModule.forRoot(routes)],
    exports: [RouterModule],
    providers: [],
})
export class RootRoutingModule {}
