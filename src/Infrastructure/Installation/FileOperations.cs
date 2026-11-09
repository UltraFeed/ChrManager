using VisualCancelOption = Microsoft.VisualBasic.FileIO.UICancelOption;
using VisualFileIO = Microsoft.VisualBasic.FileIO.FileSystem;
using VisualRecycleOption = Microsoft.VisualBasic.FileIO.RecycleOption;
using VisualUIOption = Microsoft.VisualBasic.FileIO.UIOption;

namespace ChrManager.Infrastructure.Installation;

/// <summary>
/// File and directory removal. "to recycle bin" goes through the managed
/// Microsoft.VisualBasic wrapper instead of hand written shell interop.
/// </summary>
internal static class FileOperations
{
	public static void DeleteDirectory (string path, bool toRecycleBin)
	{
		ArgumentException.ThrowIfNullOrEmpty(path);
		if (!Directory.Exists(path))
		{
			return;
		}

		if (toRecycleBin)
		{
			VisualFileIO.DeleteDirectory(path, VisualUIOption.OnlyErrorDialogs, VisualRecycleOption.SendToRecycleBin, VisualCancelOption.DoNothing);
			return;
		}

		ClearReadOnlyAttributes(path);
		Directory.Delete(path, recursive: true);
	}

	/// <summary>A file is in use when it cannot be opened with exclusive access.</summary>
	public static bool IsInUse (string path)
	{
		if (!File.Exists(path))
		{
			return false;
		}

		try
		{
			using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
			return false;
		}
		catch (IOException)
		{
			return true;
		}
		catch (UnauthorizedAccessException)
		{
			return true;
		}
	}

	private static void ClearReadOnlyAttributes (string directory)
	{
		foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
		{
			FileAttributes attributes = File.GetAttributes(file);
			if ((attributes & FileAttributes.ReadOnly) != 0)
			{
				File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
			}
		}
	}
}
