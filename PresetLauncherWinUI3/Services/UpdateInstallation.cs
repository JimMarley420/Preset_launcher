using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace PresetLauncher.Services;

internal sealed record UpdateInstallation(string Directory, bool AllUsers, Version Version)
{
    internal const string LauncherName = "PresetLauncherWinUI3.exe";
    private const string LauncherAssembly = "PresetLauncherWinUI3";
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}_is1";
    private const string ArchitectureKey = @"SOFTWARE\PresetLauncher\Installation";
    private const int RestartExitCode = 3010;

    internal string LauncherPath => Path.Combine(Directory, LauncherName);

    internal static void RequireNonElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (identity.IsSystem || new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Open PresetLauncher without administrator privileges to update.");
    }

    internal static UpdateInstallation Find(string launcherPath, Architecture architecture)
    {
        List<UpdateInstallation> entries = [];
        Read(RegistryHive.CurrentUser, RegistryView.Registry32, allUsers: false);
        Read(RegistryHive.LocalMachine, RegistryView.Registry32, allUsers: true);
        if (Environment.Is64BitOperatingSystem)
            Read(RegistryHive.LocalMachine, RegistryView.Registry64, allUsers: true);

        if (entries.Count != 1 ||
            !string.Equals(entries[0].LauncherPath, launcherPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The running launcher is not the registered installation.");
        var installation = entries[0];
        var assembly = AssemblyName.GetAssemblyName(Path.Combine(installation.Directory, LauncherAssembly + ".dll"));
        var executableVersion = FileVersionInfo.GetVersionInfo(installation.LauncherPath);
        if (assembly.Name != LauncherAssembly ||
            assembly.Version?.ToString(3) != installation.Version.ToString(3) ||
            new Version(executableVersion.FileMajorPart, executableVersion.FileMinorPart, executableVersion.FileBuildPart) != installation.Version)
            throw new InvalidDataException("The registered launcher version does not match its files.");
        return installation;

        void Read(RegistryHive hive, RegistryView view, bool allUsers)
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var registration = root.OpenSubKey(UninstallKey);
            if (registration is null) return;
            using var target = root.OpenSubKey(ArchitectureKey);
            var name = target?.GetValue("PresetLauncherArchitecture") as string ?? "x64";
            if (!name.Equals(architecture.ToString(), StringComparison.OrdinalIgnoreCase) ||
                allUsers && (view == RegistryView.Registry64) != (architecture != Architecture.X86) ||
                registration.GetValue("Inno Setup: App Path") is not string location ||
                !Path.IsPathFullyQualified(location) ||
                !UpdateService.TryVersion(registration.GetValue("DisplayVersion") as string, out var version))
                throw new InvalidDataException("The installed scope, architecture or version cannot be verified.");
            entries.Add(new(Path.TrimEndingDirectorySeparator(Path.GetFullPath(location)), allUsers, version));
        }
    }

    internal ProcessStartInfo InstallerStartInfo(string installer)
    {
        var start = new ProcessStartInfo(installer)
        {
            UseShellExecute = AllUsers,
            Verb = AllUsers ? "runas" : "",
            WorkingDirectory = Environment.SystemDirectory
        };
        foreach (var argument in new[]
        {
            "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/SP-",
            $"/RESTARTEXITCODE={RestartExitCode}", "/NOCLOSEAPPLICATIONS", "/NORESTARTAPPLICATIONS",
            AllUsers ? "/ALLUSERS" : "/CURRENTUSER"
        })
            start.ArgumentList.Add(argument);
        return start;
    }

    internal static string? ResultKey(int code) => code switch
    {
        0 => null,
        1 => "update.setupStartFailed",
        2 or 5 => "update.setupCancelled",
        3 or 4 or 6 or 7 => "update.setupFailed",
        8 or RestartExitCode => "update.rebootRequired",
        _ => "update.setupUnexpected"
    };
}
