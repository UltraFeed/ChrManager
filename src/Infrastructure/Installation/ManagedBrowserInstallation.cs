using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using ChrManager.Application;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Archives;
using ChrManager.Infrastructure.Network;
using ChrManager.Infrastructure.Persistence;

namespace ChrManager.Infrastructure.Installation;

internal sealed class ManagedBrowserInstallation : IManagedBrowserInstallation
{
	private readonly AppPaths _paths;
	private readonly RunState _state;
	private readonly Downloader _downloader;
	private readonly TimeProvider _time;
	private readonly BrowserSettings _browser;
	private readonly BrowserPaths _target;
	private readonly BrowserMemory _memory;
	private readonly string? _version;
	private readonly ChromePayload _payload;
	private readonly string _configHash;
	public BrowserUpdatePlan Plan { get; }

	public ManagedBrowserInstallation (AppPaths paths, RunState state, Downloader downloader, TimeProvider time, BrowserSettings browser, BrowserPaths target, BrowserMemory memory, SelectedBrowserRelease? release, string? version, ChromePayload payload, string configHash, BrowserUpdatePlan plan)
	{
		_paths = paths;
		_state = state;
		_downloader = downloader;
		_time = time;
		_browser = browser;
		_target = target;
		_memory = memory;
		Release = release;
		_version = version;
		_payload = payload;
		_configHash = configHash;
		Plan = plan;
		PackagePath = paths.CreateTemporaryFile("browser-package", ".package");
	}

	[AllowNull]
	private SelectedBrowserRelease Release => field ?? throw new AppFailure(FailureKind.BrowserReleaseMissing);

	private static string PackageIdentity (Build build, BuildDownload package, string architecture)
	{
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{build.Id}|{build.Version}|{architecture}|{package.Simd}|{package.Url}|{package.Sha256}")));
	}

	private string RelativePackage => Path.GetFileName(PackagePath);
	private string PackagePath { get; }

	public async Task DownloadAsync (IProgress<double> progress, CancellationToken token)
	{
		string identity = PackageIdentity(Release.Build, Release.Package, Release.Architecture);
		string? recordedHash = null;
		if (_memory.Pending?.Identity == identity)
		{
			recordedHash = _memory.Pending.Sha256;
		}

		string actual = await _downloader.DownloadAsync(new Uri(Release.Package.Url), PackagePath, Release.Package.Sha256 ?? recordedHash, Release.Package.Size, progress, token).ConfigureAwait(false);
		PendingPackage pending = new(identity, RelativePackage, actual, Release.Build.Version, _browser.BuildId, new FileInfo(PackagePath).Length);
		_state.Browser(_browser.Folder, _state.Browser(_browser.Folder) with { Pending = pending });
	}

	public async Task ExtractAsync (IProgress<double> progress, CancellationToken token)
	{
		Installer.Delete(_target.Stage, false);
		await ArchiveExtractor.ExtractAsync(PackagePath, _target.Stage, progress, token).ConfigureAwait(false);
		string binary = Path.Combine(_target.Stage, "chrome.exe");
		if (!File.Exists(binary))
		{
			throw new AppFailure(FailureKind.PackageMissing);
		}

		if (BrowserResolver.Architecture(binary) != Release.Architecture)
		{
			throw new AppFailure(FailureKind.ArchitectureMismatch);
		}

		File.Delete(PackagePath);
	}

	public void CopyToStage ()
	{
		Installer.Delete(_target.Stage, false);
		CopyTree(_target.Directory, _target.Stage);
	}

	private static void CopyTree (string source, string target)
	{
		PortablePaths.CheckTree(source);
		_ = Directory.CreateDirectory(target);
		foreach (string file in Directory.GetFiles(source))
		{
			File.Copy(file, Path.Combine(target, Path.GetFileName(file)), false);
		}

		foreach (string directory in Directory.GetDirectories(source))
		{
			CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)));
		}
	}

	public Task ConfigureStageAsync (CancellationToken token)
	{
		return ChromePlusManager.ApplyAsync(_target.Stage, _browser, _payload, _memory.ChromePlusHash, token);
	}

	public bool IsLocked ()
	{
		return FileOperations.IsInUse(_target.BinaryPath);
	}

	public void Dispose ()
	{
		try
		{
			Installer.Delete(_target.Stage, false);
		}
		finally
		{
			_paths.ReleaseTemporaryFile(PackagePath);
			if (_payload.Temporary)
			{
				_paths.ReleaseTemporaryFile(_payload.File);
			}
		}
	}

	public void Commit ()
	{
		string version = _version ?? _memory.InstalledVersion ?? "0.0";
		if (Plan.Upgrade)
		{
			version = Release.Build.Version;
		}

		new Installer(_state).Commit(_browser.Folder, _target, version, _browser.BuildId, _payload.Hash, _configHash);
	}
}
