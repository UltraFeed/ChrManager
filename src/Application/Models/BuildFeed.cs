namespace ChrManager.Application.Models;

/// <summary>Response of https://chromiumbuilds.org/api/v1/windows.json.</summary>
internal sealed record BuildFeed (DateTimeOffset? GeneratedAt, string? Platform, IReadOnlyList<Build> Builds);

internal sealed record Build (string Id, string? Project, string? Name, string? Maintainer, string? Platform, IReadOnlyList<string>? Arch, string? Channel, string Version, string? Revision, DateTimeOffset? Released, string? Freshness, string? Google, bool ProprietaryCodecs, bool Hevc, bool Widevine, string? Simd, string? Page, string? ReleaseNotes, IReadOnlyList<BuildDownload>? Downloads)
{
	/// <summary>Human readable identity of the build ("Official Chromium snapshot").</summary>
	public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name!;
}

internal sealed record BuildDownload (string? Kind, string? Label, string? Filename, string Url, long Size, string? Sha256, string? Arch, string? Simd)
{
	/// <summary>
	/// Only a portable package can be unpacked into the browser directory: an installer
	/// is a program and chrmanager never runs programs it downloaded.
	/// </summary>
	public bool IsPortable => Kind is "portable" or "archive";
}

internal sealed record SelectedBrowserRelease (Build Build, BuildDownload Package, string Architecture);
