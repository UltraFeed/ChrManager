using ChrManager.Application.Contracts;
using ChrManager.Application.Models;
using ChrManager.Presentation.Localization;
using Serilog;

namespace ChrManager.Application;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class SelfUpdateCoordinator
{
	private readonly ISelfUpdatePreparation _preparation;
	private readonly ISelfUpdateHandover _handover;
	private readonly LauncherSettings _settings;
	private readonly IUserInterface _view;
	private readonly ILogger _log;
	public SelfUpdateCoordinator (ISelfUpdatePreparation preparation, ISelfUpdateHandover handover, LauncherSettings settings, IUserInterface view, ILogger log)
	{
		_preparation = preparation;
		_handover = handover;
		_settings = settings;
		_view = view;
		_log = log;
	}

	public async Task<SelfUpdateOutcome> TryApplyAsync (PassSummary summary)
	{
		try
		{
			PreparedSelfUpdate? prepared = await _preparation.PrepareAsync(_settings, summary, CancellationToken.None).ConfigureAwait(false);

			if (prepared is null)
			{
				return SelfUpdateOutcome.None;
			}

			_view.Say(StringId.SelfUpdateRestarting);
			await _handover.LaunchAsync(prepared, CancellationToken.None).ConfigureAwait(false);

			return SelfUpdateOutcome.Handover;
		}
		catch (Exception ex) when (OperationErrors.IsExpected(ex))
		{
			_log.Error(ex, DiagnosticKind.SelfUpdateFailure);

			return SelfUpdateOutcome.Failed;
		}
	}
}
