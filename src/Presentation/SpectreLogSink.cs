using ChrManager.Application;
using ChrManager.Presentation.Localization;
using ChrManager.Presentation.Terminal;
using Serilog.Core;
using Serilog.Events;

namespace ChrManager.Presentation;

internal sealed class SpectreLogSink : ILogEventSink
{
	public LocalizationProvider Language { get; set; }

	public SpectreLogSink (LocalizationProvider? language = null)
	{
		Language = language ?? new(null);
	}

	public void Emit (LogEvent logEvent)
	{
		DiagnosticKind? kind = logEvent.Properties.TryGetValue("Diagnostic", out LogEventPropertyValue? diagnostic) && diagnostic is ScalarValue { Value: DiagnosticKind value } ? value : null;
		StringId id = kind switch
		{
			DiagnosticKind.ApplicationStartupFailed => StringId.ApplicationStartupFailed,
			DiagnosticKind.ApplicationFailed => StringId.ApplicationFailed,
			DiagnosticKind.ManagerFailed => StringId.ManagerFailed,
			DiagnosticKind.BrowserCatalogueFailed => StringId.FeedFailure,
			DiagnosticKind.BrowserFailed => StringId.BrowserFailure,
			DiagnosticKind.SelfUpdateFailed => StringId.SelfUpdateFailure,
			DiagnosticKind.SelfUpdateCompleted => StringId.SelfUpdateCompleted,
			DiagnosticKind.TemporaryCleanupFailed => StringId.TemporaryCleanupFailed,
			DiagnosticKind.ChromePlusMetadataFailed => StringId.ChromePlusMetadataFailed,
			DiagnosticKind.ChromePlusChecking => StringId.ChromePlusChecking,
			DiagnosticKind.ChromePlusDownloading => StringId.ChromePlusDownloading,
			DiagnosticKind.GitHubFallback => StringId.GitHubFallback,
			DiagnosticKind.CatalogueSizeMismatch => StringId.MetadataMismatch,
			_ => StringId.GenericFailure
		};
		object? [] arguments = logEvent.Properties.TryGetValue("Arguments", out LogEventPropertyValue? values) && values is SequenceValue sequence ? sequence.Elements.Select(Unwrap).ToArray() : [];
		string operation = kind?.ToString() ?? "Diagnostic";
		ConsoleLine.Write(ConsoleLine.Format(logEvent.Timestamp, logEvent.Level, operation, Language.Format(id, arguments)), logEvent.Level);
		for (Exception? current = logEvent.Exception; current is not null; current = current.InnerException)
		{
			string details = Language.Format(StringId.ExceptionDetails, current.GetType().Name, ViewText.Explain(current, Language));
			ConsoleLine.Write(ConsoleLine.Format(logEvent.Timestamp, logEvent.Level, operation, details), logEvent.Level);
			if (current.StackTrace is { } stack)
			{
				ConsoleLine.Write(stack, logEvent.Level);
			}
		}
	}

	private static object? Unwrap (LogEventPropertyValue value) { return (value as ScalarValue)?.Value; }
}
