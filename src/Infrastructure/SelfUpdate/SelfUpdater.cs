using System.Diagnostics;
using ChrManager.Application;
using ChrManager.Application.Models;
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
	private readonly TimeProvider _time;
	private readonly ILogger _log;
	public SelfUpdater (AppPaths paths, RunState state, ISelfUpdateSource source, Downloader downloader, TimeProvider time, ILogger log)
	{
		_paths = paths;
		_state = state;
		_source = source;
		_downloader = downloader;
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
			return null;
		}

		if (!Version.TryParse(release.Version, out _))
		{
			throw new AppFailure(FailureKind.InvalidSelfUpdateRelease);
		}

		if (VersionOrder.Compare(release.Version, AppInfo.Version) <= 0)
		{
			return null;
		}

		string expectedArch = ExpectedArchitecture();
		ValidateMetadata(release, expectedArch);
		SelfUpdateFiles files = _paths.CreateSelfUpdateFiles();
		try
		{
			await StageReleaseAsync(files, release, expectedArch, token).ConfigureAwait(false);
			return await CreateJobAsync(files, release, settings, summary, token).ConfigureAwait(false);
		}
		catch
		{
			files.Cleanup(_log);
			throw;
		}
	}
	private static string ExpectedArchitecture ()
	{
		if (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
		{
			return "arm64";
		}

		return "x64";
	}
	private static void ValidateMetadata (SelfRelease release, string expectedArch)
	{

		if (release.Rid != "win-" + expectedArch)
		{
			throw new AppFailure(FailureKind.SelfUpdateArchitectureMismatch);
		}

		if (release.Sha256.Length != 64 || !release.Sha256.All(Uri.IsHexDigit))
		{
			throw new AppFailure(FailureKind.SelfUpdateChecksumRequired);
		}
	}
	private async Task StageReleaseAsync (SelfUpdateFiles files, SelfRelease release, string expectedArch, CancellationToken token)
	{
		string executable = files.NewExecutable;
		_ = await _downloader.DownloadAsync(new Uri(release.Url), executable, release.Sha256, 0, null, token).ConfigureAwait(false);

		string architecture;
		try
		{
			architecture = BrowserResolver.Architecture(executable);
		}
		catch (BadImageFormatException ex)
		{
			throw new AppFailure(FailureKind.InvalidSelfUpdateExecutable, ex);
		}

		if (architecture != expectedArch)
		{
			throw new AppFailure(FailureKind.SelfUpdateArchitectureMismatch);
		}

		string? binaryVersion = BrowserResolver.QueryVersion(executable);
		if (!Version.TryParse(binaryVersion, out _) || VersionOrder.Compare(binaryVersion, release.Version) != 0)
		{
			throw new AppFailure(FailureKind.SelfUpdateVersionMismatch);
		}
	}

	private async Task<PreparedSelfUpdate> CreateJobAsync (SelfUpdateFiles files, SelfRelease release, LauncherSettings settings, PassSummary summary, CancellationToken token)
	{
		using Process parent = Process.GetCurrentProcess();
		UpdateJob job = new(_paths.AppDirectory, Path.GetFileName(_paths.ExecutablePath), parent.Id, parent.StartTime.ToUniversalTime().Ticks, files.Nonce, release.Version, await Downloader.FileSha256Async(files.NewExecutable, token).ConfigureAwait(false), summary, settings.ExitDelaySeconds, true)
		{
			PreviousSha256 = await Downloader.FileSha256Async(_paths.ExecutablePath, token).ConfigureAwait(false),
			Language = settings.UiLanguage
		};
		AtomicJson.Write(files.Job, job);
		return new PreparedSelfUpdate(job, files.Job);
	}
}
