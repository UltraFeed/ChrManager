using ChrManager.Application;
using ChrManager.Presentation.Localization;
using ChrManager.Presentation.Terminal;
using Serilog.Core;
using Serilog.Events;

namespace ChrManager.Presentation;

internal sealed class SpectreLogSink : ILogEventSink
{
	private readonly LocalizationProvider _language;

	public SpectreLogSink (LocalizationProvider language)
	{
		_language = language;
	}

	public void Emit (LogEvent logEvent)
	{
		DiagnosticKind? kind = logEvent.Properties.TryGetValue("Diagnostic", out LogEventPropertyValue? diagnostic) && diagnostic is ScalarValue { Value: DiagnosticKind value } ? value : null;
		object? [] arguments = logEvent.Properties.TryGetValue("Arguments", out LogEventPropertyValue? values) && values is SequenceValue sequence ? sequence.Elements.Select(Unwrap).ToArray() : [];
		string operation = kind?.ToString() ?? "Diagnostic";
		string message = kind is { } diagnosticKind ? _language.Format(diagnosticKind, arguments) : _language.GetString(StringId.GenericFailure);
		ConsoleLine.Write(ConsoleLine.Format(logEvent.Timestamp, logEvent.Level, operation, message), logEvent.Level);
		for (Exception? current = logEvent.Exception; current is not null; current = current.InnerException)
		{
			string details = _language.Format(StringId.ExceptionDetails, current.GetType().Name, _language.Explain(current));
			ConsoleLine.Write(ConsoleLine.Format(logEvent.Timestamp, logEvent.Level, operation, details), logEvent.Level);
			if (current.StackTrace is { } stack)
			{
				ConsoleLine.Write(stack, logEvent.Level);
			}
		}
	}

	private static object? Unwrap (LogEventPropertyValue value) { return (value as ScalarValue)?.Value; }
}
