using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace OrderFlow.Tests;

public class OrderServiceTests : IClassFixture<OrderServiceFactory>
{
    private readonly OrderServiceFactory _factory;

    public OrderServiceTests(OrderServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostOrders_WithIdempotencyKey_IsCreated_AndSecondCallReturnsExisting()
    {
        var client = _factory.CreateClient();

        var idem = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("Idempotency-Key", idem);

        var body = new
        {
            amount = 123.45m,
            currency = "TRY",
            customerId = "test-user"
        };

        var resp1 = await client.PostAsJsonAsync("/orders", body);
        resp1.StatusCode.Should().Be(HttpStatusCode.Created);

        var json1 = await resp1.Content.ReadAsStringAsync();
        using var doc1 = JsonDocument.Parse(json1);
        doc1.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc1.RootElement.TryGetProperty("data", out var data1).Should().BeTrue();
        data1.TryGetProperty("orderId", out var orderId1).Should().BeTrue();
        var orderId = orderId1.GetGuid();
        orderId.Should().NotBeEmpty();

        // second call with same key
        var resp2 = await client.PostAsJsonAsync("/orders", body);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK);

        var json2 = await resp2.Content.ReadAsStringAsync();
        using var doc2 = JsonDocument.Parse(json2);
        doc2.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc2.RootElement.GetProperty("message").GetString().Should().Be("Order already exists");
    }

    [Fact]
    public async Task GetOrder_NotFound_ReturnsBaseResponseFail()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/orders/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("message").GetString().Should().Be("Order not found");
    }
}

