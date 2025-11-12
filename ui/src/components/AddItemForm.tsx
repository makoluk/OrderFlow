import React, { useState } from 'react';
import { apiPost } from '../api';

export default function AddItemForm({ onAdded }: { onAdded: () => void }) {
  const [productId, setProductId] = useState('p-100');
  const [name, setName] = useState('Product 100');
  const [quantity, setQuantity] = useState(1);
  const [unitPrice, setUnitPrice] = useState(50);
  const [currency, setCurrency] = useState('TRY');
  const [loading, setLoading] = useState(false);

  async function addItem(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      await apiPost('/basket/items', { productId, name, quantity, unitPrice, currency });
      onAdded();
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={addItem} style={{ display: 'grid', gap: 8, gridTemplateColumns: 'repeat(6, 1fr)', alignItems: 'center' }}>
      <input value={productId} onChange={e => setProductId(e.target.value)} placeholder="productId" />
      <input value={name} onChange={e => setName(e.target.value)} placeholder="name" />
      <input type="number" value={quantity} onChange={e => setQuantity(parseInt(e.target.value || '0'))} min={1} />
      <input type="number" step="0.01" value={unitPrice} onChange={e => setUnitPrice(parseFloat(e.target.value || '0'))} />
      <input value={currency} onChange={e => setCurrency(e.target.value)} />
      <button type="submit" disabled={loading}>{loading ? 'Adding...' : 'Add item'}</button>
    </form>
  );
}
