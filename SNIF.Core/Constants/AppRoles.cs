namespace SNIF.Core.Constants
{
    public static class AppRoles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
        public const string Moderator = "Moderator";
        public const string Support = "Support";

        public static readonly string[] All = { SuperAdmin, Admin, Moderator, Support };
    }
}
