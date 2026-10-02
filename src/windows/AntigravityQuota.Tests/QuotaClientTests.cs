using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AntigravityQuota.Core;
using AntigravityQuota.Core.Models;
using Xunit;

namespace AntigravityQuota.Tests;

public class QuotaClientTests
{
    private const string SampleSummaryJson = """
    {
        "userStatus": {
            "cascadeModelConfigData": {
                "clientModelConfigs": []
            }
        },
        "response": {
            "groups": [
                {
                    "displayName": "Claude 3.5 Sonnet",
                    "description": "Claude models",
                    "buckets": [
                        {
                            "bucketId": "claude-5h",
                            "window": "5h",
                            "remainingFraction": 0.85,
                            "resetTime": "2026-10-02T22:30:00Z"
                        }
                    ]
                },
                {
                    "displayName": "Gemini 1.5 Pro",
                    "description": "Gemini models",
                    "buckets": [
                        {
                            "bucketId": "gemini-5h",
                            "window": "5h",
                            "remainingFraction": 1.0,
                            "resetTime": "2026-10-02T23:00:00Z"
                        }
                    ]
                }
            ]
        }
    }
    """;

    private const string SampleModelsJson = """
    {
        "clientModelConfigs": [
            {
                "modelId": "claude-3-5-sonnet",
                "label": "Claude 3.5 Sonnet",
                "remainingFraction": 0.85,
                "resetTime": "2026-10-02T22:30:00Z"
            },
            {
                "modelId": "gemini-1-5-pro",
                "label": "Gemini 1.5 Pro",
                "remainingFraction": 1.0,
                "resetTime": "2026-10-02T23:00:00Z"
            }
        ]
    }
    """;

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
        public List<HttpRequestMessage> CapturedRequests { get; } = new();

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequests.Add(request);
            return Task.FromResult(_responseFactory(request));
        }
    }

    [Fact]
    public void CreateRpcRequest_BuildsValidPostRequestWithRequiredHeaders()
    {
        int port = 54321;
        string method = "RetrieveUserQuotaSummary";
        string csrfToken = "test-csrf-secret-123";
        string jsonBody = """{"forceRefresh":true}""";

        using var request = QuotaClient.CreateRpcRequest(port, method, csrfToken, jsonBody, useTls: false);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"http://127.0.0.1:{port}/exa.language_server_pb.LanguageServerService/{method}", request.RequestUri?.ToString());
        Assert.NotNull(request.Content);
        Assert.Equal("application/json", request.Content.Headers.ContentType?.MediaType);

        Assert.True(request.Headers.TryGetValues("Connect-Protocol-Version", out var protoVersions));
        Assert.Contains("1", protoVersions);

        Assert.True(request.Headers.TryGetValues("x-codeium-csrf-token", out var csrfValues));
        Assert.Contains(csrfToken, csrfValues);
    }

    [Fact]
    public void CreateRpcRequest_WithTls_UsesHttpsScheme()
    {
        int port = 54321;
        string method = "GetCascadeModelConfigData";
        string csrfToken = "token-abc";

        using var request = QuotaClient.CreateRpcRequest(port, method, csrfToken, "{}", useTls: true);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"https://127.0.0.1:{port}/exa.language_server_pb.LanguageServerService/{method}", request.RequestUri?.ToString());
    }

    [Fact]
    public async Task FetchSnapshotAsync_WhenServerRunning_FetchesSummaryAndModels()
    {
        var endpoint = new ServerEndpoint(1234, "csrf-test-token", new[] { 50001 });

        var handler = new MockHttpMessageHandler(req =>
        {
            var uri = req.RequestUri?.ToString() ?? string.Empty;
            if (uri.Contains("RetrieveUserQuotaSummary"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleSummaryJson, Encoding.UTF8, "application/json")
                };
            }
            if (uri.Contains("GetCascadeModelConfigData"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleModelsJson, Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = new QuotaClient(handler, () => endpoint);

        var snapshot = await client.FetchSnapshotAsync(forceRefresh: true);

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.Groups.Count);
        Assert.Equal(2, snapshot.Models.Count);
        Assert.Equal(endpoint, client.CachedEndpoint);

        // Verify headers were sent properly
        Assert.NotEmpty(handler.CapturedRequests);
        foreach (var req in handler.CapturedRequests)
        {
            Assert.Contains(req.Headers.GetValues("Connect-Protocol-Version"), v => v == "1");
            Assert.Contains(req.Headers.GetValues("x-codeium-csrf-token"), v => v == "csrf-test-token");
        }
    }

    [Fact]
    public async Task FetchSnapshotAsync_WhenDiscoveryReturnsNull_ReturnsNullWithoutCallingHttp()
    {
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new QuotaClient(handler, () => null);

        var snapshot = await client.FetchSnapshotAsync();

        Assert.Null(snapshot);
        Assert.Empty(handler.CapturedRequests);
        Assert.Null(client.CachedEndpoint);
    }

    [Fact]
    public async Task FetchSnapshotAsync_WhenNetworkFails_PerformsRediscoveryAndRetries()
    {
        var staleEndpoint = new ServerEndpoint(1111, "stale-token", new[] { 50001 });
        var freshEndpoint = new ServerEndpoint(2222, "fresh-token", new[] { 50002 });

        int discoveryCallCount = 0;
        ServerEndpoint? EndpointFinder()
        {
            discoveryCallCount++;
            return discoveryCallCount == 1 ? staleEndpoint : freshEndpoint;
        }

        var handler = new MockHttpMessageHandler(req =>
        {
            var uri = req.RequestUri?.ToString() ?? string.Empty;
            // Requests to stale port fail
            if (uri.Contains(":50001/"))
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            // Requests to fresh port succeed
            if (uri.Contains(":50002/"))
            {
                if (uri.Contains("RetrieveUserQuotaSummary"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(SampleSummaryJson, Encoding.UTF8, "application/json")
                    };
                }
                if (uri.Contains("GetCascadeModelConfigData"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(SampleModelsJson, Encoding.UTF8, "application/json")
                    };
                }
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = new QuotaClient(handler, EndpointFinder);

        var snapshot = await client.FetchSnapshotAsync();

        Assert.NotNull(snapshot);
        Assert.Equal(freshEndpoint, client.CachedEndpoint);
        Assert.True(discoveryCallCount >= 2);
    }

    [Fact]
    public async Task FetchSnapshotAsync_WhenAllAttemptsFail_ClearsCacheAndReturnsNull()
    {
        var endpoint = new ServerEndpoint(1234, "csrf-fail", new[] { 50001 });

        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var client = new QuotaClient(handler, () => endpoint);

        var snapshot = await client.FetchSnapshotAsync();

        Assert.Null(snapshot);
        Assert.Null(client.CachedEndpoint);
    }

    [Fact]
    public void ConfigureCertificateValidation_AllowsLocalhostAnd127001()
    {
        var callback = QuotaClient.LocalCertificateValidationCallback;
        Assert.NotNull(callback);

        using var request127 = new HttpRequestMessage(HttpMethod.Get, "https://127.0.0.1:54321/test");
        bool allowed127 = callback(request127, null, null, SslPolicyErrors.RemoteCertificateNameMismatch);
        Assert.True(allowed127);

        using var requestLocalhost = new HttpRequestMessage(HttpMethod.Get, "https://localhost:54321/test");
        bool allowedLocalhost = callback(requestLocalhost, null, null, SslPolicyErrors.RemoteCertificateChainErrors);
        Assert.True(allowedLocalhost);

        using var requestExternal = new HttpRequestMessage(HttpMethod.Get, "https://example.com/test");
        bool disallowedExternal = callback(requestExternal, null, null, SslPolicyErrors.RemoteCertificateNameMismatch);
        Assert.False(disallowedExternal);
    }
}
