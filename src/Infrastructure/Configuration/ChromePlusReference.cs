using ChrManager.Application;
using ChrManager.Application.Contracts;
using ChrManager.Application.Models;

namespace ChrManager.Infrastructure.Configuration;

internal sealed class ChromePlusReference : IChromePlusReference
{
	public const string DocumentationUrl = "https://github.com/Bush2021/chrome_plus/blob/main/src/chrome%2B%2B.ini";
	private static readonly Uri Upstream = new("https://raw.githubusercontent.com/Bush2021/chrome_plus/refs/heads/main/src/chrome%2B%2B.ini");
	private readonly HttpClient _http;
	private readonly Uri _endpoint;
	private readonly Uri _commandEndpoint;

	public ChromePlusReference (HttpClient http, Uri? endpoint = null, Uri? commandEndpoint = null)
	{
		_http = http;
		_endpoint = endpoint ?? Upstream;
		_commandEndpoint = commandEndpoint ?? ChromeCommandReference.Endpoint;
	}

	public async Task<ChromePlusCheck> CheckAsync (LauncherSettings settings, CancellationToken token)
	{
		Dictionary<string, HashSet<string>> reference;
		try
		{
			using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
			deadline.CancelAfter(TimeSpan.FromSeconds(settings.HttpTimeoutSeconds) * 3);
			string text = await _http.GetStringAsync(_endpoint, deadline.Token).ConfigureAwait(false);
			reference = Parse(text);
		}
		catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidDataException or IOException or InvalidOperationException or ArgumentException)
		{
			throw new AppFailure(FailureKind.ChromePlusReferenceUnavailable, ex, DocumentationUrl);
		}

		await ChromeCommandReference.ValidateAsync(settings, _http, _commandEndpoint, token).ConfigureAwait(false);
		return Compare(settings.Browsers, reference);
	}

	public static bool IsManaged (string section, string key)
	{
		return section == "general" && key is "command_line" or "data_dir" or "cache_dir" or "managed_updates";
	}

	public static Dictionary<string, HashSet<string>> Parse (string text)
	{
		Dictionary<string, HashSet<string>> sections = new(StringComparer.Ordinal);
		HashSet<string>? current = null;
		using StringReader reader = new(text.TrimStart('\uFEFF'));
		while (reader.ReadLine() is { } raw)
		{
			string line = raw.Trim();
			if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
			{
				continue;
			}

			if (line.StartsWith('[') && line.EndsWith(']'))
			{
				string section = line [1..^1];
				current = new(StringComparer.Ordinal);
				if (!IsName(section) || !sections.TryAdd(section, current))
				{
					throw new AppFailure(FailureKind.InvalidReferenceSection);
				}

				continue;
			}

			int separator = line.IndexOf('=', StringComparison.Ordinal);
			if (current is null || separator < 1 || !IsName(line [..separator].Trim()) || !current.Add(line [..separator].Trim()))
			{
				throw new AppFailure(FailureKind.InvalidReferenceParameter);
			}
		}

		if (!sections.TryGetValue("general", out HashSet<string>? general) || !general.Contains("data_dir") || !general.Contains("cache_dir") || !general.Contains("command_line"))
		{
			throw new AppFailure(FailureKind.IncompleteReference);
		}

		return sections;
	}

	public static ChromePlusCheck Compare (IReadOnlyList<BrowserSettings> browsers, IReadOnlyDictionary<string, HashSet<string>> reference)
	{
		List<ChromePlusDifference> differences = [];
		List<IgnoredChromePlusSetting> ignored = [];
		foreach (BrowserSettings browser in browsers)
		{
			List<string> add = [];
			List<string> remove = [];
			foreach ((string section, HashSet<string> keys) in reference)
			{
				if (!browser.ChromePlus.Settings.TryGetValue(section, out Dictionary<string, string>? supplied))
				{
					add.Add("[" + section + "]");
				}

				foreach (string key in keys)
				{
					if (!IsManaged(section, key) && (supplied is null || !supplied.ContainsKey(key)))
					{
						add.Add(section + "." + key);
					}
				}
			}

			foreach ((string section, Dictionary<string, string> values) in browser.ChromePlus.Settings)
			{
				if (!reference.ContainsKey(section))
				{
					remove.Add("[" + section + "]");
				}

				foreach (string key in values.Keys)
				{
					if (IsManaged(section, key))
					{
						ignored.Add(new(browser.Folder, section + "." + key));
					}
					else if (section != "keymapping" && (!reference.TryGetValue(section, out HashSet<string>? keys) || !keys.Contains(key)))
					{
						remove.Add(section + "." + key);
					}
				}
			}

			if (add.Count > 0 || remove.Count > 0)
			{
				differences.Add(new(browser.Folder, add.Order(StringComparer.Ordinal).ToArray(), remove.Order(StringComparer.Ordinal).ToArray()));
			}
		}

		return new(differences, ignored);
	}

	private static bool IsName (string value)
	{
		return !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.IndexOfAny(['[', ']', '=', ';', '\0']) < 0;
	}
}
