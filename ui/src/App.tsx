import React from 'react';
import AddItemForm from './components/AddItemForm';
import BasketList from './components/BasketList';
import CheckoutButton from './components/CheckoutButton';
import OrderLookup from './components/OrderLookup';
import { API_BASE, getCustomerId } from './api';

export default function App() {
  const [lastOrderId, setLastOrderId] = React.useState<string | null>(null);
  const [refreshToken, setRefreshToken] = React.useState(0);

  return (
    <div style={{ maxWidth: 900, margin: '32px auto', padding: 16, fontFamily: 'system-ui, Arial, sans-serif' }}>
      <h2 style={{ marginTop: 0 }}>OrderFlow UI</h2>
      <div style={{ marginBottom: 12, color: '#666' }}>
        <div>Gateway: <code>{API_BASE}</code></div>
        <div>Customer: <code>{getCustomerId()}</code></div>
      </div>

      <section style={{ display: 'grid', gap: 16 }}>
        <AddItemForm onAdded={() => setRefreshToken(t => t + 1)} />
        <BasketList refreshToken={refreshToken} />
        <CheckoutButton onCompleted={(id) => setLastOrderId(id)} />
      </section>

      <hr style={{ margin: '24px 0' }} />

      <section style={{ display: 'grid', gap: 12 }}>
        <h3 style={{ margin: 0 }}>Order Lookup</h3>
        {lastOrderId && <div>Last created orderId: <code>{lastOrderId}</code></div>}
        <OrderLookup />
      </section>
    </div>
  );
}
