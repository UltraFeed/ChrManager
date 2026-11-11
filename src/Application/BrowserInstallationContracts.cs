using ChrManager.Application.Contracts;
using ChrManager.Application.Models;

namespace ChrManager.Application;

internal interface IBrowserInstallationFactory
{
	public Task<IManagedBrowserInstallation> OpenAsync (BrowserSettings browser, IReadOnlyList<Build> builds, CancellationToken token);
}

internal interface IManagedBrowserInstallation : IDisposable
{
	public BrowserUpdatePlan Plan { get; }

	public Task DownloadAsync (IProgress<DownloadProgress> progress, CancellationToken token);
	public Task ExtractAsync (IProgress<double> progress, CancellationToken token);
	public void CopyToStage ();
	public Task ConfigureStageAsync (CancellationToken token);
	public bool IsLocked ();
	public void Commit ();
}
