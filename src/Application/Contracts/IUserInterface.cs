using ChrManager.Presentation.Localization;

namespace ChrManager.Application.Contracts;

internal sealed record Choice<T> (T Value, StringId Label) where T : struct, Enum;

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
	public void Say (StringId message, params object? [] arguments);
	public void ReportError (StringId message, params object? [] arguments);
	public void Countdown (int seconds);
	public Task<bool> AskAsync (StringId message, params object? [] arguments);
	public Task<T> ChooseAsync<T> (StringId title, IReadOnlyList<Choice<T>> choices)
		where T : struct, Enum;
	public Task AcknowledgeAsync ();
	public IOperationProgress BeginOperation (StringId title, params object? [] arguments);
	public IDownloadProgress BeginDownload (StringId title, params object? [] arguments);
}
