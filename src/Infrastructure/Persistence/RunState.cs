using System.Text.Json;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Configuration;
namespace ChrManager.Infrastructure.Persistence;

internal sealed class RunState
{
	public RunSnapshot State { get; private set; } = new();
	public BrowserMemory Browser (string folder) { return State.Browsers.GetValueOrDefault(folder) ?? new(); }
	public void Browser (string folder, BrowserMemory memory)
	{
		Dictionary<string, BrowserMemory> browsers = new(State.Browsers, StringComparer.OrdinalIgnoreCase) { [folder] = memory };
		State = State with { Browsers = browsers };
	}
	public void Save (RunSnapshot snapshot) { State = snapshot; }
}

internal static class AtomicJson
{
	public static void Write<T> (string path, T value)
	{
		PortablePaths.RejectReparsePoints(path);
		_ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		string temporary = path + ".tmp";
		PortablePaths.RejectReparsePoints(temporary);
		using (FileStream file = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
		{
			JsonSerializer.Serialize(file, value, AppJsonContext.Type<T>(ConfigLoader.Options));
			file.Flush(true);
		}

		File.Move(temporary, path, true);
	}
}
