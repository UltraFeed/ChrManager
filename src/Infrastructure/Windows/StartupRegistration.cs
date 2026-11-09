using ChrManager.Application;
using ChrManager.Infrastructure.Startup;
using SharpShellLink;
using SharpShellLink.Structures;

namespace ChrManager.Infrastructure.Windows;

internal sealed class StartupRegistration
{
	private readonly string? _startupDirectory;

	public StartupRegistration (string? startupDirectory = null)
	{
		_startupDirectory = startupDirectory;
	}

	public void Apply (bool enabled, string executable)
	{
		string directory = _startupDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.Startup);
		string path = Path.Combine(directory, AppInfo.Name + ".lnk");
		if (File.Exists(path))
		{
			CheckOwnership(path);
		}
		else if (!enabled)
		{
			return;
		}

		if (!enabled)
		{
			File.Delete(path);
			return;
		}

		string target = Path.GetFullPath(executable);
		// The library's environment-variable block holds at most 259 UTF-16 characters.
		if (target.Length > 259)
		{
			throw new AppFailure(FailureKind.StartupTargetTooLong, target);
		}

		Shortcut shortcut = Shortcut.CreateShortcut(target);
		shortcut.StringData = new StringData(true)
		{
			NameString = AppInfo.Name,
			WorkingDir = Path.GetDirectoryName(target) ?? throw new AppFailure(FailureKind.StartupTargetInvalid, target),
			CommandLineArguments = ""
		};
		_ = Directory.CreateDirectory(directory);
		shortcut.WriteToFile(path);
	}

	private static void CheckOwnership (string path)
	{
		Shortcut shortcut = Shortcut.ReadFromFile(path);
		string? target = shortcut.ExtraData.EnvironmentVariableDataBlock?.TargetUnicode;
		if (string.IsNullOrEmpty(target))
		{
			target = shortcut.LinkInfo?.LocalBasePathUnicode ?? shortcut.LinkInfo?.LocalBasePath;
		}

		if (shortcut.StringData?.NameString != AppInfo.Name || string.IsNullOrEmpty(target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			throw new AppFailure(FailureKind.ForeignStartupShortcut);
		}
	}
}
