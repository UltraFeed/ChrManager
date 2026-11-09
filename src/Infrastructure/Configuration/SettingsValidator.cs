using ChrManager.Application;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Persistence;

namespace ChrManager.Infrastructure.Configuration;

internal static class SettingsValidator
{
	public static void Validate (LauncherSettings settings)
	{
		if (settings.ExitDelaySeconds is < -1 or > 86400)
		{
			throw new AppFailure(FailureKind.SettingOutOfRange, "exitDelaySeconds", -1, 86400);
		}

		if (settings.HttpTimeoutSeconds is < 1 or > 300)
		{
			throw new AppFailure(FailureKind.SettingOutOfRange, "httpTimeoutSeconds", 1, 300);
		}

		if (settings.UiLanguage is not null && (string.IsNullOrWhiteSpace(settings.UiLanguage) || settings.UiLanguage == "auto"))
		{
			throw new AppFailure(FailureKind.InvalidUiLanguage);
		}

		Require(settings.Browsers, "browsers");
		HashSet<string> folders = new(StringComparer.OrdinalIgnoreCase);
		for (int index = 0; index < settings.Browsers.Count; index++)
		{
			BrowserSettings browser = settings.Browsers [index];
			string path = $"browsers[{index}]";
			Require(browser, path);
			Require(browser.Folder, path + ".folder");
			Require(browser.BuildId, path + ".buildId");
			PortablePaths.ValidateFolder(browser.Folder);
			if (string.IsNullOrWhiteSpace(browser.BuildId))
			{
				throw new AppFailure(FailureKind.RequiredSetting, path + ".buildId");
			}

			if (!folders.Add(browser.Folder))
			{
				throw new AppFailure(FailureKind.DuplicateBrowserFolder, browser.Folder);
			}

			ValidateBrowser(browser, path);
		}
	}

	private static void ValidateBrowser (BrowserSettings browser, string path)
	{
		Require(browser.Flags, path + ".flags");
		if (browser.Flags.Any(f => f is null || f.IndexOfAny(['\r', '\n', '\0']) >= 0))
		{
			throw new AppFailure(FailureKind.InvalidBrowserFlags, browser.Folder);
		}

		if (browser.Flags.Any(f => f.StartsWith("--user-data-dir", StringComparison.OrdinalIgnoreCase) || f.StartsWith("--disk-cache-dir", StringComparison.OrdinalIgnoreCase)))
		{
			throw new AppFailure(FailureKind.ManagedBrowserFlags, browser.Folder);
		}

		Require(browser.ChromePlus, path + ".chromePlus");
		Require(browser.ChromePlus.Settings, path + ".chromePlus.settings");
		foreach ((string section, Dictionary<string, string> values) in browser.ChromePlus.Settings)
		{
			if (!SafeIni(section) || values is null)
			{
				throw new AppFailure(FailureKind.InvalidChromePlusSection, browser.Folder, section);
			}

			foreach ((string key, string value) in values)
			{
				bool invalidValue = value is null || (!ChromePlusReference.IsManaged(section, key) && value.IndexOfAny(['\r', '\n', '\0']) >= 0);
				if (!SafeIni(key) || invalidValue)
				{
					throw new AppFailure(FailureKind.InvalidChromePlusSetting, browser.Folder, section + "." + key);
				}

				if (section == "keymapping" && (!KeymappingValidator.IsValidHotkey(key) || !KeymappingValidator.IsValidTarget(value)))
				{
					throw new AppFailure(FailureKind.InvalidChromePlusSetting, browser.Folder, section + "." + key);
				}
			}
		}
	}

	private static void Require (object? value, string path)
	{
		if (value is null)
		{
			throw new AppFailure(FailureKind.RequiredSetting, path);
		}
	}

	private static bool SafeIni (string value)
	{
		return !string.IsNullOrWhiteSpace(value) && value.IndexOfAny(['\r', '\n', '\0', '[', ']', '=', ';']) < 0;
	}
}
