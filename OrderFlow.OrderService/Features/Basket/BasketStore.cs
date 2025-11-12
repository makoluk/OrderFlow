using System.Collections.Concurrent;

namespace OrderFlow.OrderService.Features.Basket;

public record BasketItem(string ProductId, string Name, int Quantity, decimal UnitPrice, string Currency);
public record BasketSnapshot(string CustomerId, IReadOnlyList<BasketItem> Items)
{
	public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);
}

public interface IBasketStore
{
	BasketSnapshot Get(string customerId);
	BasketSnapshot AddItem(string customerId, BasketItem item);
	BasketSnapshot RemoveItem(string customerId, string productId);
	void Clear(string customerId);
}

public class InMemoryBasketStore : IBasketStore
{
	private readonly ConcurrentDictionary<string, List<BasketItem>> _baskets = new(StringComparer.OrdinalIgnoreCase);

	public BasketSnapshot Get(string customerId)
	{
		var items = _baskets.TryGetValue(customerId, out var list) ? list : new List<BasketItem>();
		return new BasketSnapshot(customerId, new List<BasketItem>(items));
	}

	public BasketSnapshot AddItem(string customerId, BasketItem item)
	{
		_baskets.AddOrUpdate(
			customerId,
			_ => new List<BasketItem> { item },
			(_, existingList) =>
			{
				var newList = new List<BasketItem>(existingList);
				var idx = newList.FindIndex(i => i.ProductId == item.ProductId);
				if (idx < 0)
				{
					newList.Add(item);
				}
				else
				{
					var existing = newList[idx];
					newList[idx] = existing with { Quantity = existing.Quantity + item.Quantity, UnitPrice = item.UnitPrice, Name = item.Name, Currency = item.Currency };
				}
				return newList;
			});
		return Get(customerId);
	}

	public BasketSnapshot RemoveItem(string customerId, string productId)
	{
		_baskets.AddOrUpdate(
			customerId,
			_ => new List<BasketItem>(),
			(_, existingList) =>
			{
				var newList = new List<BasketItem>(existingList);
				newList.RemoveAll(i => i.ProductId == productId);
				return newList;
			});
		return Get(customerId);
	}

	public void Clear(string customerId)
	{
		_baskets.TryRemove(customerId, out _);
	}
}


