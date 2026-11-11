using ChrManager.Application;
using Serilog;

namespace ChrManager.Infrastructure.Startup;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class DownloadNotifications
{
	private readonly ILogger _log;
	public DownloadNotifications (ILogger log)
	{
		_log = log;
	}

	public void ReportMismatch (long expected, long actual)
	{
		_log.Warning(DiagnosticKind.MetadataMismatch, expected, actual);
	}
}
