using System.Diagnostics;
using System.IO.Pipes;
using ChrManager.Application;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Diagnostics;
using ChrManager.Infrastructure.Ipc;
using ChrManager.Infrastructure.Persistence;
using ChrManager.Infrastructure.Startup;
using ChrManager.Presentation;
using Serilog.Core;
using static ChrManager.Infrastructure.Persistence.SelfUpdateJobStore;

namespace ChrManager.Infrastructure.SelfUpdate;

internal static class SelfUpdateWorker
{
	public static async Task LaunchHelperAsync (UpdateJob job, string jobPath, CancellationToken token)
	{
		using NamedPipeServerStream started = new(InternalProtocol.WorkerPipe(job.Nonce), PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
		string worker = SelfUpdateFiles.FromJobPath(jobPath).Worker;
		File.Copy(ValidateExecutable(job, jobPath), worker, true);
		ProcessStartInfo start = new(worker)
		{
			UseShellExecute = false,
			WorkingDirectory = job.Root
		};
		start.Environment [InternalProtocol.UpdateJob] = jobPath;
		start.Environment [InternalProtocol.UpdateToken] = job.Nonce;

		using Process helper = Process.Start(start) ?? throw new AppFailure(FailureKind.UpdateHelperStartFailed);
		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
		timeout.CancelAfter(TimeSpan.FromSeconds(10));
		await started.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
		byte [] ready = new byte [1];
		if (await started.ReadAsync(ready, timeout.Token).ConfigureAwait(false) != 1 || ready [0] != 1)
		{
			throw new AppFailure(FailureKind.UpdateLockFailed);
		}
	}

	public static async Task<int> RunHelperAsync (string jobPath, string nonce, TimeSpan? readinessTimeout = null)
	{
		UpdateJob job = ValidateJob(jobPath, nonce);
		using Logger log = AppLogging.CreateLogger(new SpectreLogSink(new(job.Language)));
		ExecutableReplacement replacement = new(job, jobPath, log);
		return await replacement.RunAsync(readinessTimeout ?? TimeSpan.FromSeconds(30)).ConfigureAwait(false);
	}
	public static async Task<UpdateJob?> AcknowledgeAsync (AppPaths paths, CancellationToken token)
	{
		string? path = Environment.GetEnvironmentVariable(InternalProtocol.ResumeJob);
		string? nonce = Environment.GetEnvironmentVariable(InternalProtocol.ResumeToken);
		Environment.SetEnvironmentVariable(InternalProtocol.ResumeJob, null);
		Environment.SetEnvironmentVariable(InternalProtocol.ResumeToken, null);
		if (path is null || nonce is null)
		{
			return null;
		}

		UpdateJob job = ValidateJob(path, nonce);
		if (!string.Equals(Path.GetFullPath(job.Root), paths.AppDirectory, StringComparison.OrdinalIgnoreCase) || VersionOrder.Compare(job.Version, AppInfo.Version) != 0 || !string.Equals(ValidateExecutable(job, path), paths.ExecutablePath, StringComparison.OrdinalIgnoreCase))
		{
			throw new AppFailure(FailureKind.UpdateResumeMismatch);
		}

		using NamedPipeClientStream pipe = new(".", InternalProtocol.ReadinessPipe(nonce), PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
		await pipe.ConnectAsync(25000, token).ConfigureAwait(false);
		await pipe.WriteAsync(new byte [] { 1 }, token).ConfigureAwait(false);
		return job;
	}

	public static async Task<UpdateResume> FinishHandoverAsync (AppPaths paths, UpdateJob? replacement, Serilog.ILogger log, CancellationToken token)
	{
		string? path = Environment.GetEnvironmentVariable(InternalProtocol.CleanupJob);
		string? nonce = Environment.GetEnvironmentVariable(InternalProtocol.CleanupToken);
		string? parent = Environment.GetEnvironmentVariable(InternalProtocol.CleanupParent);
		Environment.SetEnvironmentVariable(InternalProtocol.CleanupJob, null);
		Environment.SetEnvironmentVariable(InternalProtocol.CleanupToken, null);
		Environment.SetEnvironmentVariable(InternalProtocol.CleanupParent, null);
		if (path is null && nonce is null && parent is null && replacement is null)
		{
			return new(null, null);
		}

		if (path is null || nonce is null || !int.TryParse(parent, System.Globalization.CultureInfo.InvariantCulture, out int helperId) || helperId == Environment.ProcessId)
		{
			throw new AppFailure(FailureKind.InvalidUpdateHandover);
		}

		UpdateJob job = ValidateJob(path, nonce);
		if (!string.Equals(Path.GetFullPath(job.Root), paths.AppDirectory, StringComparison.OrdinalIgnoreCase))
		{
			throw new AppFailure(FailureKind.InvalidUpdateJob, path);
		}

		SelfUpdateFiles files = SelfUpdateFiles.FromJobPath(path);
		try
		{
			using Process helper = Process.GetProcessById(helperId);
			try
			{
				if (!helper.HasExited && !string.Equals(helper.MainModule?.FileName, files.Worker, StringComparison.OrdinalIgnoreCase))
				{
					throw new AppFailure(FailureKind.UpdateHelperMismatch, files.Worker);
				}

				await helper.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
			}
			catch (Exception ex) when (helper.HasExited && ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
		}
		catch (ArgumentException) { /* The helper already exited. */ }

		SelfUpdatePhase? phase = ReadProgress(path)?.Phase;
		SelfUpdatePhase expected = replacement is null ? SelfUpdatePhase.RolledBack : SelfUpdatePhase.Complete;
		string version = replacement is null ? job.PreviousVersion : job.Version;
		if (phase != expected || (replacement is not null && job.Nonce != replacement.Nonce) ||
			!string.Equals(ValidateExecutable(job, path), paths.ExecutablePath, StringComparison.OrdinalIgnoreCase) ||
			VersionOrder.Compare(version, AppInfo.Version) != 0)
		{
			throw new AppFailure(FailureKind.InvalidUpdateHandover);
		}

		files.Cleanup(log);
		return new(job, phase);
	}
}
