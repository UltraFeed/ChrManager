using ChrManager.Application;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Persistence;

namespace ChrManager.Infrastructure.Installation;

internal sealed class Installer
{
	private readonly RunState _state;
	private readonly Action<InstallationPhase>? _checkpoint;
	public Installer (RunState state, Action<InstallationPhase>? checkpoint = null)
	{
		_state = state;
		_checkpoint = checkpoint;
	}

	public void Recover (string folder, BrowserPaths paths)
	{
		PortablePaths.CheckTree(paths.Root);
		BrowserMemory memory = _state.Browser(folder);
		InstallJournal? journal = memory.Journal;
		if (journal is null)
		{
			if (Directory.Exists(paths.Backup))
			{
				throw new AppFailure(FailureKind.UntrackedBackup);
			}

			return;
		}

		if (journal.Phase == InstallationPhase.Committed)
		{
			if (!paths.IsInstalled)
			{
				throw new AppFailure(FailureKind.CommittedBrowserMissing);
			}

			Delete(paths.Backup, true);
			Delete(paths.Stage, false);
			_state.Browser(folder, memory with { Journal = null });
			return;
		}

		// Any incomplete swap returns to the previous complete installation.
		if (Directory.Exists(paths.Backup))
		{
			Delete(paths.Directory, false);
			Directory.Move(paths.Backup, paths.Directory);
		}
		else if (!journal.HadOld)
		{
			Delete(paths.Directory, false);
		}
		else if (!Directory.Exists(paths.Directory))
		{
			throw new AppFailure(FailureKind.BrowserAndBackupMissing);
		}

		Delete(paths.Stage, false);
		_state.Browser(folder, memory with { Journal = null });
	}

	public void Commit (string folder, BrowserPaths paths, string version, string buildId, string? chromeHash, string configHash)
	{
		PortablePaths.CheckTree(paths.Root);
		if (!File.Exists(Path.Combine(paths.Stage, "chrome.exe")))
		{
			throw new AppFailure(FailureKind.PackageMissing);
		}

		if (Directory.Exists(paths.Backup))
		{
			throw new AppFailure(FailureKind.UnrecoveredBackup);
		}

		BrowserMemory old = _state.Browser(folder);
		InstallJournal journal = new(InstallationPhase.Prepared, version, buildId, chromeHash, configHash, Directory.Exists(paths.Directory));
		_state.Browser(folder, old with { Journal = journal });
		_checkpoint?.Invoke(InstallationPhase.Prepared);
		try
		{
			if (journal.HadOld)
			{
				Directory.Move(paths.Directory, paths.Backup);
			}

			_checkpoint?.Invoke(InstallationPhase.OldMoved);
			_state.Browser(folder, old with { Journal = journal with { Phase = InstallationPhase.OldMoved } });
			Directory.Move(paths.Stage, paths.Directory);
			_checkpoint?.Invoke(InstallationPhase.NewMoved);
			_state.Browser(folder, old with { InstalledVersion = version, BuildId = buildId, Pending = null, ChromePlusHash = chromeHash, ConfigurationHash = configHash, Journal = journal with { Phase = InstallationPhase.Committed } });
		}
		catch
		{
			Recover(folder, paths);
			throw;
		}

		Recover(folder, paths);
	}

	public static void Delete (string path, bool recycle)
	{
		if (!Directory.Exists(path))
		{
			return;
		}

		PortablePaths.CheckTree(path);
		FileOperations.DeleteDirectory(path, recycle);
		if (Directory.Exists(path))
		{
			throw new AppFailure(FailureKind.RemovalCancelled, path);
		}
	}
}
