using ChrManager.Application.Contracts;
using ChrManager.Application.Models;
using ChrManager.Presentation;
using ChrManager.Presentation.Localization;
using Serilog;

namespace ChrManager.Application;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class BrowserManager
{
	private readonly IReleaseFeed _feed;
	private readonly BrowserUpdateOperation _operation;
	private readonly IUserInterface _view;
	private readonly ILogger _log;
	public BrowserManager (IReleaseFeed feed, BrowserUpdateOperation operation, IUserInterface view, ILogger log)
	{
		_feed = feed;
		_operation = operation;
		_view = view;
		_log = log;
	}

	public async Task<PassSummary> RunAsync (LauncherSettings settings, CancellationToken token)
	{
		if (!settings.Browsers.Any(b => b.Enabled))
		{
			_view.Say(StringId.NoBrowsers);
			return new(0, 0, 0);
		}

		IReadOnlyList<Build> builds = [];
		int failed = 0;
		try
		{
			using IOperationProgress progress = _view.BeginOperation(_view.Language.GetString(StringId.CheckFeed));
			builds = await _feed.GetBuildsAsync(token).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
		{
			failed++;
			_log.Warning(ex, DiagnosticKind.BrowserCatalogueFailed);

			_view.ReportError(_view.Language.GetString(StringId.FeedFailure));
		}

		int ready = 0;
		int deferred = 0;
		foreach (BrowserSettings browser in settings.Browsers.Where(b => b.Enabled))
		{
			token.ThrowIfCancellationRequested();

			BrowserUpdateResult result = await UpdateOneAsync(browser, builds, token).ConfigureAwait(false);
			switch (result.Status)
			{
				case BrowserUpdateStatus.Ready:
					ready++;
					break;
				case BrowserUpdateStatus.Deferred:
					deferred++;
					break;
				case BrowserUpdateStatus.Failed:
					failed++;
					break;
			}
		}

		return new(ready, failed, deferred);
	}

	private async Task<BrowserUpdateResult> UpdateOneAsync (BrowserSettings browser, IReadOnlyList<Build> builds, CancellationToken token)
	{
		try
		{
			_view.Say(StringId.CheckingBrowser, browser.Folder);
			return await _operation.UpdateAsync(browser, builds, token).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
		{

			_view.ReportError(_view.Language.Format(StringId.BrowserFailure, browser.Folder) + " " + _view.Explain(ex));
			_log.Error(ex, DiagnosticKind.BrowserFailed, browser.Folder);
			return new(BrowserUpdateStatus.Failed, ex);
		}
	}
}
