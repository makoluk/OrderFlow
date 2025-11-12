using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace OrderFlow.Tests;

public class PaymentServiceTests : IClassFixture<PaymentServiceFactory>
{
    private readonly PaymentServiceFactory _factory;
    public PaymentServiceTests(PaymentServiceFactory factory) => _factory = factory;

    [Fact]
    public async Task Root_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Health_ReturnsOkBaseResponse()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.TryGetProperty("data", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Ready_ReturnsOkBaseResponse()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/ready");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.TryGetProperty("data", out _).Should().BeTrue();
    }
}

