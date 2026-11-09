using ChrManager.Application;
using ChrManager.Application.Contracts;
using ChrManager.Presentation;
using ChrManager.Presentation.Localization;
using Serilog;

namespace ChrManager.Infrastructure.Startup;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class DownloadNotifications
{
	private readonly ILogger _log;
	private readonly IUserInterface _view;
	public DownloadNotifications (ILogger log, IUserInterface view)
	{
		_log = log;
		_view = view;
	}

	public void ReportMismatch (long expected, long actual)
	{
		_log.Warning(DiagnosticKind.CatalogueSizeMismatch, expected, actual);
		_view.Say(StringId.MetadataMismatch, expected, actual);
	}
}
