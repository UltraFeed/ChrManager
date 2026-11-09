using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using ChrManager.Application;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Startup;

namespace ChrManager.Infrastructure.Network;

internal sealed class SelfUpdateSource : ISelfUpdateSource
{
	private readonly HttpClient _http;
	private readonly Uri _endpoint;
	private readonly string _rid;
	public SelfUpdateSource (HttpClient http, Uri? endpoint = null, string? rid = null)
	{
		_http = http;
		_endpoint = endpoint ?? ProductionEndpoint;
		_rid = rid ?? (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64");
	}

	public static Uri ProductionEndpoint { get; } = new("https://api.github.com/repos/UltraFeed/ChrManager/releases/latest");

	public async Task<SelfRelease?> LatestAsync (CancellationToken token)
	{
		using HttpRequestMessage request = new(HttpMethod.Get, _endpoint);
		request.Headers.UserAgent.ParseAdd(AppInfo.UserAgent);
		using HttpResponseMessage response = await _http.SendAsync(request, token).ConfigureAwait(false);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}

		_ = response.EnsureSuccessStatusCode();
		try
		{
			using Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
			using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: token).ConfigureAwait(false);
			return ReadRelease(document.RootElement);
		}
		catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
		{
			throw new AppFailure(FailureKind.InvalidSelfUpdateRelease, ex);
		}
	}

	private SelfRelease ReadRelease (JsonElement release)
	{
		string? tag = release.GetProperty("tag_name").GetString();
		if (tag is null || !tag.StartsWith('v') || !Version.TryParse(tag [1..], out _))
		{
			throw new AppFailure(FailureKind.InvalidSelfUpdateRelease);
		}

		string expected = AppInfo.Name + "-" + _rid + ".exe";
		JsonElement selected = default;
		foreach (JsonElement asset in release.GetProperty("assets").EnumerateArray())
		{
			if (asset.GetProperty("name").GetString() != expected)
			{
				continue;
			}

			if (selected.ValueKind != JsonValueKind.Undefined)
			{
				throw new AppFailure(FailureKind.InvalidSelfUpdateRelease);
			}

			selected = asset;
		}

		if (selected.ValueKind == JsonValueKind.Undefined)
		{
			throw new AppFailure(FailureKind.SelfUpdateAssetMissing, expected);
		}

		if (!selected.TryGetProperty("digest", out JsonElement digest) || digest.ValueKind != JsonValueKind.String || digest.GetString() is not { Length: 71 } hash || !hash.StartsWith("sha256:", StringComparison.Ordinal) || !hash [7..].All(Uri.IsHexDigit))
		{
			throw new AppFailure(FailureKind.SelfUpdateChecksumRequired);
		}

		if (!Uri.TryCreate(selected.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out Uri? url) || (url.Scheme != Uri.UriSchemeHttps && !(_endpoint.IsLoopback && url.IsLoopback && url.Scheme == Uri.UriSchemeHttp)))
		{
			throw new AppFailure(FailureKind.InvalidSelfUpdateRelease);
		}

		return new(tag [1..], _rid, url.AbsoluteUri, hash [7..]);
	}
}
