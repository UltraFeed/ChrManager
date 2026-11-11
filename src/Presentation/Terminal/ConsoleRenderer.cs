using ChrManager.Application;
using Serilog.Events;
using Spectre.Console;

namespace ChrManager.Presentation.Terminal;

internal sealed record ConsoleEvent (string Kind, string? Text = null, double Value = 0);

internal sealed class ConsoleRenderer
{
	private readonly Lock _sync = new();
	public void Render (ConsoleEvent packet)
	{
		lock (_sync)
		{
			switch (packet.Kind)
			{
				case "line":
					ConsoleLine.Write(FormatInfo("Countdown", packet.Text), new Style(Color.Yellow));
					break;
				case "progress":
					ConsoleLine.Write(FormatInfo("Progress", FormatProgress(packet)), new Style(Color.Cyan));
					break;
				case "phase":
					ConsoleLine.Write(FormatInfo("Progress", packet.Text), new Style(Color.Cyan));
					break;
				case "message":
					ConsoleLine.Write(FormatInfo("Message", packet.Text), LogEventLevel.Information);
					break;
				case "error":
					WriteError(packet.Text!);
					break;
				default:
					throw new AppFailure(FailureKind.InvalidConsolePacket, packet.Kind);
			}
		}
	}

	private static void WriteError (string message)
	{
		ConsoleLine.Write(ConsoleLine.Format(DateTimeOffset.Now, LogEventLevel.Error, "Message", message), LogEventLevel.Error);
	}

	public static string FormatInfo (string operation, string? text)
	{
		return ConsoleLine.Format(DateTimeOffset.Now, LogEventLevel.Information, operation, text);
	}

	private static string FormatProgress (ConsoleEvent packet)
	{
		const int barWidth = 12;
		double percent = Math.Clamp(packet.Value, 0, 100);
		int filled = (int) (percent * barWidth / 100);
		return $"{packet.Text} {new string('━', filled)}{new string('─', barWidth - filled)} {percent:0}%";
	}
}
