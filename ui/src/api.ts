export const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000/api';

export function getCustomerId(): string {
  const key = 'customerId';
  let v = localStorage.getItem(key);
  if (!v) {
    v = 'c1';
    localStorage.setItem(key, v);
  }
  return v;
}

export function genIdempotencyKey(prefix = 'idem'): string {
  return `${prefix}-${crypto.randomUUID?.() ?? Math.random().toString(36).slice(2)}`;
}

export async function apiGet<T>(path: string): Promise<T> {
  const resp = await fetch(`${API_BASE}${path}`, {
    headers: {
      'X-Customer-Id': getCustomerId()
    }
  });
  if (!resp.ok) throw new Error(await resp.text());
  const json = await resp.json();
  return json.data ?? json;
}

export async function apiPost<T>(path: string, body?: unknown, extraHeaders: Record<string, string> = {}): Promise<T> {
  const resp = await fetch(`${API_BASE}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Customer-Id': getCustomerId(),
      ...extraHeaders
    },
    body: body ? JSON.stringify(body) : undefined
  });
  if (!resp.ok) throw new Error(await resp.text());
  const json = await resp.json();
  return json.data ?? json;
}

export type BasketItem = {
  productId: string;
  name: string;
  quantity: number;
  unitPrice: number;
  currency: string;
};

export type BasketDetails = {
  customerId: string;
  items: BasketItem[];
  total: number;
};

export function ensureBasket(details: Partial<BasketDetails> | null | undefined): BasketDetails {
  return {
    customerId: details?.customerId ?? 'anonymous',
    items: Array.isArray(details?.items) ? (details!.items as BasketItem[]) : [],
    total: typeof details?.total === 'number' ? (details!.total as number) : 0
  };
}

export type CreateOrderResponse = { orderId: string };
