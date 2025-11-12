import React, { useState } from 'react';
import { apiPost, genIdempotencyKey } from '../api';

export default function CheckoutButton({ onCompleted }: { onCompleted: (orderId: string) => void }) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function checkout() {
    setLoading(true);
    setError(null);
    try {
      const idem = genIdempotencyKey('checkout');
      const data = await apiPost<{ orderId: string }>(
        '/orders/checkout',
        undefined,
        { 'Idempotency-Key': idem }
      );
      onCompleted(data.orderId);
    } catch (e: any) {
      setError(e?.message ?? 'Checkout failed');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
      <button onClick={checkout} disabled={loading}>{loading ? 'Processing…' : 'Checkout'}</button>
      {error && <span style={{ color: 'crimson' }}>{error}</span>}
    </div>
  );
}
