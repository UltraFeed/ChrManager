using ChrManager.Application.Contracts;
using ChrManager.Application.Models;
using ChrManager.Presentation;
using ChrManager.Presentation.Localization;

namespace ChrManager.Application;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class BrowserUpdateOperation
{
	private readonly IBrowserInstallationFactory _installations;
	private readonly IBrowserProcesses _processes;
	private readonly IUserInterface _view;
	private readonly TimeProvider _time;
	private readonly BrowserClosure _closure;
	public BrowserUpdateOperation (IBrowserInstallationFactory installations, IBrowserProcesses processes, IUserInterface view, TimeProvider time, BrowserClosure closure)
	{
		_installations = installations;
		_processes = processes;
		_view = view;
		_time = time;
		_closure = closure;
	}

	public async Task<BrowserUpdateResult> UpdateAsync (BrowserSettings browser, IReadOnlyList<Build> builds, CancellationToken token)
	{
		using IManagedBrowserInstallation installation = await _installations.OpenAsync(browser, builds, token).ConfigureAwait(false);

		BrowserUpdatePlan plan = installation.Plan;
		if (plan.Configured)
		{
			_view.Say(StringId.UpToDate, browser.Folder);
			return new(BrowserUpdateStatus.Ready);
		}

		await PrepareAsync(browser, installation, token).ConfigureAwait(false);

		bool wasRunning = _processes.IsRunning(plan.BinaryPath);
		if (!await _closure.CloseAsync(plan.BinaryPath, token).ConfigureAwait(false))
		{
			return new(BrowserUpdateStatus.Deferred);
		}

		if (_processes.IsRunning(plan.BinaryPath) || installation.IsLocked())
		{
			throw new AppFailure(FailureKind.BrowserFilesInUse);
		}

		token.ThrowIfCancellationRequested();

		installation.Commit();
		_view.Say(StringId.Installed, browser.Folder);

		await OfferRestartAsync(browser, plan, wasRunning, token).ConfigureAwait(false);
		return new(BrowserUpdateStatus.Ready);
	}

	private async Task PrepareAsync (BrowserSettings browser, IManagedBrowserInstallation installation, CancellationToken token)
	{
		if (installation.Plan.Upgrade)
		{
			if (installation.Plan.NoChecksum)
			{
				_view.Say(StringId.NoChecksum, browser.Folder);
			}

			IDownloadProgress download = _view.BeginDownload(_view.Language.Format(StringId.Downloading, browser.Folder, installation.Plan.Version));
			await using (download.ConfigureAwait(false))
			{
				await installation.DownloadAsync(download, token).ConfigureAwait(false);
			}

			using IOperationProgress progress = _view.BeginOperation(_view.Language.Format(StringId.Extracting, browser.Folder));
			await installation.ExtractAsync(progress, token).ConfigureAwait(false);
		}
		else
		{
			installation.CopyToStage();
		}

		await installation.ConfigureStageAsync(token).ConfigureAwait(false);
	}

	private async Task OfferRestartAsync (BrowserSettings browser, BrowserUpdatePlan plan, bool wasRunning, CancellationToken token)
	{
		if (!wasRunning || !await _view.AskAsync(StringId.RestartBrowser, browser.Folder).ConfigureAwait(false))
		{
			return;
		}

		if (!_processes.Start(plan.BinaryPath))
		{
			return;
		}

		await Task.Delay(TimeSpan.FromSeconds(2), _time, token).ConfigureAwait(false);
		bool? loaded = _processes.IsModuleLoaded(plan.BinaryPath, plan.ModulePath);
		StringId message = StringId.ModuleUnknown;
		if (loaded == true)
		{
			message = StringId.ModuleLoaded;
		}
		else if (loaded == false)
		{
			message = StringId.ModuleNotLoaded;
		}

		_view.Say(message, browser.Folder);
	}
}
