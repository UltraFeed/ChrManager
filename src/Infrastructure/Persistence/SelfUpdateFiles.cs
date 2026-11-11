using ChrManager.Application;
using ChrManager.Infrastructure.Installation;
using ChrManager.Infrastructure.Startup;
using Serilog;

namespace ChrManager.Infrastructure.Persistence;

internal sealed record SelfUpdateFiles (string Root, string Nonce)
{
	private string Prefix => Path.Combine(Root, AppInfo.TemporaryPrefix + Nonce);
	public string Job => Prefix + ".update.json";
	public string Checkpoint => Prefix + ".checkpoint.json";
	public string Abort => Prefix + ".abort.json";
	public string NewExecutable => Prefix + ".new.exe";
	public string Backup => Prefix + ".old.exe";
	public string Worker => Prefix + ".worker.exe";
	public string Gate => Path.Combine(Root, AppInfo.Name + ".self-update.lock");
	public bool IsActive => FileOperations.IsInUse(Worker) || FileOperations.IsInUse(Gate);

	public static SelfUpdateFiles FromJobPath (string path)
	{
		string full = Path.GetFullPath(path);
		string name = Path.GetFileName(full);
		string prefix = AppInfo.TemporaryPrefix;
		const string suffix = ".update.json";
		if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal) || name.Length != prefix.Length + 32 + suffix.Length)
		{
			throw new AppFailure(FailureKind.InvalidUpdateJob, path);
		}

		string nonce = name [prefix.Length..^suffix.Length];
		if (!Guid.TryParseExact(nonce, "N", out Guid parsed) || nonce != parsed.ToString("N"))
		{
			throw new AppFailure(FailureKind.InvalidUpdateJob, path);
		}

		return new(Path.GetDirectoryName(full)!, nonce);
	}

	public void Cleanup (ILogger log)
	{
		string [] paths = [NewExecutable, Backup, Worker, Job, Checkpoint, Abort, Job + ".tmp", Checkpoint + ".tmp", Abort + ".tmp"];
		foreach (string path in paths)
		{
			try
			{
				PortablePaths.RejectReparsePoints(path);
				File.Delete(path);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				log.Warning(ex, DiagnosticKind.TemporaryCleanupFailed, path);
			}
		}
	}
}
