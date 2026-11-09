using ChrManager.Application;
using ChrManager.Application.Contracts;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Installation;
using ChrManager.Infrastructure.Network;
using ChrManager.Infrastructure.Persistence;
using ChrManager.Infrastructure.SelfUpdate;
using ChrManager.Infrastructure.Windows;
using ChrManager.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ChrManager.Infrastructure.Startup;

internal static class ApplicationComposition
{
	public static void RegisterManager (IServiceCollection services, AppPaths paths, ILogger log, ConsoleView view, LauncherSettings settings, UpdateJob? resume, HttpClientProvider clients)
	{
		_ = services.AddSingleton(settings);
		_ = services.AddSingleton(new UpdateResume(resume));
		_ = services.AddSingleton(paths);
		_ = services.AddSingleton(log);
		_ = services.AddSingleton<IUserInterface>(view);
		_ = services.AddSingleton(TimeProvider.System);
		_ = services.AddSingleton(clients);
		_ = services.AddSingleton<RunState>(CreateState);
		_ = services.AddSingleton<DownloadNotifications>();
		_ = services.AddSingleton<Downloader>(CreateDownloader);
		_ = services.AddSingleton<ChromePlusManager>(CreateChromePlus);
		_ = services.AddSingleton<IReleaseFeed>(CreateFeed);
		_ = services.AddSingleton<ISelfUpdatePreparation>(CreateSelfUpdate);
		_ = services.AddSingleton<ISelfUpdateHandover, SelfUpdateHandover>();
		_ = services.AddSingleton<IBrowserInstallationFactory, BrowserInstallationFactory>();
		_ = services.AddSingleton<IBrowserProcesses, BrowserProcesses>();
		_ = services.AddSingleton<BrowserUpdateOperation>();
		_ = services.AddSingleton<BrowserClosure>();
		_ = services.AddSingleton<BrowserManager>();
		_ = services.AddSingleton<SelfUpdateCoordinator>();
		_ = services.AddSingleton<ManagerPassRunner>();
		_ = services.AddSingleton<OnceMode>();
		_ = services.AddSingleton<IHostLifetime, ManagedLifetime>();
		_ = services.AddHostedService<ManagerService>();
	}

	private static RunState CreateState (IServiceProvider services)
	{
		return new();
	}

	private static Downloader CreateDownloader (IServiceProvider services)
	{
		return new(services.GetRequiredService<HttpClientProvider>().Download, services.GetRequiredService<DownloadNotifications>().ReportMismatch);
	}

	private static ChromePlusManager CreateChromePlus (IServiceProvider services)
	{
		return new(services.GetRequiredService<HttpClientProvider>().Feed, services.GetRequiredService<Downloader>(), services.GetRequiredService<AppPaths>(), services.GetRequiredService<RunState>(), services.GetRequiredService<TimeProvider>(), log: services.GetRequiredService<ILogger>());
	}

	private static IReleaseFeed CreateFeed (IServiceProvider services)
	{
		return new ReleaseFeedClient(services.GetRequiredService<HttpClientProvider>().Feed, services.GetRequiredService<RunState>(), services.GetRequiredService<TimeProvider>());
	}

	private static ISelfUpdatePreparation CreateSelfUpdate (IServiceProvider services)
	{
		SelfUpdateSource source = new(services.GetRequiredService<HttpClientProvider>().Feed);
		return new SelfUpdater(services.GetRequiredService<AppPaths>(), services.GetRequiredService<RunState>(), source, services.GetRequiredService<Downloader>(), services.GetRequiredService<TimeProvider>(), services.GetRequiredService<ILogger>());
	}
}

