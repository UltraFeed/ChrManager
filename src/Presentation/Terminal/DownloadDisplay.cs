using ChrManager.Application.Contracts;
using ChrManager.Presentation.Localization;

namespace ChrManager.Presentation.Terminal;

internal sealed class DownloadDisplay : IDownloadProgress
{
	private readonly Lock _sync = new();
	private readonly string _title;
	private readonly LocalizationProvider _language;
	private readonly LiveProgressDisplay _display;
	private DownloadProgress _progress;

	public DownloadDisplay (string title, LocalizationProvider language)
	{
		_title = title;
		_language = language;
		_display = new(title, "Download");
	}

	public void Report (DownloadProgress value)
	{
		lock (_sync)
		{
			_progress = value;
		}

		string suffix = value.Total is > 0
			? LiveProgressDisplay.Bar(value.Received * 100d / value.Total.Value)
			: _language.Format(StringId.DownloadReceived, string.Empty, value.Received);
		_display.Report(suffix);
	}

	public async ValueTask DisposeAsync ()
	{
		await _display.DisposeAsync().ConfigureAwait(false);
		DownloadProgress result;
		lock (_sync)
		{
			result = _progress;
		}

		string summary = result.Complete
			? _language.Format(StringId.DownloadFinished, _title, result.Received)
			: _language.Format(StringId.DownloadInterrupted, _title);
		_display.Write(summary);
	}
}
