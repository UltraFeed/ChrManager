namespace ChrManager.Infrastructure.Configuration;

/// <summary>Thrown when settings.json exists but cannot be understood.</summary>
internal sealed class SettingsException : Exception
{
	public string FilePath { get; } = string.Empty;

	public SettingsException () : base(nameof(SettingsException)) { }

	public SettingsException (string filePath, Exception innerException) : base(nameof(SettingsException), innerException)
	{
		FilePath = filePath;
	}

	public SettingsException (string message) : base(message) { }
}
