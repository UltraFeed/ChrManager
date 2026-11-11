using ChrManager.Application.Contracts;
using ChrManager.Presentation.Localization;
using ChrManager.Presentation.Terminal;
using Spectre.Console;

namespace ChrManager.Presentation;

internal sealed class ConsoleView : IUserInterface
{
	private readonly ConsoleRenderer _renderer = new();
	private readonly LocalizationProvider _language;
	public ConsoleView (LocalizationProvider language)
	{
		_language = language;
	}

	public void Message (string message) { _renderer.Render(new("message", message)); }
	public void Say (StringId message, params object? [] arguments) { Message(_language.Format(message, arguments)); }
	public void ReportError (StringId message, params object? [] arguments) { _renderer.Render(new("error", _language.Format(message, arguments))); }
	public void Countdown (int seconds) { _renderer.Render(new("line", _language.Format(StringId.Countdown, seconds))); }

	public async Task<bool> AskAsync (StringId message, params object? [] arguments)
	{
		Choice<Confirmation> [] choices = [new(Confirmation.Yes, StringId.Yes), new(Confirmation.No, StringId.No)];
		return await ChooseAsync(_language.Format(message, arguments), choices).ConfigureAwait(false) == Confirmation.Yes;
	}

	private enum Confirmation { Yes, No }
	public Task<T> ChooseAsync<T> (StringId title, IReadOnlyList<Choice<T>> choices) where T : struct, Enum
	{
		return ChooseAsync(_language.GetString(title), choices);
	}

	private async Task<T> ChooseAsync<T> (string title, IReadOnlyList<Choice<T>> choices) where T : struct, Enum
	{
		string promptTitle = ConsoleRenderer.FormatInfo("Choose", title);
		SelectionPrompt<Choice<T>> prompt = new SelectionPrompt<Choice<T>>().Title(Markup.Escape(promptTitle)).AddChoices(choices).UseConverter(ChoiceLabel);
		Choice<T> selected = await AnsiConsole.PromptAsync(prompt).ConfigureAwait(false);
		return selected.Value;
	}
	private string ChoiceLabel<T> (Choice<T> choice) where T : struct, Enum { return Markup.Escape(_language.GetString(choice.Label)); }
	public async Task AcknowledgeAsync ()
	{
		ConsoleLine.Write(ConsoleRenderer.FormatInfo("Acknowledge", _language.GetString(StringId.PressEnter)), Style.Plain);
		while (AnsiConsole.Profile.Capabilities.Interactive &&
			await AnsiConsole.Console.Input.ReadKeyAsync(true, CancellationToken.None).ConfigureAwait(false) is { Key: not ConsoleKey.Enter })
		{ }
	}
	public IDownloadProgress BeginDownload (StringId title, params object? [] arguments)
	{
		return new DownloadDisplay(_language.Format(title, arguments), _language);
	}

	public IOperationProgress BeginOperation (StringId title, params object? [] arguments)
	{
		string label = _language.Format(title, arguments);
		_renderer.Render(new("phase", label));
		return new ProgressScope(_renderer, label);
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
