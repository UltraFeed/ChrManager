using ChrManager.Application.Contracts;
using ChrManager.Application.Models;
using ChrManager.Presentation;
using ChrManager.Presentation.Localization;

namespace ChrManager.Application;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class ManagerPassRunner
{
	private readonly BrowserManager _browsers;
	private readonly SelfUpdateCoordinator _selfUpdate;
	private readonly LauncherSettings _settings;
	private readonly IUserInterface _view;
	private readonly UpdateResume _resume;
	public ManagerPassRunner (BrowserManager browsers, SelfUpdateCoordinator selfUpdate, LauncherSettings settings, IUserInterface view, UpdateResume resume)
	{
		_browsers = browsers;
		_selfUpdate = selfUpdate;
		_settings = settings;
		_view = view;
		_resume = resume;
	}

	public async Task<PassExecution> RunAsync ()
	{
		PassSummary summary;
		if (_resume.Job is not null)
		{
			summary = _resume.Job.Summary;
			if (_resume.Phase == SelfUpdatePhase.RolledBack)
			{
				_view.ReportError(_view.Language.Format(StringId.SelfUpdateRolledBack, _resume.Job.PreviousVersion));
			}
			else
			{
				_view.Say(StringId.SelfUpdateCompleted, _resume.Job.PreviousVersion, _resume.Job.Version);
			}
		}
		else
		{
			summary = await _browsers.RunAsync(_settings, CancellationToken.None).ConfigureAwait(false);
		}

		_view.Say(StringId.Summary, summary.Succeeded, summary.Deferred, summary.Failed);
		if (summary.Failed > 0 || _resume.Phase == SelfUpdatePhase.RolledBack)
		{
			return new(summary, PassOutcome.Failed);
		}

		if (_resume.Job is not null)
		{
			return new(summary, PassOutcome.Ready);
		}

		SelfUpdateOutcome update = await _selfUpdate.TryApplyAsync(summary).ConfigureAwait(false);
		PassOutcome outcome = update switch { SelfUpdateOutcome.Handover => PassOutcome.Handover, SelfUpdateOutcome.Failed => PassOutcome.Failed, _ => PassOutcome.Ready };
		return new(summary, outcome);
	}
}
