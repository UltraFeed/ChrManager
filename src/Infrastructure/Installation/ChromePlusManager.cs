using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChrManager.Application;
using ChrManager.Application.Contracts;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Archives;
using ChrManager.Infrastructure.Configuration;
using ChrManager.Infrastructure.Network;
using ChrManager.Infrastructure.Persistence;
using ChrManager.Infrastructure.Startup;
using ChrManager.Presentation.Localization;

namespace ChrManager.Infrastructure.Installation;

internal sealed record ChromePayload (string File, string Hash, bool Temporary = true, string? PackageHash = null);

internal sealed class ChromePlusManager
{
	private readonly HttpClient _http;
	private readonly Downloader _downloader;
	private readonly AppPaths _paths;
	private readonly RunState _state;
	private readonly TimeProvider _time;
	private readonly Uri? _endpoint;
	private readonly IUserInterface _view;
	private readonly Serilog.ILogger? _log;
	public ChromePlusManager (HttpClient http, Downloader downloader, AppPaths paths, RunState state, TimeProvider time, IUserInterface view, Uri? endpoint = null, Serilog.ILogger? log = null)
	{
		_http = http;
		_downloader = downloader;
		_paths = paths;
		_state = state;
		_time = time;
		_view = view;
		_endpoint = endpoint;
		_log = log;
	}

