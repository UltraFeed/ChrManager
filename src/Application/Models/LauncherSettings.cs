using System.Text.Json.Serialization;

namespace ChrManager.Application.Models;

internal sealed record LauncherSettings
{

	[JsonRequired]
	public int ExitDelaySeconds { get; set; }

	[JsonRequired]
	public bool StartAtLogin { get; set; }

	[JsonRequired]
	public int HttpTimeoutSeconds { get; set; }

	[JsonRequired]
	public string? UiLanguage { get; set; }

	[JsonRequired]
	public bool SelfUpdateEnabled { get; set; }

	[JsonRequired]
	public IReadOnlyList<BrowserSettings> Browsers { get; set; } = null!;
}

internal sealed record BrowserSettings
{

	[JsonRequired]
	public string Folder { get; set; } = null!;

	[JsonRequired]
	public string BuildId { get; set; } = null!;

	[JsonRequired]
	public bool Enabled { get; set; }

	[JsonRequired]
	public IReadOnlyList<string> Flags { get; set; } = null!;

	[JsonRequired]
	public ChromePlusSettings ChromePlus { get; set; } = null!;
}

internal sealed record ChromePlusSettings
{

	[JsonRequired]
	public Dictionary<string, Dictionary<string, string>> Settings { get; set; } = null!;
}

