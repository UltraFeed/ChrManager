using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChrManager.Application;
using ChrManager.Infrastructure.Configuration;

namespace ChrManager.Presentation.Localization;

// All resources are validated before selecting one language. No per-string fallback.
internal sealed partial class LocalizationProvider
{
	private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Resources = new(Load);
	private IReadOnlyDictionary<string, string> _strings = null!;
	public LocalizationProvider (string? language = null)
	{
		_ = SelectLanguage(language);
	}

	public string? SelectLanguage (string? language)
	{
		IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> all = Resources.Value;
		string selected = language ?? SystemLanguage(CultureInfo.CurrentUICulture, all.Keys);
		if (all.TryGetValue(selected, out IReadOnlyDictionary<string, string>? strings))
		{
			_strings = strings;
			return null;
		}

		_strings = all ["en"];
		return Format(StringId.UnknownLanguage, language);
	}

	public string GetString (StringId id)
	{
		return _strings [id.ToString()];
	}

	public string Format (StringId id, params object? [] values)
	{
		return string.Format(CultureInfo.CurrentCulture, _strings [id.ToString()], values);
	}

	public string Format (DiagnosticKind kind, params object? [] values)
	{
		return string.Format(CultureInfo.CurrentCulture, _strings [kind.ToString()], values);
	}

	public string Explain (Exception exception)
	{
		if (exception is AppFailure failure)
		{
			return FormatFailure(failure);
		}

		if (exception is SettingsException settings)
		{
			return Format(StringId.InvalidSettings, settings.FilePath);
		}

		if (exception is JsonException json)
		{
			return Format(StringId.InvalidJson, json.Path ?? "$", json.LineNumber.GetValueOrDefault() + 1, json.BytePositionInLine.GetValueOrDefault() + 1);
		}

		if (exception is HttpRequestException { StatusCode: { } status })
		{
			return Format(StringId.NetworkStatusFailure, (int) status);
		}

		StringId id = exception switch
		{
			HttpRequestException => StringId.NetworkFailure,
			UnauthorizedAccessException => StringId.FileAccessDenied,
			IOException => StringId.FileOperationFailed,
			InvalidDataException => StringId.InvalidData,
			ValidationException => StringId.InvalidSettingsValue,
			OperationCanceledException => StringId.OperationCancelled,
			TimeoutException => StringId.OperationTimedOut,
			_ => StringId.GenericFailure
		};
		return GetString(id);
	}

	private string FormatFailure (AppFailure failure)
	{
		if (!_strings.TryGetValue(failure.Kind.ToString(), out string? message))
		{
			message = GetString(StringId.GenericFailure);
		}

		return string.Format(CultureInfo.CurrentCulture, message, failure.Arguments);
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

	private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Load ()
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

	public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Validate (IReadOnlyDictionary<string, string> documents)
	{
		string [] codes = ["en", "ru"];
		if (!codes.ToHashSet(StringComparer.Ordinal).SetEquals(documents.Keys))
		{
			throw new AppFailure(FailureKind.LocalizationResourcesInvalid);
		}

		IEnumerable<string> names = Enum.GetNames<StringId>()
			.Concat(Enum.GetNames<DiagnosticKind>())
			.Concat(Enum.GetNames<FailureKind>().Where(IsLocalizableFailure));
		Dictionary<string, string> expected = names.ToDictionary(JsonNamingPolicy.CamelCase.ConvertName, StringComparer.Ordinal);
		Dictionary<string, IReadOnlyDictionary<string, string>> result = new(StringComparer.Ordinal);
		foreach ((string? code, string? json) in documents)
		{
			if (CultureInfo.GetCultureInfo(code).Name != code)
			{
				throw new AppFailure(FailureKind.LocalizationTagInvalid, code);
			}

			using JsonDocument document = JsonDocument.Parse(json);
			Dictionary<string, string> values = new(StringComparer.Ordinal);
			foreach (JsonProperty property in document.RootElement.EnumerateObject())
			{
				if (!expected.TryGetValue(property.Name, out string? id) || !values.TryAdd(id, property.Value.GetString() ?? ""))
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

		if (!result.TryGetValue("en", out IReadOnlyDictionary<string, string>? english))
		{
			throw new AppFailure(FailureKind.LocalizationResourcesInvalid);
		}

		foreach ((string? code, IReadOnlyDictionary<string, string>? values) in result)
		{
			foreach (string id in expected.Values)
			{
				if (Signature(values [id]) != Signature(english [id]))
				{
					throw new AppFailure(FailureKind.LocalizationParametersInvalid, code + "/" + id);
				}
			}
		}

		return result;
	}

	private static bool IsLocalizableFailure (string name) { return name != nameof(FailureKind.Unknown); }

	private static string Signature (string value)
	{
		return string.Join(",", PlaceholderPattern().Matches(value).Select(m => m.Groups [1].Value).Distinct().Order(StringComparer.Ordinal));
	}

	[GeneratedRegex(@"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})")]
	private static partial Regex PlaceholderPattern ();
}