	public static string ConfigurationHash (BrowserSettings browser)
	{
		return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new BrowserConfigurationFingerprint(browser.Flags, browser.ChromePlus), AppJsonContext.Type<BrowserConfigurationFingerprint>(ConfigLoader.Options))));
	}

	public async Task<ChromePayload> GetPayloadAsync (string architecture, CancellationToken token, string? installedDll = null)
	{
		DateTimeOffset now = _time.GetUtcNow();
		if (_state.State.ChromePlusAttempt is not { } last || now - last >= TimeSpan.FromDays(1))
		{
			_log?.Information(DiagnosticKind.ChromePlusChecking);
			_state.Save(_state.State with { ChromePlusAttempt = now });
			try
			{
				List<ReleaseAsset> assets = await GitHubRelease.ReadAssetsAsync(_http, _endpoint ?? new Uri("https://api.github.com/repos/Bush2021/chrome_plus/releases/latest"), token).ConfigureAwait(false);
				_state.Save(_state.State with { ChromePlusAssets = assets });
			}
			catch (HttpRequestException ex) when (_state.State.ChromePlusAssets.Count > 0)
			{
				_log?.Warning(ex, DiagnosticKind.ChromePlusMetadataFailed);
			}
		}

		List<ReleaseAsset> candidates = _state.State.ChromePlusAssets.Where(a => a.Name.Contains(architecture, StringComparison.OrdinalIgnoreCase) && (a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))).ToList();
		if (candidates.Count != 1)
		{
			throw new AppFailure(FailureKind.ChromePlusAssetMissing, architecture);
		}

		ReleaseAsset asset = candidates [0];
		string? hash = asset.Digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true ? asset.Digest [7..] : null;
		if (hash is not null && installedDll is not null && File.Exists(installedDll))
		{
			string installedHash = await Downloader.FileSha256Async(installedDll, token).ConfigureAwait(false);
			string ini = Path.Combine(Path.GetDirectoryName(installedDll)!, "chrome++.ini");
			string? installedIni = File.Exists(ini) ? await File.ReadAllTextAsync(ini, token).ConfigureAwait(false) : null;
			bool matchingReceipt = installedIni?.StartsWith(HashHeader(hash, installedHash) + AppInfo.IniMarker, StringComparison.OrdinalIgnoreCase) == true;
			bool matchingDirectDll = asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && installedHash.Equals(hash, StringComparison.OrdinalIgnoreCase);
			if ((matchingReceipt || matchingDirectDll) && BrowserResolver.Architecture(installedDll) == architecture)
			{
				return new(installedDll, installedHash, false, hash);
			}
		}

		string package = _paths.CreateTemporaryFile("chrome-plus-package", Path.GetExtension(asset.Name));
		string unpacked = _paths.CreateTemporaryFile("chrome-plus-unpacked");
		string dll = _paths.CreateTemporaryFile("chrome-plus", ".dll");
		try
		{
			string packageHash;
			IDownloadProgress progress = _view.BeginDownload(_view.Language.Format(StringId.ChromePlusDownloading, asset.Name));
			await using (progress.ConfigureAwait(false))
			{
				packageHash = await _downloader.DownloadAsync(new Uri(asset.Url), package, hash, asset.Size, progress, token).ConfigureAwait(false);
			}

			if (asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
			{
				File.Move(package, dll);
			}
			else
			{
				await ArchiveExtractor.ExtractAsync(package, unpacked, null, token).ConfigureAwait(false);
				string [] dlls = Directory.GetFiles(unpacked, "version.dll", SearchOption.AllDirectories);
				List<string> compatible = dlls.Where(BrowserResolver.IsX64).ToList();
				if (compatible.Count != 1)
				{
					throw new AppFailure(FailureKind.ChromePlusDllMissing);
				}

				File.Copy(compatible [0], dll);
			}

			if (BrowserResolver.Architecture(dll) != architecture)
			{
				throw new AppFailure(FailureKind.InvalidChromePlusArchitecture);
			}

			return new(dll, await Downloader.FileSha256Async(dll, token).ConfigureAwait(false), PackageHash: packageHash);
		}
		catch
		{
			_paths.ReleaseTemporaryFile(dll);
			throw;
		}
		finally
		{
			_paths.ReleaseTemporaryFile(package);
			_paths.ReleaseTemporaryFile(unpacked);
		}
	}

	public static async Task ApplyAsync (string stage, BrowserSettings browser, ChromePayload payload, string? ownedHash, CancellationToken token)
	{
		string dll = Path.Combine(stage, "version.dll");
		if (File.Exists(dll))
		{
			string existing = await Downloader.FileSha256Async(dll, token).ConfigureAwait(false);
			if (!existing.Equals(ownedHash, StringComparison.OrdinalIgnoreCase) && !existing.Equals(payload.Hash, StringComparison.OrdinalIgnoreCase))
			{
				throw new AppFailure(FailureKind.ForeignDll);
			}
		}

		File.Copy(payload.File, dll, true);
		await File.WriteAllTextAsync(Path.Combine(stage, "chrome++.ini"), RenderIni(browser, payload), new UTF8Encoding(false), token).ConfigureAwait(false);
	}

	private static string HashHeader (string packageHash, string dllHash)
	{
		return $"; package-sha256={packageHash.ToUpperInvariant()}\r\n; dll-sha256={dllHash.ToUpperInvariant()}\r\n";
	}

	public static bool IsGeneratedIni (string text)
	{
		using StringReader reader = new(text);
		string? marker = reader.ReadLine();
		if (marker?.StartsWith("; package-sha256=", StringComparison.Ordinal) == true
			&& reader.ReadLine()?.StartsWith("; dll-sha256=", StringComparison.Ordinal) == true)
		{
			marker = reader.ReadLine();
		}

		return marker?.StartsWith(AppInfo.IniMarker, StringComparison.Ordinal) == true;
	}

	public static string RenderIni (BrowserSettings browser, ChromePayload? payload = null)
	{
		Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.OrdinalIgnoreCase);
		foreach ((string? section, Dictionary<string, string>? values) in browser.ChromePlus.Settings)
		{
			if (!sections.TryAdd(section, new(values, StringComparer.OrdinalIgnoreCase)))
			{
				throw new AppFailure(FailureKind.DuplicateChromePlusSection, section);
			}
		}

		Dictionary<string, string> general = sections ["general"];
		general ["data_dir"] = "\"%app%\\..\\profile\"";
		general ["cache_dir"] = "\"%app%\\..\\cache\"";
		general ["command_line"] = string.Join(" ", browser.Flags.Select(QuoteArgument));
		general ["managed_updates"] = "0";
		StringBuilder output = new(AppInfo.IniHeader);
		if (payload?.PackageHash is { } packageHash)
		{
			_ = output.Insert(0, HashHeader(packageHash, payload.Hash));
		}

		foreach ((string? section, Dictionary<string, string>? values) in sections)
		{
			_ = output.Append('[').Append(section).AppendLine("]");
			foreach ((string? key, string? value) in values)
			{
				_ = output.Append(key).Append('=').AppendLine(value);
			}

			_ = output.AppendLine();
		}

		return output.ToString();
	}

	// CommandLineToArgvW-compatible encoding, also handles quoted flag values.
	internal static string QuoteArgument (string value)
	{
		if (value.Length > 0 && !value.Any(c => char.IsWhiteSpace(c) || c == '"'))
		{
			return value;
		}

		StringBuilder result = new("\"");
		int slashes = 0;
		foreach (char c in value)
		{
			if (c == '\\')
			{
				slashes++;
				continue;
			}

			_ = result.Append('\\', c == '"' ? (slashes * 2) + 1 : slashes);
			_ = result.Append(c);
			slashes = 0;
		}

		return result.Append('\\', slashes * 2).Append('"').ToString();
	}
}

