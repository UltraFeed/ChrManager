using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using ChrManager.Application;
using ChrManager.Infrastructure.Persistence;

namespace ChrManager.Infrastructure.Network;

internal sealed class Downloader
{
	private readonly HttpClient _http;
	private readonly Action<long, long>? _sizeWarning;
	public Downloader (HttpClient http, Action<long, long>? sizeWarning = null)
	{
		_http = http;
		_sizeWarning = sizeWarning;
	}

	public async Task<string> DownloadAsync (Uri url, string target, string? expectedHash, long expectedSize, IProgress<double>? progress, CancellationToken token)
	{
		if (url.Scheme != Uri.UriSchemeHttps && !(url.IsLoopback && url.Scheme == Uri.UriSchemeHttp))
		{
			throw new AppFailure(FailureKind.DownloadRequiresHttps);
		}

		PortablePaths.RejectReparsePoints(target);
		if (File.Exists(target))
		{
			File.Delete(target);
		}

		_ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
		string partial = target + ".part";
		PortablePaths.RejectReparsePoints(partial);
		try
		{
			using HttpResponseMessage response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
			_ = response.EnsureSuccessStatusCode();
			long? length = response.Content.Headers.ContentLength;
			Stream input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);

			await using ConfiguredAsyncDisposable inputLifetime = input.ConfigureAwait(false);
			using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			long count = 0;
			FileStream file = new(partial, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough);
			await using (file.ConfigureAwait(false))
			{
				byte [] buffer = new byte [65536];
				int read;
				while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
				{
					await file.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
					hash.AppendData(buffer, 0, read);
					count += read;
					progress?.Report((length ?? expectedSize) > 0 ? count * 100d / (length ?? expectedSize) : 0);
				}

				await file.FlushAsync(token).ConfigureAwait(false);
			}

			if (length.HasValue && count != length)
			{
				throw new AppFailure(FailureKind.IncompleteDownload, length.Value, count);
			}

			if (expectedSize > 0 && count != expectedSize)
			{
				_sizeWarning?.Invoke(expectedSize, count);
			}

			string actual = Convert.ToHexStringLower(hash.GetHashAndReset());
			if (!string.IsNullOrWhiteSpace(expectedHash) && !actual.Equals(expectedHash.Trim(), StringComparison.OrdinalIgnoreCase))
			{
				throw new AppFailure(FailureKind.ChecksumMismatch);
			}

			File.Move(partial, target);
			return actual;
		}
		finally
		{
			if (File.Exists(partial))
			{
				File.Delete(partial);
			}
		}
	}

	public static async Task<string> FileSha256Async (string path, CancellationToken token)
	{
		FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);

		await using ConfiguredAsyncDisposable streamLifetime = stream.ConfigureAwait(false);
		return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
	}
}
