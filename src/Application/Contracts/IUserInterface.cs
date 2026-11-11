using ChrManager.Presentation.Localization;

namespace ChrManager.Application.Contracts;

internal sealed record Choice<T> (T Value, string Label) where T : struct, Enum;

internal enum BrowserCloseAction
{
	Wait,
	Postpone,
	ForceTerminate
}

internal interface IOperationProgress : IProgress<double>, IDisposable
{
}

internal readonly record struct DownloadProgress (long Received, long? Total, bool Complete = false);

internal interface IDownloadProgress : IProgress<DownloadProgress>, IAsyncDisposable;

internal interface IUserInterface
{
	public LocalizationProvider Language { get; }

	public void Message (string message);
	public void ReportError (string message);
	public void Countdown (string message);
	public Task<bool> ConfirmAsync (string message);
	public Task<T> ChooseAsync<T> (string title, IReadOnlyList<Choice<T>> choices, T safeChoice)
		where T : struct, Enum;
	public Task AcknowledgeAsync ();
	public IOperationProgress BeginOperation (string title);
	public IDownloadProgress BeginDownload (string title);
}