internal static class GitHubRelease
{
	public static async Task<List<ReleaseAsset>> ReadAssetsAsync (HttpClient http, Uri endpoint, CancellationToken token)
	{
		// ResponseHeadersRead does not apply HttpClient.Timeout to subsequent body reads.
		using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
		deadline.CancelAfter(http.Timeout);
		return await ReadAssetsCoreAsync(http, endpoint, deadline.Token).ConfigureAwait(false);
	}

	private static async Task<List<ReleaseAsset>> ReadAssetsCoreAsync (HttpClient http, Uri endpoint, CancellationToken token)
	{
		try
		{
			using HttpRequestMessage request = new(HttpMethod.Get, endpoint);
			request.Headers.UserAgent.ParseAdd(AppInfo.UserAgent);
			using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
			_ = response.EnsureSuccessStatusCode();
			using Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
			using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
			if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
			{
				throw new AppFailure(FailureKind.InvalidGithubMetadata);
			}

			List<ReleaseAsset> result = assets.EnumerateArray().Select(ReadAsset).ToList();
			if (result.Count == 0)
			{
				throw new AppFailure(FailureKind.GithubAssetsMissing);
			}

			return result;
		}
		catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or AppFailure or KeyNotFoundException or InvalidOperationException or FormatException || (ex is OperationCanceledException && !token.IsCancellationRequested))
		{
			Serilog.Log.Logger.Debug(ex, DiagnosticKind.GitHubFallback);
			return await ReadRedirectAsync(http, token).ConfigureAwait(false);
		}
	}

	private static ReleaseAsset ReadAsset (JsonElement asset)
	{
		string name = ReadRequiredString(asset, "name");
		string url = ReadRequiredString(asset, "browser_download_url");
		if (!asset.TryGetProperty("size", out JsonElement length) || !length.TryGetInt64(out long size) || size < 0)
		{
			throw new AppFailure(FailureKind.GithubAssetFieldMissing, "size");
		}

		string? checksum = asset.TryGetProperty("digest", out JsonElement digest) ? digest.GetString() : null;
		return new(name, url, size, checksum);
	}

	private static string ReadRequiredString (JsonElement asset, string key)
	{
		if (asset.ValueKind != JsonValueKind.Object || !asset.TryGetProperty(key, out JsonElement value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
		{
			throw new AppFailure(FailureKind.GithubAssetFieldMissing, key);
		}

		return value.GetString()!;
	}

	private static async Task<List<ReleaseAsset>> ReadRedirectAsync (HttpClient http, CancellationToken token)
	{
		Uri latest = new("https://github.com/Bush2021/chrome_plus/releases/latest");
		using HttpResponseMessage response = await http.GetAsync(latest, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
		_ = response.EnsureSuccessStatusCode();
		Uri release = response.RequestMessage?.RequestUri ?? throw new AppFailure(FailureKind.GithubRedirectMissing);
		const string prefix = "/Bush2021/chrome_plus/releases/tag/";
		bool expectedRepository = release.Scheme == Uri.UriSchemeHttps && release.Host == "github.com" && release.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal);
		if (!expectedRepository)
		{
			throw new AppFailure(FailureKind.GithubRedirectInvalid);
		}

		string tag = Uri.UnescapeDataString(release.AbsolutePath [prefix.Length..]);
		if (!Version.TryParse(tag, out _))
		{
			throw new AppFailure(FailureKind.GithubTagInvalid);
		}

		string name = $"Chrome++_v{tag}_x86_x64_arm64.7z";
		Uri downloadBase = new($"https://github.com/Bush2021/chrome_plus/releases/download/{Uri.EscapeDataString(tag)}/");
		Uri package = new(downloadBase, Uri.EscapeDataString(name));
		using HttpResponseMessage manifest = await http.GetAsync(new Uri(downloadBase, "update.json"), token).ConfigureAwait(false);
		if (manifest.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return [new(name, package.AbsoluteUri, 0, null)];
		}

		_ = manifest.EnsureSuccessStatusCode();
		using Stream stream = await manifest.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
		using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
		JsonElement root = document.RootElement;
		string hash = ReadRequiredString(root, "sha256");
		if (ReadRequiredString(root, "version") != tag || hash.Length != 64 || !hash.All(Uri.IsHexDigit)
			|| !root.TryGetProperty("size", out JsonElement length) || length.ValueKind != JsonValueKind.Number
			|| !length.TryGetInt64(out long size) || size <= 0)
		{
			throw new AppFailure(FailureKind.InvalidGithubMetadata);
		}

		return [new(name, package.AbsoluteUri, size, "sha256:" + hash)];
	}
}
