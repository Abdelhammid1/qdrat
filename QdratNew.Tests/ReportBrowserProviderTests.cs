using Moq;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;
using QdratNew.Services.Reports;
using Xunit;

namespace QdratNew.Tests;

public sealed class ReportBrowserProviderTests : IDisposable
{
    private readonly string _cachePath = Path.Combine(Path.GetTempPath(), "qdrat-browser-tests", Guid.NewGuid().ToString("N"));
    private readonly Mock<IBrowserFetcher> _fetcher = new();
    private readonly InstalledBrowser _browser;

    public ReportBrowserProviderTests()
    {
        var realFetcher = new BrowserFetcher(new BrowserFetcherOptions { Path = _cachePath, Platform = Platform.Win64 });
        var executablePath = realFetcher.GetExecutablePath(Chrome.DefaultBuildId);
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        _browser = realFetcher.GetInstalledBrowsers().Single();
        _fetcher.SetupGet(x => x.CacheDir).Returns(_cachePath);
        _fetcher.Setup(x => x.GetExecutablePath(Chrome.DefaultBuildId)).Returns(_browser.GetExecutablePath());
    }

    [Fact]
    public async Task ConcurrentRequests_DownloadOnce_AndWaitForInstallation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _fetcher.Setup(x => x.DownloadAsync()).Returns(async () =>
        {
            started.SetResult();
            await finish.Task;
            InstallExecutable();
            return _browser;
        });
        var provider = new ReportBrowserProvider(_fetcher.Object);
        var first = provider.GetExecutablePathAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var others = Enumerable.Range(0, 10).Select(_ => provider.GetExecutablePathAsync()).ToArray();
        Assert.All(others, task => Assert.False(task.IsCompleted));
        finish.SetResult();
        var paths = await Task.WhenAll(others.Append(first)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.All(paths, path => Assert.Equal(_browser.GetExecutablePath(), path));
        _fetcher.Verify(x => x.DownloadAsync(), Times.Once);
    }

    [Fact]
    public async Task ExistingInstallation_IsReusedWithoutDownload()
    {
        InstallExecutable();
        var path = await new ReportBrowserProvider(_fetcher.Object).GetExecutablePathAsync();
        Assert.Equal(_browser.GetExecutablePath(), path);
        _fetcher.Verify(x => x.DownloadAsync(), Times.Never);
    }

    [Fact]
    public async Task FailedDownload_ReleasesLocks_AndNextRequestCanRetry()
    {
        _fetcher.SetupSequence(x => x.DownloadAsync())
            .ThrowsAsync(new IOException("Download interrupted"))
            .Returns(() => { InstallExecutable(); return Task.FromResult(_browser); });
        var provider = new ReportBrowserProvider(_fetcher.Object);
        await Assert.ThrowsAsync<IOException>(() => provider.GetExecutablePathAsync());
        Assert.Equal(_browser.GetExecutablePath(), await provider.GetExecutablePathAsync());
        _fetcher.Verify(x => x.DownloadAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task ExternalInstallationLock_PreventsDownload_AndWaitingCanBeCancelled()
    {
        Directory.CreateDirectory(_cachePath);
        using var externalLock = new FileStream(Path.Combine(_cachePath, ".qdrat-chrome-install.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var cancellation = new CancellationTokenSource();
        var provider = new ReportBrowserProvider(_fetcher.Object);
        var waiting = provider.GetExecutablePathAsync(cancellation.Token);
        Assert.False(waiting.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        _fetcher.Verify(x => x.DownloadAsync(), Times.Never);
    }

    private void InstallExecutable()
    {
        var path = _browser.GetExecutablePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test executable");
    }

    public void Dispose()
    {
        if (Directory.Exists(_cachePath))
            Directory.Delete(_cachePath, recursive: true);
    }
}
