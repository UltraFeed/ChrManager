using ChrManager.Application.Contracts;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ChrManager.Application;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class ManagerService : BackgroundService
{
	private readonly ManagerPassRunner _passes;
	private readonly OnceMode _once;
	private readonly IUserInterface _view;
	private readonly IHostApplicationLifetime _lifetime;
	private readonly ILogger _log;
	public int ExitCode { get; private set; }

	public ManagerService (ManagerPassRunner passes, OnceMode once, IUserInterface view, IHostApplicationLifetime lifetime, ILogger log)
	{
		_passes = passes;
		_once = once;
		_view = view;
		_lifetime = lifetime;
		_log = log;
	}

	protected override async Task ExecuteAsync (CancellationToken stoppingToken)
	{
		try
		{
			PassExecution initial = await _passes.RunAsync().ConfigureAwait(false);
			if (initial.Failed)
			{
				ExitCode = 1;
			}

			if (initial.Handover)
			{
				return;
			}

			await _once.RunAsync(initial).ConfigureAwait(false);
		}
		catch (Exception ex) when (OperationErrors.CanReport(ex))
		{
			ExitCode = 1;
			_log.Error(ex, DiagnosticKind.ManagerFailed);

			await _view.AcknowledgeAsync().ConfigureAwait(false);
		}
		finally
		{
			_lifetime.StopApplication();
		}
	}
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class ManagedLifetime : IHostLifetime
{
	public Task WaitForStartAsync (CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}

	public Task StopAsync (CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}
