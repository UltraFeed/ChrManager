namespace ChrManager.Infrastructure.Ipc;

internal static class InternalProtocol
{
	public const string InstanceIdentity = "ChrManager";
	public const string UpdateJob = "CHRMANAGER_UPDATE_JOB";
	public const string UpdateToken = "CHRMANAGER_UPDATE_TOKEN";
	public const string Role = "CHRMANAGER_ROLE";
	public const string RestartWait = "CHRMANAGER_RESTART_WAIT";
	public const string ResumeJob = "CHRMANAGER_RESUME_JOB";
	public const string ResumeToken = "CHRMANAGER_RESUME_TOKEN";
	public const string CleanupJob = "CHRMANAGER_CLEANUP_JOB";
	public const string CleanupToken = "CHRMANAGER_CLEANUP_TOKEN";
	public const string CleanupParent = "CHRMANAGER_CLEANUP_PARENT";
	public static string WorkerPipe (string nonce)
	{
		return "chrmanager-worker-" + nonce;
	}
	public static string ReadinessPipe (string nonce)
	{
		return "chrmanager-ready-" + nonce;
	}
}
