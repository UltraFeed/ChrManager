using System.Globalization;
using System.Text.RegularExpressions;
using ChrManager.Application;
using ChrManager.Application.Models;

namespace ChrManager.Infrastructure.Configuration;

internal static partial class ChromeCommandReference
{
	public static Uri Endpoint { get; } = new("https://raw.githubusercontent.com/chromium/chromium/refs/heads/main/chrome/app/chrome_command_ids.h");

	public static async Task ValidateAsync (LauncherSettings settings, HttpClient http, Uri endpoint, CancellationToken token)
	{
		List<Mapping> commands = [];
		foreach (BrowserSettings browser in settings.Browsers)
		{
			if (!browser.ChromePlus.Settings.TryGetValue("keymapping", out Dictionary<string, string>? mappings))
			{
				continue;
			}

			foreach ((string key, string value) in mappings)
			{
				if (!value.StartsWith("command:", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (!value.StartsWith("command:", StringComparison.Ordinal) || !TryId(value.AsSpan("command:".Length), out int id))
				{
					throw new AppFailure(FailureKind.InvalidChromeCommand, browser.Folder, key, value);
				}

				commands.Add(new(browser.Folder, key, value, id));
			}
		}

		if (commands.Count == 0)
		{
			return;
		}

		HashSet<int> ids;
		try
		{
			using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
			deadline.CancelAfter(TimeSpan.FromSeconds(settings.HttpTimeoutSeconds) * 3);
			string header = await http.GetStringAsync(endpoint, deadline.Token).ConfigureAwait(false);
			ids = Parse(header);
		}
		catch (Exception ex) when (OperationErrors.IsExpected(ex) && !token.IsCancellationRequested)
		{
			throw new AppFailure(FailureKind.ChromeCommandReferenceUnavailable, ex, Endpoint.AbsoluteUri);
		}

		foreach (Mapping command in commands)
		{
			if (!ids.Contains(command.Id))
			{
				throw new AppFailure(FailureKind.InvalidChromeCommand, command.Folder, command.Key, command.Value);
			}
		}
	}

	private static HashSet<int> Parse (string header)
	{
		HashSet<int> ids = [];
		string content = Comments().Replace(header, " ");
		foreach (Match definition in Definitions().Matches(content))
		{
			if (definition.Groups [1].Value != "IDC_MinimumLabelValue" && TryId(definition.Groups [2].ValueSpan, out int id))
			{
				_ = ids.Add(id);
			}
		}

		if (ids.Count == 0)
		{
			throw new AppFailure(FailureKind.ChromeCommandReferenceUnavailable, Endpoint.AbsoluteUri);
		}

		return ids;
	}

	private static bool TryId (ReadOnlySpan<char> value, out int id)
	{
		return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0 && value.IndexOfAnyExceptInRange('0', '9') < 0;
	}

	[GeneratedRegex(@"//[^\r\n]*|/\*[\s\S]*?(?:\*/|\z)", RegexOptions.CultureInvariant)]
	private static partial Regex Comments ();

	[GeneratedRegex(@"^[ \t]*#[ \t]*define[ \t]+(IDC_\w+)[ \t]+([0-9]+)[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
	private static partial Regex Definitions ();

	private sealed record Mapping (string Folder, string Key, string Value, int Id);
}
