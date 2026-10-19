import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiRequest, translateApiError } from '../../../shared/api/api'
import { formatCurrency, formatDate } from '../../../shared/i18n/format'
import { LoadingSkeleton } from '../../../components/atoms/LoadingSkeleton'
import type { OrderHistoryPageData } from '../types/OrderHistory'

const pageSize = 20

export function OrderHistoryPage() {
  const { t, i18n } = useTranslation()
  const [page, setPage] = useState(1)
  const orders = useQuery({
    queryKey: ['admin-order-history', page],
    queryFn: () => apiRequest<OrderHistoryPageData>(
      `/api/admin/orders?page=${page}&pageSize=${pageSize}`, 
    ),
  })
  const pages = Math.max(1, Math.ceil((orders.data?.totalCount ?? 0) / pageSize))

  return (
    <section className="panel">
      <h1>{t('orderHistory')}</h1>
      {orders.isLoading && <LoadingSkeleton rows={5} />}
      {orders.isError && <p role="alert">{translateApiError(orders.error, t, 'orderHistoryLoadFailed')}</p>}
      {orders.data?.items.length === 0 && <p role="status">{t('emptyOrders')}</p>}
      {orders.data && orders.data.items.length > 0 && (
        <>
          <div className="table-scroll">
            <table>
              <caption className="sr-only">{t('orderHistory')}</caption>
              <thead>
                <tr>
                  <th scope="col">{t('orderReferenceHeader')}</th>
                  <th scope="col">{t('customer')}</th>
                  <th scope="col">{t('orderDate')}</th>
                  <th scope="col">{t('orderState')}</th>
                  <th scope="col">{t('paymentProvider')}</th>
                  <th scope="col">{t('total')}</th>
                </tr>
              </thead>
              <tbody>
                {orders.data.items.map((order) => (
                  <tr key={order.id}>
                    <td>{order.orderNumber}</td>
                    <td>{order.customerName}<br /><small>{order.customerEmail}</small></td>
                    <td>{formatDate(order.createdAtUtc, i18n.language)}</td>
                    <td>{t(`orderState${order.state}`, { defaultValue: order.state })}</td>
                    <td>{order.paymentProviderKey
                      ? t(`settingName_${order.paymentProviderKey}`, { defaultValue: order.paymentProviderKey })
                      : t('notPaid')}</td>
                    <td>{formatCurrency(order.total, i18n.language)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <nav className="pagination" aria-label={t('pagination')}>
            <button type="button" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>
              {t('previous')}
            </button>
            <span aria-current="page">{t('page', { page, pages })}</span>
            <button type="button" disabled={page >= pages} onClick={() => setPage((value) => value + 1)}>
              {t('next')}
            </button>
          </nav>
        </>
      )}
    </section>
  )
}
