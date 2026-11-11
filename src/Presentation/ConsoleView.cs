using ChrManager.Application.Contracts;
using ChrManager.Presentation.Localization;
using ChrManager.Presentation.Terminal;
using Spectre.Console;

namespace ChrManager.Presentation;

internal sealed class ConsoleView : IUserInterface
{
	private readonly ConsoleRenderer _renderer = new();
	public LocalizationProvider Language { get; private set; } = new(null);
	public void Configure (LocalizationProvider language)
	{
		Language = language;
	}
	public void Message (string message) { _renderer.Render(new("message", message)); }
	public void ReportError (string message) { _renderer.Render(new("error", message)); }
	public void Countdown (string message) { _renderer.Render(new("line", message)); }
	public Task<bool> ConfirmAsync (string message) { return ChooseConfirmationAsync(message); }
	private async Task<bool> ChooseConfirmationAsync (string message)
	{
		Choice<Confirmation> [] choices = [new(Confirmation.Yes, Language.GetString(StringId.Yes)), new(Confirmation.No, Language.GetString(StringId.No))];
		return await ChooseAsync(message, choices, Confirmation.No).ConfigureAwait(false) == Confirmation.Yes;
	}
	private enum Confirmation { Yes, No }
	public async Task<T> ChooseAsync<T> (string title, IReadOnlyList<Choice<T>> choices, T safeChoice) where T : struct, Enum
	{
		string promptTitle = ConsoleRenderer.FormatInfo("Choose", title);
		SelectionPrompt<Choice<T>> prompt = new SelectionPrompt<Choice<T>>().Title(Markup.Escape(promptTitle)).AddChoices(choices).UseConverter(ChoiceLabel);
		Choice<T> selected = await AnsiConsole.PromptAsync(prompt).ConfigureAwait(false);
		return selected.Value;
	}
	private static string ChoiceLabel<T> (Choice<T> choice) where T : struct, Enum { return Markup.Escape(choice.Label); }
	public async Task AcknowledgeAsync ()
	{
		ConsoleLine.Write(ConsoleRenderer.FormatInfo("Acknowledge", Language.GetString(StringId.PressEnter)), Style.Plain);
		while (AnsiConsole.Profile.Capabilities.Interactive &&
			await AnsiConsole.Console.Input.ReadKeyAsync(true, CancellationToken.None).ConfigureAwait(false) is { Key: not ConsoleKey.Enter })
		{ }
	}
	public IDownloadProgress BeginDownload (string title)
	{
		return new DownloadDisplay(title, Language);
	}

	public IOperationProgress BeginOperation (string title)
	{
		_renderer.Render(new("phase", title));
		return new ProgressScope(_renderer, title);
	}
	private sealed class ProgressScope : IOperationProgress
	{
		private readonly ConsoleRenderer _renderer;
		private readonly string _title;
		public ProgressScope (ConsoleRenderer renderer, string title)
		{
			_renderer = renderer;
			_title = title;
		}
		public void Report (double value)
		{
			_renderer.Render(new("progress", _title, value));
		}
		public void Dispose ()
		{
		}
	}
}
