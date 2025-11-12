using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace OrderFlow.Tests;

public class GatewayRateLimitTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;
    public GatewayRateLimitTests(GatewayFactory factory) => _factory = factory;

    [Fact]
    public async Task Proxy_RateLimit_ReturnsSome429s_WhenBurst25()
    {
        var client = _factory.CreateClient();
        // Send 25 requests quickly to a proxied path; some should hit 429 due to FixedWindow limits
        var tasks = Enumerable.Range(0, 25).Select(_ => client.GetAsync("/api/orders/ping"));
        var results = await Task.WhenAll(tasks);
        var count429 = results.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        count429.Should().BeGreaterThan(0);
    }
}

