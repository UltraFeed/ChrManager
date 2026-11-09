using Serilog;
using Serilog.Events;

namespace ChrManager.Application;

internal enum DiagnosticKind
{
	ApplicationStartupFailed,
	ApplicationFailed,
	ManagerFailed,
	BrowserCatalogueFailed,
	BrowserFailed,
	SelfUpdateFailed,
	SelfUpdateCompleted,
	TemporaryCleanupFailed,
	ChromePlusMetadataFailed,
	ChromePlusChecking,
	ChromePlusDownloading,
	GitHubFallback,
	CatalogueSizeMismatch
}

internal static class DiagnosticLog
{
	private const string Template = "{Diagnostic} {Arguments}";

	public static void Error (this ILogger log, Exception exception, DiagnosticKind kind, params object? [] arguments)
	{
		log.Write(LogEventLevel.Error, exception, Template, kind, arguments);
	}

	public static void Warning (this ILogger log, Exception exception, DiagnosticKind kind, params object? [] arguments)
	{
		log.Write(LogEventLevel.Warning, exception, Template, kind, arguments);
	}

	public static void Warning (this ILogger log, DiagnosticKind kind, params object? [] arguments)
	{
		log.Write(LogEventLevel.Warning, Template, kind, arguments);
	}

	public static void Information (this ILogger log, DiagnosticKind kind, params object? [] arguments)
	{
		log.Write(LogEventLevel.Information, Template, kind, arguments);
	}

	public static void Debug (this ILogger log, Exception exception, DiagnosticKind kind)
	{
		log.Write(LogEventLevel.Debug, exception, Template, kind, Array.Empty<object>());
	}
}
