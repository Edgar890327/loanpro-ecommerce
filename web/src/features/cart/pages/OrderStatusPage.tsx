import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { apiRequest } from '../../../shared/api/api'
import type { Order } from '../types/Order'

export function OrderStatusPage() {
  const { t } = useTranslation()
  const { id } = useParams()
  const queryClient = useQueryClient()
  const [now, setNow] = useState(Date.now())
  const order = useQuery({
    queryKey: ['order', id],
    queryFn: () => apiRequest<Order>(`/api/orders/${id}`),
    enabled: Boolean(id),
    refetchInterval: 5000,
  })
  const accessKey = id ? window.localStorage.getItem(`store:order-access:${id}`) : null
  const cancel = useMutation({
    mutationFn: () => apiRequest<Order>(`/api/orders/${id}/cancel`, null, {
      method: 'POST',
      headers: { 'Idempotency-Key': accessKey ?? '' },
    }),
    onSuccess: (result) => queryClient.setQueryData(['order', id], result),
  })

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])

  if (order.isLoading) return <p role="status">{t('loading')}</p>
  if (order.isError || !order.data) return <section className="panel"><p role="alert">{t('orderLoadFailed')}</p><Link to="/">{t('backToCatalog')}</Link></section>

  const remaining = Math.max(0, Math.ceil((Date.parse(order.data.reservedUntilUtc) - now) / 1000))
  const canCancel = order.data.state === 'Reserved' && Boolean(accessKey)
  return (
    <section className="panel order-status">
      <p className="eyebrow">{t('orderStatus')}</p>
      <h1>{t(`orderState${order.data.state}`)}</h1>
      <p>{t('orderReference', { id: order.data.id })}</p>
      {order.data.state === 'Reserved' && <p role="timer">{t('reservationCountdown', { minutes: Math.floor(remaining / 60), seconds: String(remaining % 60).padStart(2, '0') })}</p>}
      {order.data.state === 'Reserved' && remaining === 0 && <p role="status">{t('reservationFinishing')}</p>}
      <ul>
        {order.data.items.map((item) => <li key={item.productId}>{item.name} × {item.quantity} — ${item.subtotal.toFixed(2)}</li>)}
      </ul>
      <dl className="order-totals">
        <div><dt>{t('subtotal')}</dt><dd>${order.data.subtotal.toFixed(2)}</dd></div>
        <div><dt>{t('shipping')}</dt><dd>${order.data.shipping.toFixed(2)}</dd></div>
        <div><dt>{t('total')}</dt><dd>${order.data.total.toFixed(2)}</dd></div>
      </dl>
      {canCancel && <button type="button" onClick={() => cancel.mutate()} disabled={cancel.isPending}>{t('cancelOrder')}</button>}
      {cancel.isError && <p role="alert">{t('cancelOrderFailed')}</p>}
      <Link to="/">{t('backToCatalog')}</Link>
    </section>
  )
}
