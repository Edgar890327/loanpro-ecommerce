import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { LoadingSkeleton } from '../../../components/atoms/LoadingSkeleton'
import { apiRequest, translateApiError } from '../../../shared/api/api'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { useNow } from '../hooks/useNow'
import { MyOrderRow } from '../orders/MyOrderRow'
import {
  hasActiveFilters,
  myOrdersSorts,
  readMyOrdersSearch,
  toMyOrdersApiQuery,
  writeMyOrdersSearch,
} from '../orders/myOrdersSearch'
import { orderStates } from '../types/OrderState'
import type { MyOrdersPage as MyOrdersPageData } from '../types/MyOrdersPage'
import type { MyOrdersSearch } from '../types/MyOrdersSearch'

const searchDebounceMilliseconds = 300

const sortLabelKeys: Record<string, string> = {
  date_desc: 'sortDateDesc',
  date_asc: 'sortDateAsc',
  total_desc: 'sortTotalDesc',
}

export function MyOrdersPage() {
  const { t } = useTranslation()
  const [params, setParams] = useSearchParams()
  const search = readMyOrdersSearch(params)
  const [text, setText] = useState(search.q)
  const debouncedText = useDebouncedValue(text, searchDebounceMilliseconds)
  const now = useNow()
  const syncedText = useRef(search.q)

  const update = (changes: Partial<MyOrdersSearch>) => {
    const next = { ...readMyOrdersSearch(params), ...changes }
    setParams(writeMyOrdersSearch({ ...next, page: changes.page ?? 1 }), { replace: true })
  }

  useEffect(() => {
    if (debouncedText === syncedText.current) return
    syncedText.current = debouncedText
    setParams((previous) => {
      const current = readMyOrdersSearch(previous)
      if (debouncedText.trim() === current.q.trim()) return previous
      return writeMyOrdersSearch({ ...current, q: debouncedText, page: 1 })
    }, { replace: true })
  }, [debouncedText, setParams])

  const orders = useQuery({
    queryKey: ['my-orders', toMyOrdersApiQuery(search)],
    queryFn: () => apiRequest<MyOrdersPageData>(`/me/orders?${toMyOrdersApiQuery(search)}`),
    refetchOnWindowFocus: false,
  })

  const clearFilters = () => {
    setText('')
    setParams(new URLSearchParams(), { replace: true })
  }

  const filtered = hasActiveFilters(search)
  const pages = orders.data ? Math.max(1, Math.ceil(orders.data.totalCount / orders.data.pageSize)) : 1

  return (
    <section className="panel">
      <h1>{t('myOrders')}</h1>
      <form className="error-log-filters" role="search" onSubmit={(event) => event.preventDefault()}>
        <label>
          {t('myOrdersSearchLabel')}
          <input type="search" value={text} onChange={(event) => setText(event.target.value)} />
        </label>
        <label>
          {t('orderState')}
          <select value={search.status} onChange={(event) => update({ status: event.target.value })}>
            <option value="">{t('allStatuses')}</option>
            {orderStates.map((state) => <option key={state} value={state}>{t(`orderState${state}`)}</option>)}
          </select>
        </label>
        <label>
          {t('dateFrom')}
          <input type="date" value={search.from} onChange={(event) => update({ from: event.target.value })} />
        </label>
        <label>
          {t('dateTo')}
          <input type="date" value={search.to} onChange={(event) => update({ to: event.target.value })} />
        </label>
        <label>
          {t('sortBy')}
          <select value={search.sort} onChange={(event) => update({ sort: event.target.value })}>
            {myOrdersSorts.map((sort) => <option key={sort} value={sort}>{t(sortLabelKeys[sort])}</option>)}
          </select>
        </label>
        <button type="button" className="secondary-button" onClick={clearFilters}>{t('clearFilters')}</button>
      </form>
      {orders.isLoading && <LoadingSkeleton rows={5} />}
      {orders.isError && (
        <div role="alert">
          <p>{translateApiError(orders.error, t, 'myOrdersLoadFailed')}</p>
          <button type="button" onClick={() => void orders.refetch()}>{t('retry')}</button>
        </div>
      )}
      {orders.data && orders.data.items.length === 0 && !filtered && (
        <p role="status">{t('myOrdersEmpty')} <Link to="/">{t('browseCatalog')}</Link></p>
      )}
      {orders.data && orders.data.items.length === 0 && filtered && (
        <p role="status">
          {t('myOrdersNoResults')}
          {' '}
          <button type="button" className="text-button" onClick={clearFilters}>{t('clearFilters')}</button>
        </p>
      )}
      {orders.data && orders.data.items.length > 0 && (
        <>
          <table>
            <caption className="sr-only">{t('myOrders')}</caption>
            <thead>
              <tr>
                <th scope="col">{t('orderNumberColumn')}</th>
                <th scope="col">{t('orderDate')}</th>
                <th scope="col">{t('orderState')}</th>
                <th scope="col">{t('total')}</th>
                <th scope="col">{t('actions')}</th>
              </tr>
            </thead>
            <tbody>
              {orders.data.items.map((order) => <MyOrderRow key={order.id} order={order} now={now} />)}
            </tbody>
          </table>
          <nav className="pagination" aria-label={t('pagination')}>
            <button type="button" disabled={search.page <= 1} onClick={() => update({ page: search.page - 1 })}>{t('previous')}</button>
            <span>{t('page', { page: search.page, pages })}</span>
            <button type="button" disabled={search.page >= pages} onClick={() => update({ page: search.page + 1 })}>{t('next')}</button>
          </nav>
        </>
      )}
    </section>
  )
}
