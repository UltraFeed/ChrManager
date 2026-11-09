using ChrManager.Application.Contracts;
using ChrManager.Application.Models;
using ChrManager.Presentation.Localization;

namespace ChrManager.Application;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class OnceMode
{
	private readonly IUserInterface _view;
	private readonly LauncherSettings _settings;
	private readonly TimeProvider _time;
	public OnceMode (IUserInterface view, LauncherSettings settings, TimeProvider time)
	{
		_view = view;
		_settings = settings;
		_time = time;
	}

	public async Task RunAsync (PassExecution result)
	{
		if (result.Failed)
		{
			await _view.AcknowledgeAsync().ConfigureAwait(false);
			return;
		}

		await ExitDelayAsync(_settings.ExitDelaySeconds, _time, _view.Countdown, _view.AcknowledgeAsync, _view.Language).ConfigureAwait(false);
	}

	public static async Task ExitDelayAsync (int seconds, TimeProvider time, Action<string> display, Func<Task> acknowledge, LocalizationProvider language)
	{
		if (seconds == -1)
		{
			await acknowledge().ConfigureAwait(false);
			return;
		}

		for (int remaining = seconds; remaining > 0; remaining--)
		{
			display(language.Format(StringId.Countdown, remaining));
			await Task.Delay(TimeSpan.FromSeconds(1), time).ConfigureAwait(false);
		}
	}
}
