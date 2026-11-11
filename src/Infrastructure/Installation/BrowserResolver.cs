using System.Diagnostics;
using System.Reflection.PortableExecutable;
using ChrManager.Application;

namespace ChrManager.Infrastructure.Installation;

internal static class BrowserResolver
{
	public static string Architecture (string binary)
	{
		return IsX64(binary) ? "x64" : throw new AppFailure(FailureKind.UnsupportedPeArchitecture, binary);
	}

	public static bool IsX64 (string binary)
	{
		using FileStream file = File.OpenRead(binary);
		using PEReader reader = new(file);
		return reader.PEHeaders.CoffHeader.Machine == Machine.Amd64 && reader.PEHeaders.PEHeader?.Magic == PEMagic.PE32Plus;
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
