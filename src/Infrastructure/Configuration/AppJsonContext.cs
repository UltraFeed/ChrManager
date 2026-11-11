using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ChrManager.Application;
using ChrManager.Application.Models;

namespace ChrManager.Infrastructure.Configuration;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(LauncherSettings))]
[JsonSerializable(typeof(UpdateJob))]
[JsonSerializable(typeof(UpdateProgress))]
[JsonSerializable(typeof(BuildFeed))]
[JsonSerializable(typeof(InstallationPhase))]
[JsonSerializable(typeof(SelfUpdatePhase))]
[JsonSerializable(typeof(BrowserConfigurationFingerprint))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
	internal static JsonTypeInfo<T> Type<T> (JsonSerializerOptions options)
	{
		return (JsonTypeInfo<T>) options.GetTypeInfo(typeof(T));
	}
}

internal sealed record BrowserConfigurationFingerprint (IReadOnlyList<string> Flags, ChromePlusSettings ChromePlus);
