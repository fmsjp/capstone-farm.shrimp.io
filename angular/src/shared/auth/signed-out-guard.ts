import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AppSessionService } from '../session/app-session.service';

export const signedOutGuard: CanActivateFn = () => {
    const router = inject(Router);
    return inject(AppSessionService).user ? router.parseUrl('/app') : true;
};
