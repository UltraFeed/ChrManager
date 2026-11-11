using ChrManager.Application;
using ChrManager.Application.Contracts;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Configuration;
using ChrManager.Presentation.Localization;

namespace ChrManager.Presentation;

internal sealed class SettingsPreflight
{
	private readonly IChromePlusReference _reference;
	private readonly IUserInterface _view;

	public SettingsPreflight (IChromePlusReference reference, IUserInterface view)
	{
		_reference = reference;
		_view = view;
	}

	public async Task ValidateAsync (LauncherSettings settings, CancellationToken token)
	{
		ChromePlusCheck check = await _reference.CheckAsync(settings, token).ConfigureAwait(false);
		foreach (IgnoredChromePlusSetting setting in check.Ignored)
		{
			_view.Say(StringId.ChromePlusIgnoredSetting, setting.Folder, setting.Key);
		}

		foreach (ChromePlusDifference difference in check.Differences)
		{
			if (difference.Add.Count > 0)
			{
				_view.Say(StringId.ChromePlusAddSettings, difference.Folder, string.Join(", ", difference.Add));
			}

			if (difference.Remove.Count > 0)
			{
				_view.Say(StringId.ChromePlusRemoveSettings, difference.Folder, string.Join(", ", difference.Remove));
			}
		}

		if (check.Differences.Count > 0)
		{
			throw new AppFailure(FailureKind.ChromePlusSettingsMismatch, ChromePlusReference.DocumentationUrl);
		}
	}
}
