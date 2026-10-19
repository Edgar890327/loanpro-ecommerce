import { useCallback, useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { apiRequest, translateApiError } from '../../../shared/api/api'
import { PaymentCheckout } from '../payments/PaymentCheckout'
import type { PaymentProvider } from '../payments/paymentTypes'
import type { Order } from '../types/Order'
import { formatCurrency, formatNumber } from '../../../shared/i18n/format'
import { LoadingSkeleton } from '../../../components/atoms/LoadingSkeleton'

export function OrderStatusPage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams()
  const queryClient = useQueryClient()
  const [now, setNow] = useState(Date.now())
  const order = useQuery({
    queryKey: ['order', id],
    queryFn: () => apiRequest<Order>(`/api/orders/${id}`),
    enabled: Boolean(id),
    refetchInterval: 5000,
  })
  const provider = useQuery({
    queryKey: ['active-payment-provider'],
    queryFn: () => apiRequest<PaymentProvider>('/api/payments/provider'),
    enabled: order.data?.state === 'Reserved',
    retry: false,
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

  const refreshOrder = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: ['order', id] })
  }, [id, queryClient])

  if (order.isLoading) return <LoadingSkeleton rows={5} />
  if (order.isError || !order.data) {
    return <section className="panel"><p role="alert">{translateApiError(order.error, t, 'orderLoadFailed')}</p><Link to="/">{t('backToCatalog')}</Link></section>
  }

  const remaining = Math.max(0, Math.ceil((Date.parse(order.data.reservedUntilUtc) - now) / 1000))
  const canCancel = order.data.state === 'Reserved' && Boolean(accessKey)
  return (
    <section className="panel order-status">
      <p className="eyebrow">{t('orderStatus')}</p>
      <h1>{t(`orderState${order.data.state}`)}</h1>
      <p>{t('orderReference', { id: order.data.id })}</p>
      {order.data.state === 'Reserved' && <p role="timer">{t('reservationCountdown', { minutes: formatNumber(Math.floor(remaining / 60), i18n.language, 0), seconds: String(remaining % 60).padStart(2, '0') })}</p>}
      {order.data.state === 'Reserved' && remaining === 0 && <p role="status">{t('reservationFinishing')}</p>}
      {order.data.state === 'Reserved' && accessKey && (
        <section aria-label={t('paymentSection')}>
          <h2>{t('paymentSection')}</h2>
          {provider.isLoading && <p role="status">{t('paymentLoading')}</p>}
          {provider.isError && <p role="alert">{translateApiError(provider.error, t, 'paymentProviderUnavailable')}</p>}
          {provider.data && <PaymentCheckout
            orderId={order.data.id}
            capability={accessKey}
            provider={provider.data}
            onCaptured={refreshOrder}
          />}
        </section>
      )}
      <ul>
        {order.data.items.map((item) => <li key={item.productId}>{item.name} × {formatNumber(item.quantity, i18n.language, 0)} — {formatCurrency(item.subtotal, i18n.language)}</li>)}
      </ul>
      <dl className="order-totals">
        <div><dt>{t('subtotal')}</dt><dd>{formatCurrency(order.data.subtotal, i18n.language)}</dd></div>
        <div><dt>{t('shipping')}</dt><dd>{formatCurrency(order.data.shipping, i18n.language)}</dd></div>
        <div><dt>{t('total')}</dt><dd>{formatCurrency(order.data.total, i18n.language)}</dd></div>
      </dl>
      {cancel.isError && <p role="alert">{translateApiError(cancel.error, t, 'cancelOrderFailed')}</p>}
      <div className="button-row">
        {canCancel && <button type="button" className="danger-button" onClick={() => cancel.mutate()} disabled={cancel.isPending}>{t('cancelOrder')}</button>}
        <Link to="/" className="secondary-button">{t('backToCatalog')}</Link>
      </div>
    </section>
  )
}
