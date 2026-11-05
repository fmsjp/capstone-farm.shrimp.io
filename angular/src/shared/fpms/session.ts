import { AppConsts } from '@shared/AppConsts';

export function appUrl(path = ''): string {
    return AppConsts.appBaseUrl.replace(/\/?$/, '/') + path;
}

export function signOutLocally(): void {
    abp.auth.clearToken();
    abp.utils.deleteCookie(AppConsts.authorization.encryptedAuthTokenName);
    location.href = appUrl('account/login');
}
