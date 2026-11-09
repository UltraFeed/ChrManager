using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ChrManager.Application;
using ChrManager.Application.Contracts;
using ChrManager.Infrastructure.Configuration;
using ChrManager.Presentation.Localization;

namespace ChrManager.Presentation;

internal static class ViewText
{
	public static void Say (this IUserInterface view, StringId id, params object? [] values)
	{
		view.Message(view.Language.Format(id, values));
	}

	public static Task<bool> AskAsync (this IUserInterface view, StringId id, params object? [] values)
	{
		return view.ConfirmAsync(view.Language.Format(id, values));
	}

	public static string Explain (this IUserInterface view, Exception exception)
	{
		return Explain(exception, view.Language);
	}

	public static string Explain (Exception exception, LocalizationProvider language)
	{
		if (exception is not AppFailure failure)
		{
			return ExplainExternal(exception, language);
		}

		StringId message = failure.Kind switch
		{
			FailureKind.InvalidApplicationMetadata => StringId.InvalidApplicationMetadata,
			FailureKind.RequiredSetting => StringId.RequiredSetting,
			FailureKind.SettingOutOfRange => StringId.SettingOutOfRange,
			FailureKind.InvalidUiLanguage => StringId.InvalidUiLanguage,
			FailureKind.DuplicateSetting => StringId.DuplicateSetting,
			FailureKind.SettingsNull => StringId.SettingsNull,
			FailureKind.DuplicateBrowserFolder => StringId.DuplicateBrowserFolder,
			FailureKind.InvalidBrowserFlags => StringId.InvalidBrowserFlags,
			FailureKind.ManagedBrowserFlags => StringId.ManagedBrowserFlags,
			FailureKind.InvalidChromePlusSection => StringId.InvalidChromePlusSection,
			FailureKind.InvalidChromePlusSetting => StringId.InvalidChromePlusSetting,
			FailureKind.InvalidBrowserFolder => StringId.InvalidBrowserFolder,
			FailureKind.ReservedBrowserFolder => StringId.ReservedBrowserFolder,
			FailureKind.ReparsePoint => StringId.ReparsePoint,
			FailureKind.InstanceLeaseFailed => StringId.InstanceLeaseFailed,
			FailureKind.WindowsUserUnavailable => StringId.WindowsUserUnavailable,
			FailureKind.BrowserProcessPathUnavailable => StringId.BrowserProcessPathUnavailable,
			FailureKind.StartupTargetTooLong => StringId.StartupTargetTooLong,
			FailureKind.StartupTargetInvalid => StringId.StartupTargetInvalid,
			FailureKind.BrowserUnavailable => StringId.BrowserUnavailable,
			FailureKind.UnsupportedPeArchitecture => StringId.UnsupportedPeArchitecture,
			FailureKind.InvalidBuildSelection => StringId.InvalidBuildSelection,
			FailureKind.InvalidBrowserVersion => StringId.InvalidBrowserVersion,
			FailureKind.InvalidArchiveSelection => StringId.InvalidArchiveSelection,
			FailureKind.AmbiguousArchitecture => StringId.AmbiguousArchitecture,
			FailureKind.ChromePlusAssetMissing => StringId.ChromePlusAssetMissing,
			FailureKind.ChromePlusDllMissing => StringId.ChromePlusDllMissing,
			FailureKind.InvalidChromePlusArchitecture => StringId.InvalidChromePlusArchitecture,
			FailureKind.DuplicateChromePlusSection => StringId.DuplicateChromePlusSection,
			FailureKind.InvalidGithubMetadata => StringId.InvalidGithubMetadata,
			FailureKind.GithubAssetsMissing => StringId.GithubAssetsMissing,
			FailureKind.GithubAssetFieldMissing => StringId.GithubAssetFieldMissing,
			FailureKind.GithubRedirectMissing => StringId.GithubRedirectMissing,
			FailureKind.GithubRedirectInvalid => StringId.GithubRedirectInvalid,
			FailureKind.GithubTagInvalid => StringId.GithubTagInvalid,
			FailureKind.UntrackedBackup => StringId.UntrackedBackup,
			FailureKind.CommittedBrowserMissing => StringId.CommittedBrowserMissing,
			FailureKind.BrowserAndBackupMissing => StringId.BrowserAndBackupMissing,
			FailureKind.UnrecoveredBackup => StringId.UnrecoveredBackup,
			FailureKind.RemovalCancelled => StringId.RemovalCancelled,
			FailureKind.BrowserReleaseMissing => StringId.BrowserReleaseMissing,
			FailureKind.ArchivePathInvalid => StringId.ArchivePathInvalid,
			FailureKind.ArchiveEmpty => StringId.ArchiveEmpty,
			FailureKind.ArchiveDestinationDuplicate => StringId.ArchiveDestinationDuplicate,
			FailureKind.ArchiveEntryEscapes => StringId.ArchiveEntryEscapes,
			FailureKind.ArchiveEntryUnsupported => StringId.ArchiveEntryUnsupported,
			FailureKind.InvalidBuildCatalogue => StringId.InvalidBuildCatalogue,
			FailureKind.DownloadRequiresHttps => StringId.DownloadRequiresHttps,
			FailureKind.InvalidReferenceSection => StringId.InvalidReferenceSection,
			FailureKind.InvalidReferenceParameter => StringId.InvalidReferenceParameter,
			FailureKind.IncompleteReference => StringId.IncompleteReference,
			FailureKind.UnsupportedPhase => StringId.UnsupportedPhase,
			FailureKind.InvalidPhase => StringId.InvalidPhase,
			FailureKind.InvalidInternalRole => StringId.InvalidInternalRole,
			FailureKind.InvalidConsolePacket => StringId.InvalidConsolePacket,
			FailureKind.LocalizationResourcesInvalid => StringId.LocalizationResourcesInvalid,
			FailureKind.LocalizationTagInvalid => StringId.LocalizationTagInvalid,
			FailureKind.LocalizationKeyInvalid => StringId.LocalizationKeyInvalid,
			FailureKind.LocalizationEmpty => StringId.LocalizationEmpty,
			FailureKind.LocalizationIncomplete => StringId.LocalizationIncomplete,
			FailureKind.LocalizationParametersInvalid => StringId.LocalizationParametersInvalid,
			FailureKind.MissingSettingsFields => StringId.MissingSettingsFields,
			FailureKind.ChromePlusReferenceUnavailable => StringId.ChromePlusReferenceUnavailable,
			FailureKind.ChromePlusSettingsMismatch => StringId.ChromePlusSettingsMismatch,
			FailureKind.IncompleteDownload => StringId.IncompleteDownload,
			FailureKind.ChecksumMismatch => StringId.ChecksumMismatch,
			FailureKind.PackageMissing => StringId.PackageMissing,
			FailureKind.ArchitectureMismatch => StringId.ArchitectureMismatch,
			FailureKind.ForeignDll => StringId.ForeignDll,
			FailureKind.BrowserFilesInUse => StringId.BrowserFilesInUse,
			FailureKind.ForeignStartupShortcut => StringId.ForeignStartupShortcut,
			FailureKind.ForeignTemporaryFile => StringId.ForeignTemporaryFile,
			FailureKind.InvalidUpdateJob => StringId.InvalidUpdateJob,
			FailureKind.InvalidUpdateCheckpoint => StringId.InvalidUpdateCheckpoint,
			FailureKind.InvalidUpdateHandover => StringId.InvalidUpdateHandover,
			FailureKind.UpdateHelperMismatch => StringId.UpdateHelperMismatch,
			FailureKind.UpdateHelperStartFailed => StringId.UpdateHelperStartFailed,
			FailureKind.UpdateLockFailed => StringId.UpdateLockFailed,
			FailureKind.UpdateResumeMismatch => StringId.UpdateResumeMismatch,
			FailureKind.SelfUpdateAssetMissing => StringId.SelfUpdateAssetMissing,
			FailureKind.InvalidChromeCommand => StringId.InvalidChromeCommand,
			FailureKind.ChromeCommandReferenceUnavailable => StringId.ChromeCommandReferenceUnavailable,
			FailureKind.InvalidSelfUpdateRelease => StringId.InvalidSelfUpdateRelease,
			FailureKind.SelfUpdateArchitectureMismatch => StringId.SelfUpdateArchitectureMismatch,
			FailureKind.SelfUpdateChecksumRequired => StringId.SelfUpdateChecksumRequired,
			FailureKind.InvalidSelfUpdateExecutable => StringId.InvalidSelfUpdateExecutable,
			FailureKind.SelfUpdateVersionMismatch => StringId.SelfUpdateVersionMismatch,
			FailureKind.SelfUpdatePackageChanged => StringId.SelfUpdatePackageChanged,
			FailureKind.ParentProcessMismatch => StringId.ParentProcessMismatch,
			FailureKind.InvalidReadinessResponse => StringId.InvalidReadinessResponse,
			FailureKind.ManagerRestartFailed => StringId.ManagerRestartFailed,
			_ => StringId.GenericFailure
		};
		return language.Format(message, failure.Arguments);
	}

	private static string ExplainExternal (Exception exception, LocalizationProvider language)
	{
		if (exception is SettingsException settings)
		{
			return language.Format(StringId.InvalidSettings, settings.FilePath);
		}

		if (exception is JsonException json)
		{
			return language.Format(StringId.InvalidJson, json.Path ?? "$", json.LineNumber.GetValueOrDefault() + 1, json.BytePositionInLine.GetValueOrDefault() + 1);
		}

		if (exception is HttpRequestException { StatusCode: { } status })
		{
			return language.Format(StringId.NetworkStatusFailure, (int) status);
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
		return language.GetString(id);
	}
}
