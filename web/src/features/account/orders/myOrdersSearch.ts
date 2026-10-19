import type { MyOrdersSearch } from '../types/MyOrdersSearch'

export const myOrdersPageSize = 10
export const defaultMyOrdersSort = 'date_desc'
export const myOrdersSorts = ['date_desc', 'date_asc', 'total_desc'] as const

export function readMyOrdersSearch(params: URLSearchParams): MyOrdersSearch {
  const sort = params.get('sort') ?? ''
  const page = Number.parseInt(params.get('page') ?? '1', 10)
  return {
    q: params.get('q') ?? '',
    status: params.get('status') ?? '',
    from: params.get('from') ?? '',
    to: params.get('to') ?? '',
    sort: (myOrdersSorts as readonly string[]).includes(sort) ? sort : defaultMyOrdersSort,
    page: Number.isFinite(page) && page > 0 ? page : 1,
  }
}

export function writeMyOrdersSearch(search: MyOrdersSearch): URLSearchParams {
  const params = new URLSearchParams()
  if (search.q.trim()) params.set('q', search.q.trim())
  if (search.status) params.set('status', search.status)
  if (search.from) params.set('from', search.from)
  if (search.to) params.set('to', search.to)
  if (search.sort !== defaultMyOrdersSort) params.set('sort', search.sort)
  if (search.page > 1) params.set('page', String(search.page))
  return params
}

export function toMyOrdersApiQuery(search: MyOrdersSearch): string {
  const params = writeMyOrdersSearch(search)
  params.set('page', String(search.page))
  params.set('pageSize', String(myOrdersPageSize))
  return params.toString()
}

export function hasActiveFilters(search: MyOrdersSearch): boolean {
  return Boolean(search.q.trim() || search.status || search.from || search.to)
}
