using ChrManager.Application;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Network;
using ChrManager.Infrastructure.Persistence;

namespace ChrManager.Infrastructure.Installation;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class BrowserInstallationFactory : IBrowserInstallationFactory
{
	private readonly AppPaths _paths;
	private readonly RunState _state;
	private readonly Downloader _downloader;
	private readonly ChromePlusManager _chromePlus;
	private readonly TimeProvider _time;
	public BrowserInstallationFactory (AppPaths paths, RunState state, Downloader downloader, ChromePlusManager chromePlus, TimeProvider time)
	{
		_paths = paths;
		_state = state;
		_downloader = downloader;
		_chromePlus = chromePlus;
		_time = time;
	}

	public async Task<IManagedBrowserInstallation> OpenAsync (BrowserSettings browser, IReadOnlyList<Build> builds, CancellationToken token)
	{
		BrowserPaths target = _paths.Browser(browser.Folder);
		PortablePaths.CheckTree(target.Root);
		_ = Directory.CreateDirectory(target.Root);
		_ = Directory.CreateDirectory(target.Profile);
		_ = Directory.CreateDirectory(target.Cache);
		BrowserMemory memory = _state.Browser(browser.Folder);
		SelectedBrowserRelease? release = null;
		if (builds.Count > 0)
		{
			release = BuildSelection.Select(builds, browser.BuildId);
		}

		string? version = null;
		if (target.IsInstalled)
		{
			version = BrowserResolver.QueryVersion(target.BinaryPath) ?? memory.InstalledVersion;
		}

		bool upgrade = release is { } candidate && (!target.IsInstalled || VersionOrder.IsNewer(version, candidate.Build.Version));
		string? architecture = release?.Architecture;
		if (!upgrade && target.IsInstalled)
		{
			architecture = BrowserResolver.Architecture(target.BinaryPath);
		}

		if (architecture is null)
		{
			throw new AppFailure(FailureKind.BrowserUnavailable);
		}

		string configHash = ChromePlusManager.ConfigurationHash(browser);
		string ini = Path.Combine(target.Directory, "chrome++.ini");
		string dll = Path.Combine(target.Directory, "version.dll");
		ChromePayload payload = await _chromePlus.GetPayloadAsync(architecture, token, dll).ConfigureAwait(false);
		if (File.Exists(ini) && File.Exists(dll) && ChromePlusManager.IsGeneratedIni(await File.ReadAllTextAsync(ini, token).ConfigureAwait(false)))
		{
			memory = memory with { ChromePlusHash = await Downloader.FileSha256Async(dll, token).ConfigureAwait(false) };
		}

		bool configured = false;
		if (target.IsInstalled)
		{
			configured = await VerifyConfigurationAsync(target, browser, payload, token).ConfigureAwait(false);
		}

		string offeredVersion = release?.Build.Version ?? version ?? "0.0";
		BrowserUpdatePlan plan = new(target.BinaryPath, offeredVersion, upgrade, !upgrade && configured, release is { } item && string.IsNullOrWhiteSpace(item.Package.Sha256), Path.Combine(target.Directory, "version.dll"));
		return new ManagedBrowserInstallation(_paths, _state, _downloader, _time, browser, target, memory, release, version, payload, configHash, plan);
	}

	private static async Task<bool> VerifyConfigurationAsync (BrowserPaths paths, BrowserSettings browser, ChromePayload payload, CancellationToken token)
	{
		string dll = Path.Combine(paths.Directory, "version.dll");
		string ini = Path.Combine(paths.Directory, "chrome++.ini");
		if (!File.Exists(dll) || !File.Exists(ini))
		{
			return false;
		}

		if (await Downloader.FileSha256Async(dll, token).ConfigureAwait(false) != payload.Hash)
		{
			return false;
		}

		return await File.ReadAllTextAsync(ini, token).ConfigureAwait(false) == ChromePlusManager.RenderIni(browser, payload);
	}
}
