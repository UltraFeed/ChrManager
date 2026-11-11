namespace ChrManager.Application;

internal enum FailureKind
{
	Unknown,
	InvalidApplicationMetadata,
	RequiredSetting,
	SettingOutOfRange,
	InvalidUiLanguage,
	DuplicateSetting,
	SettingsNull,
	DuplicateBrowserFolder,
	InvalidBrowserFlags,
	ManagedBrowserFlags,
	InvalidChromePlusSection,
	InvalidChromePlusSetting,
	InvalidBrowserFolder,
	ReservedBrowserFolder,
	ReparsePoint,
	InstanceLeaseFailed,
	WindowsUserUnavailable,
	BrowserProcessPathUnavailable,
	StartupTargetTooLong,
	StartupTargetInvalid,
	BrowserUnavailable,
	UnsupportedPeArchitecture,
	InvalidBuildSelection,
	InvalidBrowserVersion,
	InvalidArchiveSelection,
	AmbiguousArchitecture,
	ChromePlusAssetMissing,
	ChromePlusDllMissing,
	InvalidChromePlusArchitecture,
	DuplicateChromePlusSection,
	InvalidGithubMetadata,
	GithubAssetsMissing,
	GithubAssetFieldMissing,
	GithubRedirectMissing,
	GithubRedirectInvalid,
	GithubTagInvalid,
	UntrackedBackup,
	CommittedBrowserMissing,
	BrowserAndBackupMissing,
	UnrecoveredBackup,
	RemovalCancelled,
	BrowserReleaseMissing,
	ArchivePathInvalid,
	ArchiveEmpty,
	ArchiveDestinationDuplicate,
	ArchiveEntryEscapes,
	ArchiveEntryUnsupported,
	InvalidBuildCatalogue,
	DownloadRequiresHttps,
	InvalidReferenceSection,
	InvalidReferenceParameter,
	IncompleteReference,
	UnsupportedPhase,
	InvalidPhase,
	InvalidInternalRole,
	InvalidConsolePacket,
	LocalizationResourcesInvalid,
	LocalizationTagInvalid,
	LocalizationKeyInvalid,
	LocalizationEmpty,
	LocalizationIncomplete,
	LocalizationParametersInvalid,
	MissingSettingsFields,
	ChromePlusReferenceUnavailable,
	ChromePlusSettingsMismatch,
	IncompleteDownload,
	ChecksumMismatch,
	PackageMissing,
	ArchitectureMismatch,
	ForeignDll,
	BrowserFilesInUse,
	ForeignStartupShortcut,
	ForeignTemporaryFile,
	InvalidUpdateJob,
	InvalidUpdateCheckpoint,
	InvalidUpdateHandover,
	UpdateHelperMismatch,
	UpdateHelperStartFailed,
	UpdateLockFailed,
	UpdateResumeMismatch,
	InvalidSelfUpdateRelease,
	SelfUpdateAssetMissing,
	InvalidChromeCommand,
	ChromeCommandReferenceUnavailable,
	SelfUpdateArchitectureMismatch,
	SelfUpdateChecksumRequired,
	InvalidSelfUpdateExecutable,
	SelfUpdateVersionMismatch,
	SelfUpdatePackageChanged,
	ParentProcessMismatch,
	InvalidReadinessResponse,
	ManagerRestartFailed
}

internal sealed class AppFailure : IOException
{
	public FailureKind Kind { get; } = FailureKind.Unknown;
	public object? [] Arguments { get; } = [];

	public AppFailure () : base(nameof(AppFailure)) { }

	public AppFailure (string message) : base(message) { }

	public AppFailure (string message, Exception innerException) : base(message, innerException) { }

	public AppFailure (FailureKind kind, params object? [] arguments) : base(kind.ToString())
	{
		Kind = kind;
		Arguments = arguments;
	}

	public AppFailure (FailureKind kind, Exception innerException, params object? [] arguments) : base(kind.ToString(), innerException)
	{
		Kind = kind;
		Arguments = arguments;
	}
}
