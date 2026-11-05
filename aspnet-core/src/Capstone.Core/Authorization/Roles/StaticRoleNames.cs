namespace Capstone.Authorization.Roles;

public static class StaticRoleNames
{
    public static class Host
    {
        public const string SystemAdministrator = "SystemAdministrator";

        public const string Admin = "Admin";
    }

    public static class Tenants
    {
        public const string FarmOwner = "FarmOwner";
        public const string FarmTechnician = "FarmTechnician";
    }
}
