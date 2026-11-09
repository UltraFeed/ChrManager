using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChrManager.Application;
using ChrManager.Application.Models;

namespace ChrManager.Infrastructure.Configuration;

internal static class ConfigLoader
{
	public static JsonSerializerOptions Options { get; } = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		TypeInfoResolver = AppJsonContext.Default,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = false,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
		RespectNullableAnnotations = true,
		Converters =
		{
			new CanonicalEnumConverter<InstallationPhase>(),new CanonicalEnumConverter<SelfUpdatePhase>()
		},
	};

	public static LauncherSettings Load (string path)
	{
		try
		{
			string text = File.ReadAllText(path);
			using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = false });
			CheckDuplicates(document.RootElement);
			ConfigurationShape.Validate(document.RootElement);
			LauncherSettings settings = JsonSerializer.Deserialize(text, AppJsonContext.Type<LauncherSettings>(Options)) ?? throw new AppFailure(FailureKind.SettingsNull);
			SettingsValidator.Validate(settings);
			return settings;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ValidationException or ArgumentException)
		{
			throw new SettingsException(path, ex);
		}
	}

	private static void CheckDuplicates (JsonElement element, string path = "$")
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			HashSet<string> names = new(StringComparer.Ordinal);
			foreach (JsonProperty p in element.EnumerateObject())
			{
				if (!names.Add(p.Name))
				{
					throw new AppFailure(FailureKind.DuplicateSetting, path + "." + p.Name);
				}

				CheckDuplicates(p.Value, path + "." + p.Name);
			}
		}
		else if (element.ValueKind == JsonValueKind.Array)
		{
			int index = 0;
			foreach (JsonElement item in element.EnumerateArray())
			{
				CheckDuplicates(item, path + "[" + index++ + "]");
			}
		}
	}
}
