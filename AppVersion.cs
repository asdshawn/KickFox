using System.Reflection;

// Single source of truth for the KickFox version.
// Compiled into every executable (KickFox, Installer, Uninstaller, Updater).
// When releasing: bump the values below AND create a GitHub tag "v" + Version (e.g. v1.1.0).
[assembly: AssemblyProduct("KickFox")]
[assembly: AssemblyCompany("asdshawn")]
[assembly: AssemblyCopyright("Copyright (c) 2026 asdshawn. MIT License.")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace KickFox
{
    public static class AppInfo
    {
        public const string Version = "1.1.0";
        public const string GitHubRepo = "asdshawn/KickFox";
        public const string UpdateAssetName = "Update.zip";
        public const string SignatureAssetName = "Update.zip.sig";
    }
}