using System.Runtime.CompilerServices;
using System.Text;
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
			// Set the Windows code page before Spectre.Console captures the output writer.
			Console.OutputEncoding = new UTF8Encoding(false);
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
			LocalizationProvider language = new();
			using Serilog.Core.Logger log = AppLogging.CreateLogger(language);
			log.Error(ex, DiagnosticKind.ApplicationStartupFailed);
			if (context?.Role != LaunchRole.UpdateHelper)
			{
				await new ConsoleView(language).AcknowledgeAsync().ConfigureAwait(false);
			}

			return 1;
		}
	}
}
