import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { RegisterComponent } from './register/register.component';
import { AccountComponent } from './account.component';
import { ChangePasswordComponent } from './change-password/change-password.component';
import { signedOutGuard } from '@shared/auth/signed-out-guard';

@NgModule({
    imports: [
        RouterModule.forChild([
            {
                path: '',
                component: AccountComponent,
                children: [
                    { path: 'login', title: 'Sign in', component: LoginComponent, canActivate: [signedOutGuard] },
                    {
                        path: 'register',
                        title: 'Register a farm',
                        component: RegisterComponent,
                        canActivate: [signedOutGuard],
                    },
                    { path: 'change-password', title: 'Change password', component: ChangePasswordComponent },
                    { path: '', redirectTo: 'login', pathMatch: 'full' },
                ],
            },
        ]),
    ],
    exports: [RouterModule],
})
export class AccountRoutingModule {}
