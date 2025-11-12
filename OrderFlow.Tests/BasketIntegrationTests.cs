using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace OrderFlow.Tests;

public class BasketIntegrationTests : IClassFixture<OrderServiceFactory>
{
    private readonly OrderServiceFactory _factory;

    public BasketIntegrationTests(OrderServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Basket_Add_And_Get_Works()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Customer-Id", "c1");

        var add1 = new { productId = "p1", name = "Prod 1", quantity = 1, unitPrice = 10m, currency = "TRY" };
        var add2 = new { productId = "p2", name = "Prod 2", quantity = 2, unitPrice = 5m, currency = "TRY" };

        var r1 = await client.PostAsJsonAsync("/basket/items", add1);
        r1.StatusCode.Should().Be(HttpStatusCode.OK);

        var r2 = await client.PostAsJsonAsync("/basket/items", add2);
        r2.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync("/basket");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await get.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("customerId").GetString().Should().Be("c1");
        data.GetProperty("items").GetArrayLength().Should().Be(2);
        data.GetProperty("total").GetDecimal().Should().Be(20m);
    }

    [Fact]
    public async Task Basket_Remove_And_Clear_Works()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Customer-Id", "c2");

        var add = new { productId = "p1", name = "P", quantity = 2, unitPrice = 7.5m, currency = "TRY" };
        await client.PostAsJsonAsync("/basket/items", add);

        var remove = await client.DeleteAsync("/basket/items/p1");
        remove.StatusCode.Should().Be(HttpStatusCode.OK);

        var getAfterRemove = await client.GetAsync("/basket");
        var doc1 = JsonDocument.Parse(await getAfterRemove.Content.ReadAsStringAsync());
        doc1.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().Be(0);

        var clear = await client.DeleteAsync("/basket");
        clear.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc2 = JsonDocument.Parse(await clear.Content.ReadAsStringAsync());
        doc2.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Checkout_EmptyBasket_Returns_BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Customer-Id", "c3");
        client.DefaultRequestHeaders.Add("Idempotency-Key", "idem-c3-1");

        var resp = await client.PostAsync("/orders/checkout", content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Checkout_MixedCurrencies_Returns_BadRequest()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Customer-Id", "c4");

        await client.PostAsJsonAsync("/basket/items", new { productId = "t1", name = "T1", quantity = 1, unitPrice = 10m, currency = "TRY" });
        await client.PostAsJsonAsync("/basket/items", new { productId = "u1", name = "U1", quantity = 1, unitPrice = 10m, currency = "USD" });

        client.DefaultRequestHeaders.Add("Idempotency-Key", "idem-c4-1");
        var resp = await client.PostAsync("/orders/checkout", null);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Checkout_Succeeds_And_Clears_Basket()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Customer-Id", "c5");

        await client.PostAsJsonAsync("/basket/items", new { productId = "p1", name = "P1", quantity = 1, unitPrice = 12m, currency = "TRY" });
        await client.PostAsJsonAsync("/basket/items", new { productId = "p2", name = "P2", quantity = 2, unitPrice = 4m, currency = "TRY" });

        client.DefaultRequestHeaders.Add("Idempotency-Key", "idem-c5-1");
        var resp = await client.PostAsync("/orders/checkout", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Basket is cleared
        var get = await client.GetAsync("/basket");
        var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Checkout_Idempotency_ReturnsExistingOrder_OnSecondCall()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Customer-Id", "c6");

        await client.PostAsJsonAsync("/basket/items", new { productId = "p1", name = "P1", quantity = 1, unitPrice = 10m, currency = "TRY" });
        client.DefaultRequestHeaders.Add("Idempotency-Key", "idem-c6-1");

        var first = await client.PostAsync("/orders/checkout", null);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsync("/orders/checkout", null);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("message").GetString().Should().Be("Order already exists");
    }
}


