using System.Diagnostics;
using ChrManager.Application;

namespace ChrManager.Infrastructure.Windows;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the dependency injection container.")]
internal sealed class BrowserProcesses : IBrowserProcesses
{
	private static void DisposeProcesses (IEnumerable<Process> processes)
	{
		foreach (Process process in processes)
		{
			process.Dispose();
		}
	}

	private static List<Process> Find (string binary)
	{
		List<Process> found = new();
		foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(binary)))
		{
			try
			{
				if (process.HasExited)
				{
					process.Dispose();
					continue;
				}

				if (string.Equals(process.MainModule?.FileName, binary, StringComparison.OrdinalIgnoreCase))
				{
					found.Add(process);
				}
				else
				{
					process.Dispose();
				}
			}
			catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
			{
				if (process.HasExited || process.WaitForExit(100))
				{
					process.Dispose();
					continue;
				}

				process.Dispose();
				DisposeProcesses(found);
				// Unknown ownership is not permission to replace potentially used files.
				throw new AppFailure(FailureKind.BrowserProcessPathUnavailable, ex);
			}
		}

		return found;
	}

	public bool IsRunning (string binary)
	{
		List<Process> processes = Find(binary);
		try
		{
			return processes.Count != 0;
		}
		finally
		{
			DisposeProcesses(processes);
		}
	}

	public void RequestClose (string binary)
	{
		List<Process> processes = Find(binary);
		try
		{
			foreach (Process process in processes)
			{
				_ = process.CloseMainWindow();
			}
		}
		finally
		{
			DisposeProcesses(processes);
		}
	}
	public async Task ForceTerminateAsync (string binary, CancellationToken token)
	{
		List<Process> processes = Find(binary);
		try
		{
			foreach (Process process in processes)
			{
				if (!process.HasExited)
				{
					process.Kill(entireProcessTree: false);
				}

				await process.WaitForExitAsync(token).ConfigureAwait(false);
			}
		}
		finally
		{
			DisposeProcesses(processes);
		}
	}
	public bool Start (string binary)
	{
		using Process? process = Process.Start(new ProcessStartInfo(binary) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(binary)! });
		return process is not null;
	}

	public bool? IsModuleLoaded (string binary, string modulePath)
	{
		List<Process> processes = Find(binary);
		try
		{
			foreach (Process process in processes)
			{
				foreach (ProcessModule loaded in process.Modules.Cast<ProcessModule>())
				{
					if (string.Equals(loaded.FileName, modulePath, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}

			return processes.Count == 0 ? null : false;
		}
		catch (System.ComponentModel.Win32Exception)
		{
			return null;
		}
		finally
		{
			DisposeProcesses(processes);
		}
	}
}
