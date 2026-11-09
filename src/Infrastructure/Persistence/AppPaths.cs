using ChrManager.Application;
using ChrManager.Infrastructure.Installation;
using ChrManager.Infrastructure.Startup;

namespace ChrManager.Infrastructure.Persistence;

internal sealed class AppPaths
{
	private readonly List<string> _temporaryPaths = [];
	private readonly List<SelfUpdateFiles> _updates = [];

	public AppPaths (string appDirectory, string? executablePath = null)
	{
		AppDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDirectory));
		ExecutablePath = Path.GetFullPath(executablePath ?? Path.Combine(AppDirectory, AppInfo.ExecutableName));
		if (!string.Equals(Path.GetDirectoryName(ExecutablePath), AppDirectory, StringComparison.OrdinalIgnoreCase))
		{
			throw new AppFailure(FailureKind.InvalidApplicationMetadata);
		}
	}

	public string AppDirectory { get; }
	public string SettingsPath => Path.Combine(AppDirectory, "settings.json");
	public string ExecutablePath { get; }

	public string CreateTemporaryFile (string purpose, string extension = ".tmp")
	{
		string path = Path.Combine(AppDirectory, $"{AppInfo.TemporaryPrefix}{Guid.NewGuid():N}.{purpose}{extension}");
		PortablePaths.RejectReparsePoints(path);
		_temporaryPaths.Add(path);
		return path;
	}

	public SelfUpdateFiles CreateSelfUpdateFiles ()
	{
		SelfUpdateFiles files = new(AppDirectory, Guid.NewGuid().ToString("N"));
		_updates.Add(files);
		return files;
	}

	public void ReleaseTemporaryFile (string path)
	{
		if (!_temporaryPaths.Contains(path))
		{
			throw new AppFailure(FailureKind.ForeignTemporaryFile, path);
		}

		PortablePaths.CheckTree(path);
		if (Directory.Exists(path))
		{
			Installer.Delete(path, false);
		}
		else
		{
			File.Delete(path);
		}

		_ = _temporaryPaths.Remove(path);
	}

	public void CleanupTemporaryFiles (Serilog.ILogger log)
	{
		foreach (string path in _temporaryPaths.ToArray())
		{
			try
			{
				ReleaseTemporaryFile(path);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				log.Warning(ex, DiagnosticKind.TemporaryCleanupFailed, path);
			}
		}

		foreach (SelfUpdateFiles files in _updates)
		{
			if (!files.IsActive)
			{
				files.Cleanup(log);
			}
		}
	}

	public static AppPaths Create () { return new(AppContext.BaseDirectory, Environment.ProcessPath ?? throw new AppFailure(FailureKind.InvalidApplicationMetadata)); }

	public BrowserPaths Browser (string folder)
	{
		PortablePaths.ValidateFolder(folder);
		return new(Path.Combine(AppDirectory, folder));
	}

	public void EnsureWritable ()
	{
		PortablePaths.RejectReparsePoints(AppDirectory);
		string probe = CreateTemporaryFile("write");
		using (FileStream file = new(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
		{
		}

		ReleaseTemporaryFile(probe);
	}
}

internal sealed record BrowserPaths (string Root)
{
	public string Directory => Path.Combine(Root, "bin");
	public string BinaryPath => Path.Combine(Directory, "chrome.exe");
	public string Profile => Path.Combine(Root, "profile");
	public string Cache => Path.Combine(Root, "cache");
	public string Stage => Path.Combine(Root, ".stage");
	public string Backup => Path.Combine(Root, ".backup");
	public bool IsInstalled => File.Exists(BinaryPath);
}

internal static class PortablePaths
{
	public static void ValidateFolder (string folder)
	{
		if (string.IsNullOrWhiteSpace(folder) || folder is "." or ".." || folder.EndsWith('.') || folder.EndsWith(' ') || folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || folder.Contains('/', StringComparison.Ordinal) || folder.Contains('\\', StringComparison.Ordinal) || Path.IsPathRooted(folder))
		{
			throw new AppFailure(FailureKind.InvalidBrowserFolder, folder);
		}

		string stem = folder.Split('.') [0].ToUpperInvariant();
		if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".Contains(stem [3], StringComparison.Ordinal)))
		{
			throw new AppFailure(FailureKind.ReservedBrowserFolder, folder);
		}
	}

	public static void RejectReparsePoints (string path)
	{
		string full = Path.GetFullPath(path);
		if (new FileInfo(full).LinkTarget is not null || (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0))
		{
			throw new AppFailure(FailureKind.ReparsePoint, full);
		}

		for (DirectoryInfo? item = new(full); item is not null; item = item.Parent)
		{
			if (item.LinkTarget is not null || (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0))
			{
				throw new AppFailure(FailureKind.ReparsePoint, item.FullName);
			}
		}
	}

	public static void CheckTree (string root)
	{
		RejectReparsePoints(root);
		if (!Directory.Exists(root))
		{
			return;
		}

		foreach (string item in Directory.EnumerateFileSystemEntries(root))
		{
			FileAttributes attributes = File.GetAttributes(item);
			if ((attributes & FileAttributes.ReparsePoint) != 0)
			{
				throw new AppFailure(FailureKind.ReparsePoint, item);
			}

			if ((attributes & FileAttributes.Directory) != 0)
			{
				CheckTree(item);
			}
		}
	}
}
