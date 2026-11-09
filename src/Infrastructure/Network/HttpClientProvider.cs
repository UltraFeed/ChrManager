using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace ChrManager.Infrastructure.Network;

/// <summary>
/// The two HttpClients of the process. The feed client gets the standard resilience
/// pipeline (retries with backoff, circuit breaker, timeouts); a package download is
/// deliberately unbounded and is only stopped by cancellation.
/// </summary>
internal sealed class HttpClientProvider : IDisposable
{
	public const string FeedClient = "feed";
	public const string DownloadClient = "download";
	private readonly ServiceProvider _services;
	private readonly TimeSpan _attemptTimeout;
	public HttpClientProvider (int timeoutSeconds)
	{
		_attemptTimeout = TimeSpan.FromSeconds(timeoutSeconds);
		ServiceCollection services = new();
		IHttpClientBuilder feed = services.AddHttpClient(FeedClient).ConfigurePrimaryHttpMessageHandler(CreateFeedHandler);
		_ = feed.AddStandardResilienceHandler(ConfigureResilience);
		_ = feed.ConfigureHttpClient(ConfigureFeedClient);
		_ = services.AddHttpClient(DownloadClient, ConfigureDownloadClient).ConfigurePrimaryHttpMessageHandler(CreateDownloadHandler);
		_services = services.BuildServiceProvider();
		IHttpClientFactory factory = _services.GetRequiredService<IHttpClientFactory>();
		Feed = factory.CreateClient(FeedClient);
		Download = factory.CreateClient(DownloadClient);
	}

	public HttpClient Feed { get; }
	public HttpClient Download { get; }

	public void Dispose ()
	{
		_services.Dispose();
	}

	private void ConfigureFeedClient (HttpClient client)
	{
		client.Timeout = _attemptTimeout * 3;
	}
	private static void ConfigureDownloadClient (HttpClient client)
	{
		client.Timeout = Timeout.InfiniteTimeSpan;
	}
	private HttpMessageHandler CreateFeedHandler () { return CreateHandler(true); }
	private HttpMessageHandler CreateDownloadHandler () { return CreateHandler(false); }
	private void ConfigureResilience (Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions options)
	{
		options.AttemptTimeout.Timeout = _attemptTimeout;
		options.TotalRequestTimeout.Timeout = _attemptTimeout * 3;
		options.CircuitBreaker.SamplingDuration = _attemptTimeout * 2;
	}
	private SocketsHttpHandler CreateHandler (bool decompress)
	{
		return new SocketsHttpHandler
		{
			UseCookies = false,
			ConnectTimeout = _attemptTimeout,
			AllowAutoRedirect = true,
			AutomaticDecompression = decompress ? DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli : DecompressionMethods.None,
			PooledConnectionLifetime = TimeSpan.FromMinutes(5),
		};
	}
}
