using System.Diagnostics;
using System.Reflection.PortableExecutable;
using ChrManager.Application;

namespace ChrManager.Infrastructure.Installation;

internal static class BrowserResolver
{
	public static string Architecture (string binary)
	{
		using FileStream file = File.OpenRead(binary);
		using PEReader reader = new(file);
		return reader.PEHeaders.CoffHeader.Machine switch
		{
			Machine.Amd64 => "x64",
			Machine.Arm64 => "arm64",
			Machine.I386 => "x86",
			_ => throw new AppFailure(FailureKind.UnsupportedPeArchitecture, binary)
		};
	}

	public static string? QueryVersion (string binary)
	{
		if (!File.Exists(binary))
		{
			return null;
		}

		FileVersionInfo info = FileVersionInfo.GetVersionInfo(binary);
		return info.FileVersion is null ? null : $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}.{info.FilePrivatePart}";
	}
}
