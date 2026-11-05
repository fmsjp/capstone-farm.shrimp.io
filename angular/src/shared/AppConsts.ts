export class AppConsts {
    static readonly tenancyNamePlaceHolderInUrl = '{TENANCY_NAME}';

    static remoteServiceBaseUrl: string;
    static appBaseUrl: string;
    static appBaseHref: string;

    static localeMappings: any = [];

    static readonly userManagement = {
        defaultAdminUserName: 'admin',
    };

    static readonly localization = {
        defaultLocalizationSourceName: 'Capstone',
    };

    static readonly authorization = {
        encryptedAuthTokenName: 'enc_auth_token',
    };
}
