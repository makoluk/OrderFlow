import React, { useMemo, useState } from 'react';
import { apiGet } from '../api';

type OrderDetailsResponse = {
  id: string;
  status: string;
  createdAtUtc: string;
  paidAtUtc?: string | null;
  stockReservedAtUtc?: string | null;
  emailSentAtUtc?: string | null;
  completedAtUtc?: string | null;
  failReason?: string | null;
  failedAtUtc?: string | null;
  correlationId?: string | null;
  updatedAtUtc: string;
};

export default function OrderLookup() {
  const [orderId, setOrderId] = useState('');
  const [result, setResult] = useState<OrderDetailsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function fetchOrder() {
    setError(null);
    setResult(null);
    try {
      const data = await apiGet<OrderDetailsResponse>(`/orders/${encodeURIComponent(orderId)}`);
      setResult(data);
    } catch (e: any) {
      setError(e?.message ?? 'Failed to load order');
    }
  }

  const timeline = useMemo(() => {
    if (!result) return [] as Array<{ label: string; at?: string | null; done: boolean; note?: string }>;
    const steps: Array<{ label: string; at?: string | null; done: boolean; note?: string }> = [
      { label: 'Created', at: result.createdAtUtc, done: true },
      { label: 'Paid', at: result.paidAtUtc ?? null, done: !!result.paidAtUtc },
      { label: 'Stock Reserved', at: result.stockReservedAtUtc ?? null, done: !!result.stockReservedAtUtc },
      { label: 'Email Sent', at: result.emailSentAtUtc ?? null, done: !!result.emailSentAtUtc },
      { label: 'Completed', at: result.completedAtUtc ?? null, done: !!result.completedAtUtc }
    ];

    // Failure detail line(s)
    if (result.failedAtUtc) {
      // If failure reason indicates refund flow was triggered, show a dedicated step before Failed
      const reason = (result.failReason ?? '').toLowerCase();
      const refundTriggered =
        reason.includes('stock_failed') || reason.includes('email_failed') || reason.includes('payment timeout') || reason.includes('payment_timeout');
      if (refundTriggered) {
        steps.push({ label: 'Refund Requested', at: result.failedAtUtc, done: true });
      }
      steps.push({ label: `Failed (${result.failReason ?? 'unknown'})`, at: result.failedAtUtc, done: true });
    }
    return steps;
  }, [result]);

  return (
    <div style={{ display: 'grid', gap: 8 }}>
      <div style={{ display: 'flex', gap: 8 }}>
        <input value={orderId} onChange={e => setOrderId(e.target.value)} placeholder="orderId" style={{ flex: 1 }} />
        <button onClick={fetchOrder} disabled={!orderId}>Get Order</button>
      </div>
      {error && <div style={{ color: 'crimson' }}>{error}</div>}
      {result && (
        <div style={{ display: 'grid', gap: 8 }}>
          <div style={{ display: 'flex', gap: 12, alignItems: 'baseline' }}>
            <div><b>Status:</b> <code>{result.status}</code></div>
            {result.correlationId && <div style={{ color: '#777' }}>Trace: <code>{result.correlationId}</code></div>}
          </div>
          <ul style={{ margin: 0, paddingLeft: 18 }}>
            {timeline.map((s, i) => (
              <li key={i} style={{ color: s.done ? '#0a7' : '#999' }}>
                <span style={{ fontWeight: 600 }}>{s.label}</span>
                {s.at && <span> — {new Date(s.at).toLocaleString()}</span>}
                {s.note && <span style={{ color: '#c55' }}> — {s.note}</span>}
              </li>
            ))}
          </ul>

          {!result.completedAtUtc && !result.failedAtUtc && result.paidAtUtc && result.stockReservedAtUtc && result.emailSentAtUtc && (
            <div style={{ color: '#c55' }}>
              All steps done but not marked Completed yet. Likely missed OrderCompleted event. Try re-running checkout now that Orchestrator is up.
            </div>
          )}

          <details>
            <summary>Show raw</summary>
            <pre style={{ background: '#111', color: '#eee', padding: 12, borderRadius: 6, overflow: 'auto' }}>
{JSON.stringify(result, null, 2)}
            </pre>
          </details>
        </div>
      )}
    </div>
  );
}
