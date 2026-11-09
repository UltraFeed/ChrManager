using ChrManager.Application;
using ChrManager.Infrastructure.Ipc;
namespace ChrManager.Infrastructure.Startup;

internal enum LaunchRole
{
	Bootstrap,
	Controller,
	UpdateHelper
}

internal sealed record LaunchContext (LaunchRole Role, string? JobPath, string? Token, int? RestartParent)
{
	public static LaunchContext Read ()
	{
		string? job = Take(InternalProtocol.UpdateJob);
		string? token = Take(InternalProtocol.UpdateToken);
		string? role = Take(InternalProtocol.Role);
		int? restart = null;
		if (int.TryParse(Take(InternalProtocol.RestartWait), out int parent))
		{
			restart = parent;
		}

		LaunchRole selected = role switch
		{
			null => LaunchRole.Bootstrap,
			"controller" => LaunchRole.Controller,
			_ => throw new AppFailure(FailureKind.InvalidInternalRole)
		};
		if (job is not null && token is not null)
		{
			selected = LaunchRole.UpdateHelper;
		}

		return new(selected, job, token, restart);
	}

	private static string? Take (string name)
	{
		string? value = Environment.GetEnvironmentVariable(name);
		Environment.SetEnvironmentVariable(name, null);
		return value;
	}
}
