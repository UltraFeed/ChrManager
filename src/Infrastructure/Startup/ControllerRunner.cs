using System.Diagnostics;
using ChrManager.Application;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Configuration;
using ChrManager.Infrastructure.Diagnostics;
using ChrManager.Infrastructure.Ipc;
using ChrManager.Infrastructure.Network;
using ChrManager.Infrastructure.Persistence;
using ChrManager.Infrastructure.SelfUpdate;
using ChrManager.Infrastructure.Windows;
using ChrManager.Presentation;
using ChrManager.Presentation.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChrManager.Infrastructure.Startup;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class ControllerRunner
{
	private readonly AppPaths _paths;
	private readonly LaunchContext _context;
	public ControllerRunner (AppPaths paths, LaunchContext context)
	{
		_paths = paths;
		_context = context;
	}
	public async Task<int> RunAsync ()
	{
		await WaitForParentAsync().ConfigureAwait(false);
		using UserInstanceLease instance = new();
		if (!instance.IsOwner)
		{
			return 0;
		}

		ConsoleView view = new();
		SpectreLogSink sink = new(view.Language);
		using Serilog.Core.Logger log = AppLogging.CreateLogger(sink);
		if (!File.Exists(_paths.SettingsPath))
		{
			view.Say(StringId.MissingSettings, _paths.SettingsPath);
			await view.AcknowledgeAsync().ConfigureAwait(false);
			Environment.Exit(0);
		}

		try
		{
			LauncherSettings settings = ConfigLoader.Load(_paths.SettingsPath);
			view.Configure(new(settings.UiLanguage));
			sink.Language = view.Language;
			if (view.Language.Warning is { } warning)
			{
				view.Message(warning);
			}

			view.Message(AppInfo.FullVersion);
			using HttpClientProvider clients = new(settings.HttpTimeoutSeconds);
			UpdateJob? replacement = await SelfUpdateWorker.AcknowledgeAsync(_paths, CancellationToken.None).ConfigureAwait(false);
			UpdateResume resume = await SelfUpdateWorker.FinishHandoverAsync(_paths, replacement, log, CancellationToken.None).ConfigureAwait(false);
			if (resume.Job is null)
			{
				SettingsPreflight preflight = new(new ChromePlusReference(clients.Feed), view);
				await preflight.ValidateAsync(settings, CancellationToken.None).ConfigureAwait(false);
				_paths.EnsureWritable();
				new StartupRegistration().Apply(settings.StartAtLogin, _paths.ExecutablePath);
			}

			HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true, ContentRootPath = _paths.AppDirectory });
			_ = builder.Logging.ClearProviders();
			ApplicationComposition.RegisterManager(builder.Services, _paths, log, view, settings, resume, clients);
			using IHost host = builder.Build();
			ManagerService manager = host.Services.GetServices<IHostedService>().OfType<ManagerService>().Single();
			await host.RunAsync().ConfigureAwait(false);
			return manager.ExitCode;
		}
		catch (Exception ex) when (OperationErrors.CanReport(ex))
		{
			log.Error(ex, DiagnosticKind.ApplicationFailed);
			if (ex is SettingsException settingsError)
			{
				view.ReportError(view.Language.Format(StringId.InvalidSettings, settingsError.FilePath));
			}
			else
			{
				view.ReportError(view.Explain(ex));
			}

			await view.AcknowledgeAsync().ConfigureAwait(false);
			return 1;
		}
		finally
		{
			_paths.CleanupTemporaryFiles(log);
		}
	}
	private async Task WaitForParentAsync ()
	{
		if (_context.RestartParent is not { } parent)
		{
			return;
		}

		try
		{
			using Process process = Process.GetProcessById(parent);
			await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		}
		catch (ArgumentException) { }
	}
}
