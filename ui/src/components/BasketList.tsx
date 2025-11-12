import React from 'react';
import { apiGet, BasketDetails, ensureBasket } from '../api';

export default function BasketList({ refreshToken = 0 }: { refreshToken?: number }) {
  const [basket, setBasket] = React.useState<BasketDetails | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  async function load() {
    setError(null);
    try {
      const data = await apiGet<BasketDetails>('/basket');
      setBasket(ensureBasket(data));
    } catch (e: any) {
      setError(e?.message ?? 'Failed to load basket');
    }
  }

  React.useEffect(() => { load(); }, []);
  React.useEffect(() => { load(); }, [refreshToken]);

  if (error) return <div style={{ color: 'crimson' }}>{error}</div>;

  const items = basket?.items ?? [];
  const currency = items[0]?.currency ?? 'TRY';

  return (
    <div>
      <div style={{ display: 'flex', gap: 8, alignItems: 'baseline' }}>
        <h3 style={{ margin: 0 }}>Basket</h3>
        <button onClick={load}>Refresh</button>
      </div>
      {items.length === 0 ? (
        <p>No items.</p>
      ) : (
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th align="left">Product</th>
              <th align="right">Qty</th>
              <th align="right">Unit Price</th>
              <th align="right">Subtotal</th>
            </tr>
          </thead>
          <tbody>
            {items.map((i, idx) => (
              <tr key={idx}>
                <td>{i.productId}</td>
                <td align="right">{i.quantity}</td>
                <td align="right">{i.unitPrice.toFixed(2)} {i.currency}</td>
                <td align="right">{(i.quantity * i.unitPrice).toFixed(2)} {i.currency}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={3} align="right"><b>Total</b></td>
              <td align="right"><b>{(basket?.total ?? 0).toFixed(2)} {currency}</b></td>
            </tr>
          </tfoot>
        </table>
      )}
    </div>
  );
}
