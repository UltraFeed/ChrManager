using ChrManager.Application.Contracts;
using ChrManager.Presentation;
using ChrManager.Presentation.Localization;

namespace ChrManager.Application;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class BrowserClosure
{
	private readonly IBrowserProcesses _processes;
	private readonly IUserInterface _view;
	private readonly TimeProvider _time;

	public BrowserClosure (IBrowserProcesses processes, IUserInterface view, TimeProvider time)
	{
		_processes = processes;
		_view = view;
		_time = time;
	}

	public async Task<bool> CloseAsync (string binary, CancellationToken token)
	{
		if (!_processes.IsRunning(binary))
		{
			return true;
		}

		if (!await _view.AskAsync(StringId.CloseBrowser).ConfigureAwait(false))
		{
			return false;
		}

		_processes.RequestClose(binary);
		while (_processes.IsRunning(binary))
		{
			await Task.Delay(TimeSpan.FromSeconds(5), _time, token).ConfigureAwait(false);
			if (!_processes.IsRunning(binary))
			{
				break;
			}

			BrowserCloseAction answer = await ChooseActionAsync().ConfigureAwait(false);
			if (answer == BrowserCloseAction.Postpone)
			{
				return false;
			}

			if (answer == BrowserCloseAction.ForceTerminate && await _view.AskAsync(StringId.ForceWarning).ConfigureAwait(false))
			{
				await _processes.ForceTerminateAsync(binary, token).ConfigureAwait(false);
			}
		}

		return true;
	}
	private Task<BrowserCloseAction> ChooseActionAsync ()
	{
		Choice<BrowserCloseAction> [] choices = [
			new(BrowserCloseAction.Wait,_view.Language.GetString(StringId.Wait)),
			new(BrowserCloseAction.Postpone,_view.Language.GetString(StringId.Postpone)),
			new(BrowserCloseAction.ForceTerminate,_view.Language.GetString(StringId.ForceTerminate))];
		return _view.ChooseAsync(_view.Language.GetString(StringId.BrowserRunning), choices, BrowserCloseAction.Postpone);
	}
}
