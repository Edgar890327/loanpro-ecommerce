import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { LoadingSkeleton } from '../../../components/atoms/LoadingSkeleton'
import { CountdownLabel } from '../../../components/molecules/CountdownLabel'
import { apiRequest, translateApiError } from '../../../shared/api/api'
import { formatCurrency, formatDate, formatNumber } from '../../../shared/i18n/format'
import { useNow } from '../hooks/useNow'
import { describeOrder } from '../orders/describeOrder'
import type { MyOrderDetail } from '../types/MyOrderDetail'

export function MyOrderDetailPage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams()
  const now = useNow()
  const order = useQuery({
    queryKey: ['my-order', id],
    queryFn: () => apiRequest<MyOrderDetail>(`/me/orders/${id}`),
    enabled: Boolean(id),
    retry: false,
  })

  if (order.isLoading) return <LoadingSkeleton rows={6} />
  if (order.isError || !order.data) {
    return (
      <section className="panel">
        <p role="alert">{translateApiError(order.error, t, 'myOrderLoadFailed')}</p>
        <Link to="/account/orders">{t('backToMyOrders')}</Link>
      </section>
    )
  }

  const data = order.data
  const presentation = describeOrder(data.state, data.reservedUntilUtc, now)
  const address = data.shippingAddress
  return (
    <section className="panel order-status">
      <p className="eyebrow">{t('purchaseDetail')}</p>
      <h1>{data.orderNumber}</h1>
      <p>{t(`orderState${presentation.state}`)} · {formatDate(data.createdAtUtc, i18n.language)}</p>
      {presentation.canContinuePayment && (
        <p>
          <Link className="secondary-button" to={`/orders/${data.id}`}>{t('continuePayment')}</Link>
          {' '}
          <CountdownLabel seconds={presentation.secondsRemaining} />
        </p>
      )}
      <table>
        <caption className="sr-only">{t('orderItems')}</caption>
        <thead>
          <tr>
            <th scope="col">{t('name')}</th>
            <th scope="col">{t('sku')}</th>
            <th scope="col">{t('quantity')}</th>
            <th scope="col">{t('pricePaid')}</th>
            <th scope="col">{t('subtotal')}</th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((item) => (
            <tr key={item.sku}>
              <th scope="row">{item.productName}</th>
              <td>{item.sku}</td>
              <td>{formatNumber(item.quantity, i18n.language, 0)}</td>
              <td>{formatCurrency(item.unitPrice, i18n.language)}</td>
              <td>{formatCurrency(item.subtotal, i18n.language)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <dl className="order-totals">
        <div><dt>{t('subtotal')}</dt><dd>{formatCurrency(data.subtotal, i18n.language)}</dd></div>
        <div><dt>{t('shipping')}</dt><dd>{formatCurrency(data.shipping, i18n.language)}</dd></div>
        <div><dt>{t('total')}</dt><dd>{formatCurrency(data.total, i18n.language)}</dd></div>
        {data.paymentProviderKey && <div><dt>{t('paymentProvider')}</dt><dd>{data.paymentProviderKey}</dd></div>}
        {data.paymentReference && <div><dt>{t('paymentReference')}</dt><dd>{data.paymentReference}</dd></div>}
      </dl>
      <h2>{t('shippingAddress')}</h2>
      <address>
        {address.recipientName}<br />
        {address.line1}<br />
        {address.line2 && <>{address.line2}<br /></>}
        {address.city}, {address.region} {address.postalCode}<br />
        {address.country}
      </address>
      <div className="button-row">
        <Link to="/account/orders" className="secondary-button">{t('backToMyOrders')}</Link>
      </div>
    </section>
  )
}
