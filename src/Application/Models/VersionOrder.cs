namespace ChrManager.Application.Models;

/// <summary>
/// Order of browser versions. Browser versions are four part numbers, so System.Version
/// does the work; anything else is compared as text.
/// </summary>
internal static class VersionOrder
{
	public static int Compare (string? left, string? right)
	{
		if (TryParse(left, out Version? leftVersion) && TryParse(right, out Version? rightVersion))
		{
			return leftVersion.CompareTo(rightVersion);
		}

		return string.CompareOrdinal(left?.Trim() ?? string.Empty, right?.Trim() ?? string.Empty);
	}

	/// <summary>True when <paramref name = "candidate"/> is newer than <paramref name = "current"/>.</summary>
	public static bool IsNewer (string? current, string? candidate)
	{
		return Compare(current, candidate) < 0;
	}

	private static bool TryParse (string? value, out Version version)
	{
		if (Version.TryParse(value?.Trim(), out Version? parsed))
		{
			version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
			return true;
		}

		version = new Version(0, 0);
		return false;
	}
}
