using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChrManager.Application;

namespace ChrManager.Presentation.Localization;

// All resources are validated before selecting one language. No per-string fallback.
internal sealed partial class LocalizationProvider
{
	private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<StringId, string>>> Resources = new(Load);
	private readonly IReadOnlyDictionary<StringId, string> _strings;
	public string Code { get; }
	public string? Warning { get; }
	public static IReadOnlyCollection<string> Codes => Resources.Value.Keys.ToArray();

	public LocalizationProvider (string? language)
	{
		IReadOnlyDictionary<string, IReadOnlyDictionary<StringId, string>> all = Resources.Value;
		string selected = language is null ? SystemLanguage(CultureInfo.CurrentUICulture, all.Keys) : language;
		if (!all.TryGetValue(selected, out IReadOnlyDictionary<StringId, string>? strings))
		{
			strings = all ["en"];
			Warning = string.Format(CultureInfo.InvariantCulture, strings [StringId.UnknownLanguage], language);
			selected = "en";
		}

		Code = selected;
		_strings = strings;
	}

	public string GetString (StringId id)
	{
		return _strings [id];
	}

	public string Format (StringId id, params object? [] values)
	{
		return string.Format(CultureInfo.CurrentCulture, _strings [id], values);
	}

	public static string SystemLanguage (CultureInfo culture, IEnumerable<string> codes)
	{
		HashSet<string> supported = codes.ToHashSet(StringComparer.Ordinal);
		for (CultureInfo current = culture; current.Name.Length > 0; current = current.Parent)
		{
			if (supported.Contains(current.Name))
			{
				return current.Name;
			}
		}

		return "en";
	}

	public static void ValidateResources ()
	{
		_ = Resources.Value;
	}

	private static IReadOnlyDictionary<string, IReadOnlyDictionary<StringId, string>> Load ()
	{
		Assembly assembly = typeof(LocalizationProvider).Assembly;
		Dictionary<string, string> documents = new(StringComparer.Ordinal);
		foreach (string? name in assembly.GetManifestResourceNames().Where(n => n.Contains(".i18n.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)))
		{
			string code = name [(name.IndexOf(".i18n.", StringComparison.Ordinal) + 6)..^5];
			using Stream stream = assembly.GetManifestResourceStream(name)!;
			using StreamReader reader = new(stream);
			documents.Add(code, reader.ReadToEnd());
		}

		return Validate(documents);
	}

	public static IReadOnlyDictionary<string, IReadOnlyDictionary<StringId, string>> Validate (IReadOnlyDictionary<string, string> documents)
	{
		string [] codes = ["en", "ru"];
		if (!codes.ToHashSet(StringComparer.Ordinal).SetEquals(documents.Keys))
		{
			throw new AppFailure(FailureKind.LocalizationResourcesInvalid);
		}

		Dictionary<string, StringId> expected = Enum.GetValues<StringId>().ToDictionary(x => JsonNamingPolicy.CamelCase.ConvertName(x.ToString()), StringComparer.Ordinal);
		Dictionary<string, IReadOnlyDictionary<StringId, string>> result = new(StringComparer.Ordinal);
		foreach ((string? code, string? json) in documents)
		{
			if (CultureInfo.GetCultureInfo(code).Name != code)
			{
				throw new AppFailure(FailureKind.LocalizationTagInvalid, code);
			}

			using JsonDocument document = JsonDocument.Parse(json);
			Dictionary<StringId, string> values = new();
			foreach (JsonProperty property in document.RootElement.EnumerateObject())
			{
				if (!expected.TryGetValue(property.Name, out StringId id) || !values.TryAdd(id, property.Value.GetString() ?? ""))
				{
					throw new AppFailure(FailureKind.LocalizationKeyInvalid, code + "/" + property.Name);
				}

				if (string.IsNullOrWhiteSpace(values [id]))
				{
					throw new AppFailure(FailureKind.LocalizationEmpty, code + "/" + property.Name);
				}

				_ = System.Text.CompositeFormat.Parse(values [id]);
			}

			if (values.Count != expected.Count)
			{
				throw new AppFailure(FailureKind.LocalizationIncomplete, code);
			}

			result.Add(code, values);
		}

		if (!result.TryGetValue("en", out IReadOnlyDictionary<StringId, string>? english))
		{
			throw new AppFailure(FailureKind.LocalizationResourcesInvalid);
		}

		foreach ((string? code, IReadOnlyDictionary<StringId, string>? values) in result)
		{
			foreach (StringId id in expected.Values)
			{
				if (Signature(values [id]) != Signature(english [id]))
				{
					throw new AppFailure(FailureKind.LocalizationParametersInvalid, code + "/" + id);
				}
			}
		}

		return result;
	}

	private static string Signature (string value)
	{
		return string.Join(",", PlaceholderPattern().Matches(value).Select(m => m.Groups [1].Value).Distinct().Order(StringComparer.Ordinal));
	}

	[GeneratedRegex(@"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})")]
	private static partial Regex PlaceholderPattern ();
}
