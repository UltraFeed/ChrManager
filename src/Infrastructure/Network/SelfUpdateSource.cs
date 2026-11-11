using System.Net;
using System.Text.Json;
using ChrManager.Application;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Startup;

namespace ChrManager.Infrastructure.Network;

internal sealed class SelfUpdateSource : ISelfUpdateSource
{
	private readonly HttpClient _http;
	private readonly Uri _endpoint;
	public SelfUpdateSource (HttpClient http, Uri? endpoint = null)
	{
		_http = http;
		_endpoint = endpoint ?? ProductionEndpoint;
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

		string version = tag[1..];
		if (VersionOrder.Compare(version, AppInfo.Version) <= 0)
		{
			return new(version);
		}

		string expected = AppInfo.ExecutableName;
		JsonElement selected = default;
		foreach (JsonElement asset in release.GetProperty("assets").EnumerateArray())
		{
			if (!string.Equals(asset.GetProperty("name").GetString(), expected, StringComparison.OrdinalIgnoreCase))
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

		return new(version, new(url.AbsoluteUri, hash [7..]));
	}
}
