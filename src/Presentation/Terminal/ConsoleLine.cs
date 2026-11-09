using System.Globalization;
using ChrManager.Infrastructure.Startup;
using Serilog.Events;
using Spectre.Console;

namespace ChrManager.Presentation.Terminal;

internal static class ConsoleLine
{
	private static readonly string ProcessLabel = FormattableString.Invariant($"{AppInfo.Name}-{Environment.ProcessId}");

	public static string Format (DateTimeOffset timestamp, LogEventLevel level, string operation, string? message)
	{
		string time = timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		string severity = level switch
		{
			LogEventLevel.Warning => "WARN",
			LogEventLevel.Error or LogEventLevel.Fatal => "ERROR",
			LogEventLevel.Debug or LogEventLevel.Verbose => "DEBUG",
			_ => "INFO"
		};
		return $"{time}|{ProcessLabel}|{severity}|{operation}() {message}";
	}

	public static void Write (string line, LogEventLevel level)
	{
		Style style = level switch
		{
			LogEventLevel.Warning => new Style(Color.Yellow),
			LogEventLevel.Error or LogEventLevel.Fatal => new Style(Color.Red),
			_ => Style.Plain
		};

		Write(line, style);
	}

	public static void Write (string line, Style style)
	{
		AnsiConsole.Write(new Text(line + Environment.NewLine, style));
	}
}
