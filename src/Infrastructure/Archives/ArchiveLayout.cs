using ChrManager.Application;
using ChrManager.Infrastructure.Persistence;

namespace ChrManager.Infrastructure.Archives;

/// <summary>One entry of a package: its path inside the archive, its kind and its size.</summary>
internal sealed record EntryInfo (string Path, bool IsDirectory, long Size);

/// <summary>
/// Turns the paths of an archive into paths on disk. Chromium packages keep everything in
/// one top level directory, and that directory is dropped. Absolute paths, directory
/// escapes and entries that would land outside the target directory are refused.
/// </summary>
internal static class ArchiveLayout
{
	public static string NormalizePath (string? rawPath)
	{
		if (string.IsNullOrWhiteSpace(rawPath) || rawPath.StartsWith('/') || rawPath.StartsWith('\\'))
		{
			throw new AppFailure(FailureKind.ArchivePathInvalid);
		}

		List<string> segments = new();
		foreach (string segment in rawPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
		{
			if (segment == ".")
			{
				continue;
			}

			// "..", a drive letter or anything else that could leave the target directory
			PortablePaths.ValidateFolder(segment);
			segments.Add(segment);
		}

		return string.Join('/', segments);
	}

	/// <summary>The single top level directory shared by all entries; empty when entries are at the top level.</summary>
	public static string FindRootDirectory (IReadOnlyList<EntryInfo> entries)
	{
		List<EntryInfo> files = entries.Where(e => !e.IsDirectory).ToList();
		if (files.Count == 0 || files.Any(e => !e.Path.Contains('/', StringComparison.Ordinal)))
		{
			return string.Empty;
		}

		string root = files [0].Path.Split('/') [0];
		return entries.All(e => e.Path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase) || (e.IsDirectory && e.Path.Equals(root, StringComparison.OrdinalIgnoreCase))) ? root : string.Empty;
	}

	/// <summary>The path of an entry relative to the dropped root directory, null when the entry is the root itself.</summary>
	public static string? MapEntryPath (EntryInfo entry, string root)
	{
		if (entry.Path.Length == 0)
		{
			return null;
		}

		if (root.Length == 0)
		{
			return entry.Path;
		}

		if (entry.Path.Equals(root, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		return entry.Path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase) ? entry.Path [(root.Length + 1)..] : entry.Path;
	}

	/// <summary>Absolute destination inside the target directory; null when the entry would escape it.</summary>
	public static string? ResolveDestination (string targetDirectory, string relativePath)
	{
		if (relativePath.Length == 0)
		{
			return null;
		}

		string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));
		string candidate = Path.GetFullPath(Path.Combine(target, relativePath.Replace('/', Path.DirectorySeparatorChar)));
		if (candidate.Equals(target, StringComparison.OrdinalIgnoreCase))
		{
			return candidate;
		}

		return candidate.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? candidate : null;
	}
}
