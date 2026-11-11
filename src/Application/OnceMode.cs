using ChrManager.Application.Contracts;
using ChrManager.Application.Models;

namespace ChrManager.Application;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class OnceMode
{
	private readonly IUserInterface _view;
	private readonly LauncherSettings _settings;
	private readonly TimeProvider _time;
	private readonly UpdateResume _resume;
	public OnceMode (IUserInterface view, LauncherSettings settings, TimeProvider time, UpdateResume resume)
	{
		_view = view;
		_settings = settings;
		_time = time;
		_resume = resume;
	}

	public async Task RunAsync (PassExecution result)
	{
		int seconds = _resume.Job?.ExitDelaySeconds ?? _settings.ExitDelaySeconds;
		if (result.Failed || seconds == -1)
		{
			await _view.AcknowledgeAsync().ConfigureAwait(false);
			return;
		}

		for (int remaining = seconds; remaining > 0; remaining--)
		{
			_view.Countdown(remaining);
			await Task.Delay(TimeSpan.FromSeconds(1), _time).ConfigureAwait(false);
		}
	}
}
