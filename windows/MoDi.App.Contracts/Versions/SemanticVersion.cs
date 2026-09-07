using System.Globalization;
using System.Text.RegularExpressions;

namespace MoDi.App.Contracts;

public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private static readonly Regex Pattern = new(
        "^(?:v)?(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-(beta|rc)\\.(0|[1-9]\\d*))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private SemanticVersion(long major, long minor, long patch, string? prerelease, long? prereleaseNumber)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        PrereleaseNumber = prereleaseNumber;
    }

    public long Major { get; }
    public long Minor { get; }
    public long Patch { get; }
    public string? Prerelease { get; }
    public long? PrereleaseNumber { get; }
    public bool IsPrerelease => Prerelease is not null;
    public string ReleaseIdentity => Prerelease ?? "stable";
    public string NumericVersion => $"{Major}.{Minor}.{Patch}.0";

    public static SemanticVersion Parse(string value) =>
        TryParse(value, out var result) ? result : throw new FormatException($"Invalid MoDi semantic version: {value}");

    public static bool TryParse(string? value, out SemanticVersion result)
    {
        var match = value is null ? Match.Empty : Pattern.Match(value);
        if (!match.Success ||
            !long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor) ||
            !long.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            result = null!;
            return false;
        }
        long? prereleaseNumber = null;
        if (match.Groups[5].Success)
        {
            if (!long.TryParse(match.Groups[5].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                result = null!;
                return false;
            }
            prereleaseNumber = parsed;
        }
        result = new SemanticVersion(major, minor, patch,
            match.Groups[4].Success ? match.Groups[4].Value : null, prereleaseNumber);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;
        var core = Major.CompareTo(other.Major);
        if (core != 0) return core;
        core = Minor.CompareTo(other.Minor);
        if (core != 0) return core;
        core = Patch.CompareTo(other.Patch);
        if (core != 0) return core;
        if (!IsPrerelease && !other.IsPrerelease) return 0;
        if (!IsPrerelease) return 1;
        if (!other.IsPrerelease) return -1;
        var rank = Prerelease == "beta" ? 0 : 1;
        var otherRank = other.Prerelease == "beta" ? 0 : 1;
        var kind = rank.CompareTo(otherRank);
        return kind != 0 ? kind : PrereleaseNumber!.Value.CompareTo(other.PrereleaseNumber!.Value);
    }

    public bool Equals(SemanticVersion? other) => other is not null && CompareTo(other) == 0;
    public override bool Equals(object? obj) => obj is SemanticVersion other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, Prerelease, PrereleaseNumber);
    public override string ToString() => IsPrerelease
        ? $"{Major}.{Minor}.{Patch}-{Prerelease}.{PrereleaseNumber}"
        : $"{Major}.{Minor}.{Patch}";

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
}

