using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ChrManager.Application;
using ChrManager.Application.Models;

namespace ChrManager.Infrastructure.Configuration;

internal static class ConfigurationShape
{
	public static void Validate (JsonElement root)
	{
		List<string> missing = [];
		CheckObject(root, AppJsonContext.Type<LauncherSettings>(ConfigLoader.Options), "$", missing);
		if (missing.Count > 0)
		{
			throw new AppFailure(FailureKind.MissingSettingsFields, string.Join(", ", missing));
		}
	}

	private static void CheckObject (JsonElement element, JsonTypeInfo type, string path, List<string> missing)
	{
		if (element.ValueKind != JsonValueKind.Object || type.Kind != JsonTypeInfoKind.Object)
		{
			return;
		}

		foreach (JsonPropertyInfo property in type.Properties)
		{
			string childPath = path + "." + property.Name;
			if (!element.TryGetProperty(property.Name, out JsonElement value))
			{
				if (property.IsRequired)
				{
					missing.Add(childPath);
				}

				continue;
			}

			if (property.Name == "browsers" && value.ValueKind == JsonValueKind.Array)
			{
				int index = 0;
				foreach (JsonElement browser in value.EnumerateArray())
				{
					CheckObject(browser, ConfigLoader.Options.GetTypeInfo<BrowserSettings>(), childPath + "[" + index++ + "]", missing);
				}
			}
			else
			{
				CheckObject(value, ConfigLoader.Options.GetTypeInfo(property.PropertyType), childPath, missing);
			}
		}
	}
}
