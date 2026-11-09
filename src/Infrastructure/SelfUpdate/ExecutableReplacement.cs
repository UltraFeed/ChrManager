using System.Diagnostics;
using System.IO.Pipes;
using ChrManager.Application;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Ipc;
using ChrManager.Infrastructure.Network;
using ChrManager.Infrastructure.Persistence;
using Serilog;
using static ChrManager.Infrastructure.Persistence.SelfUpdateJobStore;

namespace ChrManager.Infrastructure.SelfUpdate;

internal sealed class ExecutableReplacement
{
	private readonly UpdateJob _job;
	private readonly string _jobPath;
	private readonly SelfUpdateFiles _files;
	private readonly string _target;
	private readonly string _backup;
	private readonly ILogger _log;
	public ExecutableReplacement (UpdateJob job, string jobPath, ILogger log)
	{
		_job = job;
		_jobPath = jobPath;
		_log = log;
		_files = SelfUpdateFiles.FromJobPath(jobPath);
		_target = ValidateExecutable(job, jobPath);
		_backup = _files.Backup;
	}
	public async Task<int> RunAsync (TimeSpan readinessTimeout)
	{
		try
		{
			using FileStream gate = new(_files.Gate, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
			await AnnounceReadyAsync().ConfigureAwait(false);
			await WaitForParentAsync().ConfigureAwait(false);

			if (File.Exists(_files.Abort))
			{
				Progress(_jobPath, SelfUpdatePhase.Aborted);
				return 0;
			}

			string hash = await Downloader.FileSha256Async(_files.NewExecutable, CancellationToken.None).ConfigureAwait(false);
			if (hash != _job.Sha256)
			{
				throw new AppFailure(FailureKind.SelfUpdatePackageChanged);
			}

			await ReplaceAndConfirmAsync(readinessTimeout).ConfigureAwait(false);
			return 0;
		}
		catch (Exception ex) when (OperationErrors.IsExpected(ex))
		{
			_log.Error(ex, DiagnosticKind.SelfUpdateFailed);
			Progress(_jobPath, SelfUpdatePhase.Failed, ex.GetType().Name);
			return 1;
		}
	}
	private async Task AnnounceReadyAsync ()
	{
		if (!_job.HandshakeRequired)
		{
			return;
		}

		using NamedPipeClientStream started = new(".", InternalProtocol.WorkerPipe(_job.Nonce), PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
		await started.ConnectAsync(10000).ConfigureAwait(false);
		await started.WriteAsync(new byte [] { 1 }).ConfigureAwait(false);
	}
	private async Task WaitForParentAsync ()
	{
		try
		{
			using Process parent = Process.GetProcessById(_job.ParentId);
			if (parent.StartTime.ToUniversalTime().Ticks != _job.ParentStarted)
			{
				throw new AppFailure(FailureKind.ParentProcessMismatch);
			}

			await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		}
		catch (ArgumentException) { /* The parent already exited. */ }
	}
	private async Task ReplaceAndConfirmAsync (TimeSpan timeout)
	{
		using NamedPipeServerStream readiness = new(InternalProtocol.ReadinessPipe(_job.Nonce), PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
		Progress(_jobPath, SelfUpdatePhase.BackingUp);
		File.Move(_target, _backup);
		try
		{
			Progress(_jobPath, SelfUpdatePhase.Installing);
			File.Move(_files.NewExecutable, _target);
			Progress(_jobPath, SelfUpdatePhase.Starting);
			using Process replacement = StartReplacement();
			await ConfirmReadinessAsync(replacement, readiness, timeout).ConfigureAwait(false);
			Progress(_jobPath, SelfUpdatePhase.Complete);
			_log.Information(DiagnosticKind.SelfUpdateCompleted, _job.Version);
		}
		catch
		{
			Progress(_jobPath, SelfUpdatePhase.Restoring);
			if (File.Exists(_target))
			{
				File.Delete(_target);
			}

			File.Move(_backup, _target);
			using Process restored = StartOriginal();
			throw;
		}
	}
	private static async Task ConfirmReadinessAsync (Process replacement, NamedPipeServerStream readiness, TimeSpan timeout)
	{
		try
		{
			using CancellationTokenSource deadline = new(timeout);
			await readiness.WaitForConnectionAsync(deadline.Token).ConfigureAwait(false);
			byte [] ready = new byte [1];
			if (await readiness.ReadAsync(ready, deadline.Token).ConfigureAwait(false) != 1 || ready [0] != 1)
			{
				throw new AppFailure(FailureKind.InvalidReadinessResponse);
			}
		}
		catch
		{
			if (!replacement.HasExited)
			{
				replacement.Kill();
				await replacement.WaitForExitAsync().ConfigureAwait(false);
			}

			throw;
		}
	}
	private ProcessStartInfo CreateStart ()
	{
		ProcessStartInfo start = new(_target) { UseShellExecute = false, WorkingDirectory = _job.Root };
		start.Environment [InternalProtocol.Role] = "controller";
		start.Environment [InternalProtocol.CleanupJob] = _jobPath;
		start.Environment [InternalProtocol.CleanupToken] = _job.Nonce;
		start.Environment [InternalProtocol.CleanupParent] = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
		return start;
	}
	private Process StartOriginal ()
	{
		return Process.Start(CreateStart()) ?? throw new AppFailure(FailureKind.ManagerRestartFailed);
	}
	private Process StartReplacement ()
	{
		ProcessStartInfo start = CreateStart();
		_ = start.Environment.Remove(InternalProtocol.UpdateJob);
		_ = start.Environment.Remove(InternalProtocol.UpdateToken);
		start.Environment [InternalProtocol.ResumeJob] = _jobPath;
		start.Environment [InternalProtocol.ResumeToken] = _job.Nonce;
		return Process.Start(start) ?? throw new AppFailure(FailureKind.ManagerRestartFailed);
	}
}
