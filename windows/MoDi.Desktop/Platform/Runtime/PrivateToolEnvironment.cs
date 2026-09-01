using System;
using System.Diagnostics;
using System.IO;

namespace MoDi.Desktop.Platform.Runtime;

/// <summary>Only child processes are changed. Never writes host environment or user tool configuration.</summary>
internal static class PrivateToolEnvironment
{
    internal static void Apply(ProcessStartInfo start, string stateRoot, params string[] executableDirectories)
    {
        var root = Path.GetFullPath(stateRoot);
        var temp = Path.Combine(root, "tmp");
        var profile = Path.Combine(root, "home");
        Directory.CreateDirectory(temp);
        Directory.CreateDirectory(profile);
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        start.Environment.Clear();
        start.Environment["SystemRoot"] = windows;
        start.Environment["WINDIR"] = windows;
        start.Environment["ComSpec"] = Path.Combine(system, "cmd.exe");
        start.Environment["PATH"] = string.Join(Path.PathSeparator, executableDirectories) + Path.PathSeparator + system;
        start.Environment["TEMP"] = temp;
        start.Environment["TMP"] = temp;
        start.Environment["HOME"] = profile;
        start.Environment["USERPROFILE"] = profile;
        start.Environment["APPDATA"] = profile;
        start.Environment["LOCALAPPDATA"] = profile;
        start.Environment["XDG_CONFIG_HOME"] = profile;
        start.Environment["ANDROID_USER_HOME"] = Path.Combine(profile, ".android");
        start.Environment["ANDROID_EMULATOR_HOME"] = Path.Combine(profile, ".android");
        start.Environment["ANDROID_SDK_HOME"] = profile;
    }

    internal static string RequireFile(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Private tools require an absolute path.", nameof(path));
        if (!File.Exists(path)) throw new FileNotFoundException("应用内置组件缺失，请修复安装；不会使用系统同名工具。", path);
        return path;
    }
}

