using System.Text.Json;
using ChrManager.Application;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Configuration;

namespace ChrManager.Infrastructure.Persistence;

internal static class SelfUpdateJobStore
{
	public static UpdateProgress? ReadProgress (string jobPath)
	{
		string path = SelfUpdateFiles.FromJobPath(jobPath).Checkpoint;
		if (!File.Exists(path))
		{
			return null;
		}

		using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		UpdateProgress progress = JsonSerializer.Deserialize(file, AppJsonContext.Type<UpdateProgress>(ConfigLoader.Options)) ?? throw new AppFailure(FailureKind.InvalidUpdateCheckpoint, path);
		if (progress.Version != 1)
		{
			throw new AppFailure(FailureKind.InvalidUpdateCheckpoint, path);
		}

		return progress;
	}

	public static void Progress (string jobPath, SelfUpdatePhase phase, string? error = null)
	{
		AtomicJson.Write(SelfUpdateFiles.FromJobPath(jobPath).Checkpoint, new UpdateProgress(phase, error));
	}

	public static UpdateJob ValidateJob (string path, string nonce)
	{
		SelfUpdateFiles files = SelfUpdateFiles.FromJobPath(path);
		if (files.Nonce != nonce)
		{
			throw new AppFailure(FailureKind.InvalidUpdateJob, path);
		}

		PortablePaths.RejectReparsePoints(path);
		UpdateJob job = JsonSerializer.Deserialize(File.ReadAllText(path), AppJsonContext.Type<UpdateJob>(ConfigLoader.Options)) ?? throw new AppFailure(FailureKind.InvalidUpdateJob, path);
		string root = Path.GetFullPath(job.Root);
		if (job.FormatVersion != 1 || job.Nonce != nonce || !string.Equals(Path.TrimEndingDirectorySeparator(root), files.Root, StringComparison.OrdinalIgnoreCase))
		{
			throw new AppFailure(FailureKind.InvalidUpdateJob, path);
		}

		PortablePaths.RejectReparsePoints(files.NewExecutable);
		_ = ValidateExecutable(job, path);
		PortablePaths.RejectReparsePoints(files.Worker);
		PortablePaths.RejectReparsePoints(files.Backup);
		PortablePaths.RejectReparsePoints(files.Gate);
		return job;
	}

	public static string ValidateExecutable (UpdateJob job, string jobPath)
	{
		string name = job.ExecutableName;
		if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name || name.IndexOfAny(['/', '\\', ':']) >= 0 || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			throw new AppFailure(FailureKind.InvalidUpdateJob, jobPath);
		}

		string target = Path.GetFullPath(Path.Combine(job.Root, name));
		SelfUpdateFiles files = SelfUpdateFiles.FromJobPath(jobPath);
		if (string.Equals(target, files.Worker, StringComparison.OrdinalIgnoreCase) || string.Equals(target, files.NewExecutable, StringComparison.OrdinalIgnoreCase) || string.Equals(target, files.Backup, StringComparison.OrdinalIgnoreCase))
		{
			throw new AppFailure(FailureKind.InvalidUpdateJob, jobPath);
		}

		PortablePaths.RejectReparsePoints(target);
		return target;
	}
}
