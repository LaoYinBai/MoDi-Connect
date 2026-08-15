using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace MoDi.Architecture.Tests;

public sealed class RepositoryLicenseBoundaryTests
{
    [Fact]
    public void Repository_license_verifier_covers_every_tracked_file()
    {
        var verifier = RepositoryLayout.Resolve("scripts/license/Verify-RepositoryLicenses.ps1");
        Assert.True(File.Exists(verifier), $"Missing repository license verifier: {verifier}");

        var startInfo = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = RepositoryLayout.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(verifier);
        startInfo.ArgumentList.Add("-RepositoryRoot");
        startInfo.ArgumentList.Add(RepositoryLayout.Root);

        using var process = Process.Start(startInfo)!;
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"Repository license verification failed.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
        Assert.Contains("unmapped=0", standardOutput, StringComparison.Ordinal);
        Assert.Contains("ambiguous=0", standardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void License_map_declares_the_required_application_and_third_party_boundaries()
    {
        var mapPath = RepositoryLayout.Resolve("license-map.v1.json");
        Assert.True(File.Exists(mapPath), $"Missing repository license map: {mapPath}");

        using var map = JsonDocument.Parse(File.ReadAllBytes(mapPath));
        var root = map.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("GPL-3.0-or-later", root.GetProperty("default").GetProperty("spdxExpression").GetString());
        Assert.Equal("Copyright (C) 2026 Silvite", root.GetProperty("default").GetProperty("copyright").GetString());

        var overrides = root.GetProperty("overrides").EnumerateArray()
            .ToDictionary(item => item.GetProperty("pathPrefix").GetString()!, StringComparer.Ordinal);
        Assert.Equal(
            "LicenseRef-MoDi-Proprietary-1.0 AND LicenseRef-MoDi-Binary-Redistribution-Grant-1.0 AND LicenseRef-MoDi-GPL-Linking-Exception-1.0",
            overrides["third_party/modi-protocol/"].GetProperty("spdxExpression").GetString());
        Assert.Equal("Apache-2.0", overrides["android/gradle/wrapper/"].GetProperty("spdxExpression").GetString());
        Assert.Equal("BSD-3-Clause", overrides["android/app/libs/concentus-1.0.1.jar"].GetProperty("spdxExpression").GetString());
    }

    [Fact]
    public void Pinned_local_Concentus_jar_has_its_matching_BSD_notice()
    {
        var jar = RepositoryLayout.Resolve("android/app/libs/concentus-1.0.1.jar");
        var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(jar))).ToLowerInvariant();
        Assert.Equal("288f4f1e646943d9a616188e8fd82d6e8f4f475d7f024409c5fdb7fa8fc12618", actualHash);

        var notice = RepositoryLayout.Resolve("android/app/libs/concentus-1.0.1.LICENSE.txt");
        Assert.True(File.Exists(notice), $"Missing Concentus license notice: {notice}");
        Assert.Equal(
            File.ReadAllBytes(RepositoryLayout.Resolve("LICENSES/BSD-3-Clause-Concentus.txt")),
            File.ReadAllBytes(notice));
    }
}
