using System.Diagnostics;
using System.Net.Http;
using NetMonitorPro.Core.Models;

namespace NetMonitorPro.Core.Services;

/// <summary>
/// Performs basic speed tests by downloading from well-known endpoints
/// and measuring throughput. Also measures latency via ICMP ping.
/// </summary>
public sealed class SpeedTestService : IDisposable
{
    private readonly HttpClient _http;
    private bool _disposed;

    /// Fires progress updates during a test: (phase, progressPercent, currentSpeedMbps)
    public event Action<string, int, double>? Progress;

    public SpeedTestService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "NetMonitorPro/1.0");
    }

    /// <summary>
    /// Runs a complete speed test: latency → download → upload estimate.
    /// Returns (downloadMbps, uploadMbps, latencyMs).
    /// </summary>
    public async Task<SpeedTestResult> RunAsync(CancellationToken ct = default)
    {
        // Phase 1: Latency test (ping several hosts)
        Progress?.Invoke("Measuring latency...", 0, 0);
        double latencyMs = await MeasureLatencyAsync(ct);

        // Phase 2: Download speed test
        Progress?.Invoke("Testing download speed...", 20, 0);
        double downloadMbps = await MeasureDownloadAsync(ct);

        // Phase 3: Upload estimate (lightweight — sends small payload)
        Progress?.Invoke("Testing upload speed...", 70, 0);
        double uploadMbps = await MeasureUploadAsync(ct);

        Progress?.Invoke("Complete!", 100, downloadMbps);

        return new SpeedTestResult
        {
            DownloadMbps = Math.Round(downloadMbps, 2),
            UploadMbps = Math.Round(uploadMbps, 2),
            LatencyMs = Math.Round(latencyMs, 1),
            Timestamp = DateTime.Now
        };
    }

    private async Task<double> MeasureLatencyAsync(CancellationToken ct)
    {
        var hosts = new[] { "8.8.8.8", "1.1.1.1", "208.67.222.222" };
        var latencies = new List<long>();

        using var pinger = new System.Net.NetworkInformation.Ping();

        foreach (var host in hosts)
        {
            try
            {
                var reply = await pinger.SendPingAsync(host, 3000);
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    latencies.Add(reply.RoundtripTime);
            }
            catch { /* skip unreachable hosts */ }

            ct.ThrowIfCancellationRequested();
        }

        return latencies.Count > 0 ? latencies.Average() : -1;
    }

    private async Task<double> MeasureDownloadAsync(CancellationToken ct)
    {
        // Use multiple test URLs for redundancy
        var testUrls = new[]
        {
            "https://speed.cloudflare.com/__down?bytes=10000000",   // 10 MB
            "https://proof.ovh.net/files/1Mb.dat",                   // 1 MB fallback
        };

        foreach (var url in testUrls)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                long totalBytes = 0;

                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                var buffer = new byte[81920];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    totalBytes += bytesRead;
                    ct.ThrowIfCancellationRequested();

                    var elapsed = sw.Elapsed.TotalSeconds;
                    if (elapsed > 0)
                    {
                        var currentMbps = (totalBytes * 8.0) / (elapsed * 1_000_000);
                        var percent = 20 + (int)(50.0 * Math.Min(elapsed / 10.0, 1.0));
                        Progress?.Invoke($"Downloading... {currentMbps:F1} Mbps", percent, currentMbps);
                    }
                }

                sw.Stop();

                if (sw.Elapsed.TotalSeconds > 0 && totalBytes > 0)
                {
                    return (totalBytes * 8.0) / (sw.Elapsed.TotalSeconds * 1_000_000);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { /* try next URL */ }
        }

        return 0;
    }

    private async Task<double> MeasureUploadAsync(CancellationToken ct)
    {
        // Upload test: POST random data to a test endpoint
        try
        {
            var payload = new byte[1_000_000]; // 1 MB
            Random.Shared.NextBytes(payload);

            var sw = Stopwatch.StartNew();
            using var content = new ByteArrayContent(payload);
            using var response = await _http.PostAsync("https://speed.cloudflare.com/__up", content, ct);
            sw.Stop();

            if (sw.Elapsed.TotalSeconds > 0)
            {
                return (payload.Length * 8.0) / (sw.Elapsed.TotalSeconds * 1_000_000);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* upload test failed */ }

        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
    }
}

/// <summary>
/// Result of a speed test run.
/// </summary>
public class SpeedTestResult
{
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public double LatencyMs { get; set; }
    public DateTime Timestamp { get; set; }
}
