using ChrManager.Application.Contracts;
using ChrManager.Presentation.Localization;

namespace ChrManager.Presentation.Terminal;

internal sealed class OperationDisplay : IOperationProgress
{
	private readonly LiveProgressDisplay _display;
	private readonly LocalizationProvider _language;
	private readonly string _title;
	private bool _complete;

	public OperationDisplay (string title, LocalizationProvider language)
	{
		_title = title;
		_language = language;
		_display = new(title, "Progress");
	}

	public void Report (double value) { _display.Report(LiveProgressDisplay.Bar(value)); }
	public void Complete () { _complete = true; }

	public async ValueTask DisposeAsync ()
	{
		await _display.DisposeAsync().ConfigureAwait(false);
		StringId result = _complete ? StringId.OperationFinished : StringId.OperationInterrupted;
		_display.Write(_language.Format(result, _title));
	}
}
