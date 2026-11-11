using ChrManager.Application.Contracts;
using ChrManager.Presentation.Localization;
using Spectre.Console;

namespace ChrManager.Presentation.Terminal;

internal sealed class DownloadDisplay : IDownloadProgress
{
	private static readonly Style DownloadStyle = new(Color.Cyan);
	private readonly Lock _sync = new();
	private readonly string _title;
	private readonly LocalizationProvider _language;
	private readonly Task _live;
	private DownloadProgress _progress;
	private bool _closed;

	public DownloadDisplay (string title, LocalizationProvider language)
	{
		_title = title;
		_language = language;
		if (AnsiConsole.Profile.Capabilities is { Interactive: true, Ansi: true })
		{
			_live = RenderAsync();
		}
		else
		{
			Write(title);
			_live = Task.CompletedTask;
		}
	}

	public void Report (DownloadProgress value)
	{
		lock (_sync)
		{
			_progress = value;
		}
	}

	public async ValueTask DisposeAsync ()
	{
		DownloadProgress result;
		lock (_sync)
		{
			_closed = true;
			result = _progress;
		}

		await _live.ConfigureAwait(false);
		string summary = result.Complete
			? _language.Format(StringId.DownloadFinished, _title, result.Received)
			: _language.Format(StringId.DownloadInterrupted, _title);
		Write(summary);
	}

	private async Task RenderAsync ()
	{
		ConsoleLine.BeginLive();
		try
		{
			await AnsiConsole.Live(RenderLine(_title, string.Empty)).AutoClear(true).StartAsync(RefreshAsync).ConfigureAwait(false);
		}
		finally
		{
			ConsoleLine.EndLive();
		}
	}

	private async Task RefreshAsync (LiveDisplayContext display)
	{
		DownloadProgress previous = default;
		while (true)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
			DownloadProgress current;
			lock (_sync)
			{
				if (_closed)
				{
					return;
				}

				current = _progress;
			}

			if (current != previous)
			{
				display.UpdateTarget(Describe(current));
				display.Refresh();
				previous = current;
			}
		}
	}

	private Text Describe (DownloadProgress progress)
	{
		if (progress.Total is not > 0)
		{
			return RenderLine(_title, _language.Format(StringId.DownloadReceived, string.Empty, progress.Received));
		}

		const int width = 12;
		double percent = progress.Complete ? 100 : Math.Min(99, progress.Received * 100d / progress.Total.Value);
		int filled = (int) (percent * width / 100);
		char full = AnsiConsole.Profile.Capabilities.Unicode ? '━' : '=';
		char empty = AnsiConsole.Profile.Capabilities.Unicode ? '─' : '-';
		return RenderLine(_title, $" {new string(full, filled)}{new string(empty, width - filled)} {percent:0}%");
	}

	private static Text RenderLine (string title, string suffix)
	{
		string prefix = Format(string.Empty);
		int available = Math.Max(0, AnsiConsole.Profile.Width - prefix.Length - suffix.Length);
		if (title.Length > available)
		{
			title = available < 4 ? string.Empty : title[..(available - 3)] + "...";
		}

		return new Text(prefix + title + suffix, DownloadStyle) { Overflow = Overflow.Crop };
	}

	private static string Format (string text) { return ConsoleRenderer.FormatInfo("Download", text); }
	private static void Write (string text) { ConsoleLine.Write(Format(text), DownloadStyle); }
}
