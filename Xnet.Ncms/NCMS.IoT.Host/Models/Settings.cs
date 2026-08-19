namespace NCMS.IoT.Host.Models;

/// <summary>
/// Lightweight app-wide UI constants used by the shared layout/partials.
/// (Replaces the old SmartAdmin template settings object.)
/// </summary>
public static class Settings
{
    public const string App = "NCMS";
    public const string Version = "1.0.0";
    public const string Logo = "/logo/xnet-logo-transparent.png";

    public static class Features
    {
        public const bool AppSidebar = true;
    }
}
