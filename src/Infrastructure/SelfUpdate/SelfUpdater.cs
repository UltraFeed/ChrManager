using System.Diagnostics;
using ChrManager.Application;
using ChrManager.Application.Models;
using ChrManager.Application.Contracts;
using ChrManager.Presentation.Localization;
using ChrManager.Infrastructure.Installation;
using ChrManager.Infrastructure.Network;
using ChrManager.Infrastructure.Persistence;
using ChrManager.Infrastructure.Startup;
using Serilog;

namespace ChrManager.Infrastructure.SelfUpdate;

internal sealed class SelfUpdater : ISelfUpdatePreparation
{
	private readonly AppPaths _paths;
	private readonly RunState _state;
	private readonly ISelfUpdateSource _source;
	private readonly Downloader _downloader;
	private readonly IUserInterface _view;
	private readonly TimeProvider _time;
	private readonly ILogger _log;
	public SelfUpdater (AppPaths paths, RunState state, ISelfUpdateSource source, Downloader downloader, IUserInterface view, TimeProvider time, ILogger log)
	{
		_paths = paths;
		_state = state;
		_source = source;
		_downloader = downloader;
		_view = view;
		_time = time;
		_log = log;
	}

	public async Task<PreparedSelfUpdate?> PrepareAsync (LauncherSettings settings, PassSummary summary, CancellationToken token)
	{
		if (!settings.SelfUpdateEnabled)
		{
			return null;
		}

		DateTimeOffset now = _time.GetUtcNow();
		if (_state.State.LastSelfUpdateAttempt is { } previous && now - previous < TimeSpan.FromDays(1))
		{
			return null;
		}

		_state.Save(_state.State with { LastSelfUpdateAttempt = now });
		SelfRelease? release = await _source.LatestAsync(token).ConfigureAwait(false);
		if (release is null)
		{
			_view.Message(_view.Language.GetString(StringId.SelfUpdateNoRelease));
			return null;
		}

		if (!Version.TryParse(release.Version, out _))
		{
			throw new AppFailure(FailureKind.InvalidSelfUpdateRelease);
		}

		if (VersionOrder.Compare(release.Version, AppInfo.Version) <= 0)
		{
			_view.Message(_view.Language.Format(StringId.SelfUpdateCurrent, AppInfo.Version));
			return null;
		}

		SelfReleaseAsset asset = release.Asset ?? throw new AppFailure(FailureKind.SelfUpdateAssetMissing, AppInfo.ExecutableName);
		_view.Message(_view.Language.Format(StringId.SelfUpdateAvailable, AppInfo.Version, release.Version));
		SelfUpdateFiles files = _paths.CreateSelfUpdateFiles();
		try
		{
			await StageReleaseAsync(files, release.Version, asset, token).ConfigureAwait(false);
			return await CreateJobAsync(files, release, settings, summary, token).ConfigureAwait(false);
		}
		catch
		{
			files.Cleanup(_log);
			throw;
		}
	}

	private async Task StageReleaseAsync (SelfUpdateFiles files, string version, SelfReleaseAsset asset, CancellationToken token)
	{
		string executable = files.NewExecutable;
		IDownloadProgress progress = _view.BeginDownload(_view.Language.Format(StringId.SelfUpdateDownloading, version));
		await using (progress.ConfigureAwait(false))
		{
			_ = await _downloader.DownloadAsync(new Uri(asset.Url), executable, asset.Sha256, 0, progress, token).ConfigureAwait(false);
		}

		bool isX64;
		try
		{
			isX64 = BrowserResolver.IsX64(executable);
		}
		catch (BadImageFormatException ex)
		{
			throw new AppFailure(FailureKind.InvalidSelfUpdateExecutable, ex);
		}

		if (!isX64)
		{
			throw new AppFailure(FailureKind.SelfUpdateArchitectureMismatch);
		}

		string? binaryVersion = BrowserResolver.QueryVersion(executable);
		if (!Version.TryParse(binaryVersion, out _) || VersionOrder.Compare(binaryVersion, version) != 0)
		{
			throw new AppFailure(FailureKind.SelfUpdateVersionMismatch);
		}
	}

	private async Task<PreparedSelfUpdate> CreateJobAsync (SelfUpdateFiles files, SelfRelease release, LauncherSettings settings, PassSummary summary, CancellationToken token)
	{
		using Process parent = Process.GetCurrentProcess();
		UpdateJob job = new(_paths.AppDirectory, Path.GetFileName(_paths.ExecutablePath), parent.Id, parent.StartTime.ToUniversalTime().Ticks, files.Nonce, release.Version, await Downloader.FileSha256Async(files.NewExecutable, token).ConfigureAwait(false), summary, settings.ExitDelaySeconds, true)
		{
			PreviousVersion = AppInfo.Version,
			PreviousSha256 = await Downloader.FileSha256Async(_paths.ExecutablePath, token).ConfigureAwait(false),
			Language = settings.UiLanguage
		};
		AtomicJson.Write(files.Job, job);
		return new PreparedSelfUpdate(job, files.Job);
	}
}
