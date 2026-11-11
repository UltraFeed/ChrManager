using System.Runtime.CompilerServices;
using ChrManager.Application;
using ChrManager.Infrastructure.Persistence;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace ChrManager.Infrastructure.Archives;

internal static class ArchiveExtractor
{
	public static async Task ExtractAsync (string archivePath, string target, IProgress<double>? progress, CancellationToken token)
	{
		PortablePaths.CheckTree(target);
		using IArchive inspection = ArchiveFactory.OpenArchive(archivePath);
		List<EntryInfo> entries = inspection.Entries.Select(InspectEntry).ToList();
		if (entries.Count == 0)
		{
			throw new AppFailure(FailureKind.ArchiveEmpty);
		}

		string root = ArchiveLayout.FindRootDirectory(entries);
		HashSet<string> destinations = new(StringComparer.OrdinalIgnoreCase);
		foreach (EntryInfo? entry in entries.Where(e => !e.IsDirectory))
		{
			string path = ArchiveLayout.MapEntryPath(entry, root)!;
			if (!destinations.Add(path))
			{
				throw new AppFailure(FailureKind.ArchiveDestinationDuplicate, path);
			}
		}

		_ = Directory.CreateDirectory(target);
		long total = entries.Where(e => !e.IsDirectory).Sum(e => e.Size), done = 0;
		IAsyncArchive archive = await ArchiveFactory.OpenAsyncArchive(archivePath, cancellationToken: token).ConfigureAwait(false);

		await using ConfiguredAsyncDisposable archiveLifetime = archive.ConfigureAwait(false);
		IAsyncReader reader = inspection.Type == ArchiveType.SevenZip || inspection.IsSolid ? await archive.ExtractAllEntriesAsync().ConfigureAwait(false) : await ReaderFactory.OpenAsyncReader(archivePath, cancellationToken: token).ConfigureAwait(false);

		await using ConfiguredAsyncDisposable readerLifetime = reader.ConfigureAwait(false);
		while (await reader.MoveToNextEntryAsync(token).ConfigureAwait(false))
		{
			IEntry entry = reader.Entry;
			ValidateEntry(entry);
			string? relative = ArchiveLayout.MapEntryPath(new(ArchiveLayout.NormalizePath(entry.Key), entry.IsDirectory, entry.Size), root);
			if (relative is null)
			{
				continue;
			}

			string destination = ArchiveLayout.ResolveDestination(target, relative) ?? throw new AppFailure(FailureKind.ArchiveEntryEscapes);
			PortablePaths.RejectReparsePoints(destination);
			if (entry.IsDirectory)
			{
				_ = Directory.CreateDirectory(destination);
				continue;
			}

			_ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
			await reader.WriteEntryToFileAsync(destination, new ExtractionOptions { Overwrite = false, ExtractFullPath = false, CheckCrc = true }, token).ConfigureAwait(false);
			done += entry.Size;
			progress?.Report(total == 0 ? 100 : done * 100d / total);
		}
	}

	private static EntryInfo InspectEntry (IEntry entry)
	{
		ValidateEntry(entry);
		return new(ArchiveLayout.NormalizePath(entry.Key), entry.IsDirectory, entry.Size);
	}
	private static void ValidateEntry (IEntry entry)
	{
		if (!string.IsNullOrEmpty(entry.LinkTarget) || entry.IsEncrypted || (entry.Attrib is int attributes && ((attributes & (int) FileAttributes.ReparsePoint) != 0 || ((attributes >> 16) & 0xf000) == 0xa000)))
		{
			throw new AppFailure(FailureKind.ArchiveEntryUnsupported);
		}

		_ = ArchiveLayout.NormalizePath(entry.Key);
	}
}
