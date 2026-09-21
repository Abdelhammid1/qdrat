using System.Diagnostics;
using PuppeteerSharp;
using PuppeteerSharp.BrowserData;

namespace QdratNew.Services.Reports;

/// <summary>Serializes Chrome installation for all PDF reports sharing this cache.</summary>
public sealed class ReportBrowserProvider(IBrowserFetcher fetcher)
{
    private readonly SemaphoreSlim _downloadGate = new(1, 1);

    public async Task<string> GetExecutablePathAsync(CancellationToken cancellationToken = default)
    {
        await _downloadGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(fetcher.CacheDir);
            // A file lock also coordinates separate worker processes sharing the cache.
            // Keep the lock file: deleting it can allow different processes to lock different files.
            using var installationLock = await AcquireInstallationLockAsync(cancellationToken);
            var executablePath = fetcher.GetExecutablePath(Chrome.DefaultBuildId);
            if (File.Exists(executablePath))
                return executablePath;

            // PuppeteerSharp has no cancellation token for downloads. Keep both locks until
            // download AND extraction finish, even if the originating HTTP request disconnects.
            var browser = await fetcher.DownloadAsync();
            executablePath = browser.GetExecutablePath();
            if (!File.Exists(executablePath))
                throw new FileNotFoundException(
                    "Chrome installation is incomplete. Check the Puppeteer browser cache.", executablePath);

            return executablePath;
        }
        finally
        {
            _downloadGate.Release();
        }
    }

    private async Task<FileStream> AcquireInstallationLockAsync(CancellationToken cancellationToken)
    {
        var lockPath = Path.Combine(fetcher.CacheDir, ".qdrat-chrome-install.lock");
        var timer = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 32 or 33)
            {
                if (timer.Elapsed >= TimeSpan.FromMinutes(2))
                    throw new TimeoutException("Timed out waiting for another process to install Chrome.", ex);

                await Task.Delay(200, cancellationToken);
            }
        }
    }
}
