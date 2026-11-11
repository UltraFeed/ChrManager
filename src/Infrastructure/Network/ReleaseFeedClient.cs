using System.Net.Http.Json;
using System.Text.Json;
using ChrManager.Application;
using ChrManager.Application.Contracts;
using ChrManager.Application.Models;
using ChrManager.Infrastructure.Configuration;
using ChrManager.Infrastructure.Persistence;

namespace ChrManager.Infrastructure.Network;

internal sealed class ReleaseFeedClient : IReleaseFeed
{
	private readonly HttpClient _http;
	private readonly RunState _state;
	private readonly TimeProvider _time;
	private readonly Uri? _endpoint;
	public ReleaseFeedClient (HttpClient http, RunState state, TimeProvider time, Uri? endpoint = null)
	{
		_http = http;
		_state = state;
		_time = time;
		_endpoint = endpoint;
	}

	public static Uri Endpoint { get; } = new("https://chromiumbuilds.org/api/v1/windows.json");

	private static readonly JsonSerializerOptions Options = new()
	{
		TypeInfoResolver = AppJsonContext.Default,
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
	};
	public async Task<IReadOnlyList<Build>> GetBuildsAsync (CancellationToken token)
	{
		DateTimeOffset now = _time.GetUtcNow();

		_state.Save(_state.State with { LastFeedAttempt = now });
		BuildFeed? feed = await _http.GetFromJsonAsync(_endpoint ?? Endpoint, AppJsonContext.Type<BuildFeed>(Options), token).ConfigureAwait(false);
		if (feed?.Builds is null || feed.Platform is not "windows")
		{
			throw new AppFailure(FailureKind.InvalidBuildCatalogue);
		}

		_state.Save(_state.State with { Feed = feed.Builds, LastFeedSuccess = now });
		return feed.Builds;
	}
}
