using ChrManager.Infrastructure.SelfUpdate;

namespace ChrManager.Infrastructure.Startup;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class UpdateHelperRunner
{
	private readonly LaunchContext _context;
	public UpdateHelperRunner (LaunchContext context)
	{
		_context = context;
	}

	public Task<int> RunAsync ()
	{
		return SelfUpdateWorker.RunHelperAsync(_context.JobPath!, _context.Token!);
	}
}
