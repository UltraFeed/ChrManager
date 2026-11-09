using System.Runtime.CompilerServices;
using ChrManager.Application;
using ChrManager.Infrastructure.Diagnostics;
using ChrManager.Infrastructure.Persistence;
using ChrManager.Infrastructure.Startup;
using ChrManager.Presentation;
using ChrManager.Presentation.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace ChrManager;

internal static class Program
{
	private static async Task<int> Main ()
	{
		LaunchContext? context = null;
		try
		{
			context = LaunchContext.Read();
			AppPaths paths = AppPaths.Create();
			ServiceCollection services = new();
			_ = services.AddSingleton(paths);
			_ = services.AddSingleton(context);
			_ = services.AddSingleton<ControllerRunner>();
			_ = services.AddSingleton<UpdateHelperRunner>();
			ServiceProvider provider = services.BuildServiceProvider();

			await using ConfiguredAsyncDisposable providerLifetime = provider.ConfigureAwait(false);
			if (context.Role == LaunchRole.UpdateHelper)
			{
				return await provider.GetRequiredService<UpdateHelperRunner>().RunAsync().ConfigureAwait(false);
			}

			LocalizationProvider.ValidateResources();
			return await provider.GetRequiredService<ControllerRunner>().RunAsync().ConfigureAwait(false);
		}
		catch (Exception ex) when (OperationErrors.CanReport(ex))
		{

			WriteFailureLog(ex);
			if (context?.Role != LaunchRole.UpdateHelper)
			{
				await new ConsoleView().AcknowledgeAsync().ConfigureAwait(false);
			}

			return 1;
		}
	}

	private static void WriteFailureLog (Exception exception)
	{
		using Serilog.Core.Logger log = AppLogging.CreateLogger();
		log.Error(exception, DiagnosticKind.ApplicationStartupFailed);
	}
}
