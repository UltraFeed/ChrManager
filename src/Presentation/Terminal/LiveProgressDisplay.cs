using Spectre.Console;

namespace ChrManager.Presentation.Terminal;

internal sealed class LiveProgressDisplay : IAsyncDisposable
{
	private static readonly Style ProgressStyle = new(Color.Cyan);
	private readonly Lock _sync = new();
	private readonly string _title;
	private readonly string _operation;
	private readonly Task _worker;
	private string _suffix = string.Empty;
	private bool _closed;

	public LiveProgressDisplay (string title, string operation)
	{
		_title = title;
		_operation = operation;
		if (AnsiConsole.Profile.Capabilities is { Interactive: true, Ansi: true })
		{
			_worker = RenderAsync();
		}
		else
		{
			Write(title);
			_worker = Task.CompletedTask;
		}
	}

	public void Report (string suffix)
	{
		lock (_sync)
		{
			_suffix = suffix;
		}
	}

	public async ValueTask DisposeAsync ()
	{
		lock (_sync)
		{
			_closed = true;
		}

		await _worker.ConfigureAwait(false);
	}

	public void Write (string text)
	{
		ConsoleLine.Write(ConsoleRenderer.FormatInfo(_operation, text), ProgressStyle);
	}

	public static string Bar (double value)
	{
		const int width = 12;
		int percent = (int) Math.Clamp(value, 0, 99);
		int filled = percent * width / 100;
		char full = AnsiConsole.Profile.Capabilities.Unicode ? '━' : '=';
		char empty = AnsiConsole.Profile.Capabilities.Unicode ? '─' : '-';
		return $" {new string(full, filled)}{new string(empty, width - filled)} {percent}%";
	}

	private async Task RenderAsync ()
	{
		ConsoleLine.BeginLive();
		try
		{
			await AnsiConsole.Live(RenderLine(" |")).AutoClear(true).StartAsync(RefreshAsync).ConfigureAwait(false);
		}
		finally
		{
			ConsoleLine.EndLive();
		}
	}

	private async Task RefreshAsync (LiveDisplayContext display)
	{
		string previous = string.Empty;
		int frame = 0;
		while (true)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
			string suffix;
			lock (_sync)
			{
				if (_closed)
				{
					return;
				}

				suffix = _suffix;
			}

			if (suffix.Length == 0)
			{
				suffix = " " + "|/-\\" [frame++ % 4];
			}

			if (suffix != previous)
			{
				display.UpdateTarget(RenderLine(suffix));
				display.Refresh();
				previous = suffix;
			}
		}
	}

	private Text RenderLine (string suffix)
	{
		string prefix = ConsoleRenderer.FormatInfo(_operation, string.Empty);
		int available = Math.Max(0, AnsiConsole.Profile.Width - prefix.Length - suffix.Length);
		string title = _title;
		if (title.Length > available)
		{
			title = available < 4 ? string.Empty : title [..(available - 3)] + "...";
		}

		return new Text(prefix + title + suffix, ProgressStyle) { Overflow = Overflow.Crop };
	}
}
