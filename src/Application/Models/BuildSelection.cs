namespace ChrManager.Application.Models;

internal sealed record ReleaseAsset (string Name, string Url, long Size, string? Digest);

internal static class BuildSelection
{
	public static SelectedBrowserRelease Select (IReadOnlyList<Build> builds, string id)
	{
		List<Build> matches = builds.Where(b => b.Id == id && b.Platform == "windows").ToList();
		if (matches.Count != 1)
		{
			throw new AppFailure(FailureKind.InvalidBuildSelection, id);
		}

		Build build = matches [0];
		if (!Version.TryParse(build.Version, out _))
		{
			throw new AppFailure(FailureKind.InvalidBrowserVersion);
		}

		List<BuildDownload> packages = (build.Downloads ?? []).Where(d => d.IsPortable && (d.Arch ?? (build.Arch?.Count == 1 ? build.Arch[0] : null)) == "x64").ToList();
		if (packages.Count != 1)
		{
			throw new AppFailure(FailureKind.InvalidArchiveSelection, id);
		}

		BuildDownload package = packages [0];
		return new(build, package, "x64");
	}
}
