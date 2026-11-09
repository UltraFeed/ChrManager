using ChrManager.Presentation;
using Serilog;
using Serilog.Core;

namespace ChrManager.Infrastructure.Diagnostics;
/// <summary>
/// Diagnostics are written only to the current console.
/// </summary>
internal static class AppLogging
{
	public static Logger CreateLogger (SpectreLogSink? sink = null)
	{
		return new LoggerConfiguration().MinimumLevel.Information().WriteTo.Sink(sink ?? new SpectreLogSink()).CreateLogger();
	}
}
