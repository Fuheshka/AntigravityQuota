using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AntigravityQuota.Core.Models;

namespace AntigravityQuota.Core;

/// <summary>
/// Connect-RPC client for communicating with Google Antigravity language_server process on localhost.
/// </summary>
public class QuotaClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;
    private readonly Func<ServerEndpoint?> _endpointFinder;
    private ServerEndpoint? _cachedEndpoint;

    public ServerEndpoint? CachedEndpoint => _cachedEndpoint;

    public static Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool> LocalCertificateValidationCallback =>
        ValidateLocalCertificate;

    public QuotaClient(
        HttpClient? httpClient = null,
        Func<ServerEndpoint?>? endpointFinder = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
        }
        else
        {
            _httpClient = CreateDefaultHttpClient();
            _disposeClient = true;
        }

        _endpointFinder = endpointFinder ?? ServerDiscovery.DiscoverActiveServer;
    }

    public QuotaClient(
        HttpMessageHandler handler,
        Func<ServerEndpoint?>? endpointFinder = null)
        : this(new HttpClient(handler), endpointFinder)
    {
        _disposeClient = true;
    }

    public void ClearCache()
    {
        _cachedEndpoint = null;
    }

    public static bool ValidateLocalCertificate(
        HttpRequestMessage request,
        X509Certificate2? cert,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        var host = request.RequestUri?.Host;
        if (string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return errors == SslPolicyErrors.None;
    }

    public static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
                ValidateLocalCertificate(req, cert, chain, errors)
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public static HttpRequestMessage CreateRpcRequest(
        int port,
        string method,
        string csrfToken,
        string jsonBody = "{}",
        bool useTls = false)
    {
        string scheme = useTls ? "https" : "http";
        var uri = new Uri($"{scheme}://127.0.0.1:{port}/exa.language_server_pb.LanguageServerService/{method}");
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Connect-Protocol-Version", "1");
        request.Headers.TryAddWithoutValidation("x-codeium-csrf-token", csrfToken);
        return request;
    }

    public async Task<QuotaSnapshot?> FetchSnapshotAsync(
        bool forceRefresh = true,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _cachedEndpoint ?? _endpointFinder();
        if (endpoint == null)
        {
            _cachedEndpoint = null;
            return null;
        }

        try
        {
            var snapshot = await ExecuteFetchAsync(endpoint, forceRefresh, cancellationToken).ConfigureAwait(false);
            if (snapshot != null)
            {
                _cachedEndpoint = endpoint;
                return snapshot;
            }
        }
        catch
        {
            // Initial attempt failed, proceed to rediscovery
        }

        // Rediscovery on network / server failure
        _cachedEndpoint = null;
        var freshEndpoint = _endpointFinder();
        if (freshEndpoint == null)
        {
            return null;
        }

        try
        {
            var snapshot = await ExecuteFetchAsync(freshEndpoint, forceRefresh, cancellationToken).ConfigureAwait(false);
            if (snapshot != null)
            {
                _cachedEndpoint = freshEndpoint;
                return snapshot;
            }
        }
        catch
        {
            // Secondary attempt also failed
        }

        return null;
    }

    private async Task<QuotaSnapshot?> ExecuteFetchAsync(
        ServerEndpoint endpoint,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        string refreshPayload = forceRefresh ? "{\"forceRefresh\":true}" : "{\"forceRefresh\":false}";

        string summaryJson = await CallRpcAsync(
            endpoint,
            "RetrieveUserQuotaSummary",
            refreshPayload,
            cancellationToken).ConfigureAwait(false);

        var groups = QuotaParser.ParseSummary(summaryJson);
        if (groups.Count == 0)
        {
            return null;
        }

        IReadOnlyList<ModelConfig> models = Array.Empty<ModelConfig>();
        try
        {
            string modelsJson = await CallRpcAsync(
                endpoint,
                "GetCascadeModelConfigData",
                "{}",
                cancellationToken).ConfigureAwait(false);

            models = QuotaParser.ParseCascadeModelConfigs(modelsJson);
        }
        catch
        {
            // Model config is optional; fallback to empty list if language server does not provide it
        }

        return new QuotaSnapshot(groups, models, DateTimeOffset.UtcNow);
    }

    private async Task<string> CallRpcAsync(
        ServerEndpoint endpoint,
        string method,
        string jsonBody,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        foreach (int port in endpoint.Ports)
        {
            foreach (bool useTls in new[] { false, true })
            {
                try
                {
                    using var request = CreateRpcRequest(port, method, endpoint.CsrfToken, jsonBody, useTls);
                    using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }
        }

        throw lastException ?? new HttpRequestException($"Failed to call RPC method {method} on any available endpoint port.");
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
