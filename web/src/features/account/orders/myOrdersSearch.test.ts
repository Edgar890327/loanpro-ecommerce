import { describe, expect, it } from 'vitest'
import { hasActiveFilters, readMyOrdersSearch, toMyOrdersApiQuery, writeMyOrdersSearch } from './myOrdersSearch'

describe('my orders search params', () => {
  it('reads defaults and ignores unknown sort values and invalid pages', () => {
    const search = readMyOrdersSearch(new URLSearchParams('sort=price&page=-3'))

    expect(search).toEqual({ q: '', status: '', from: '', to: '', sort: 'date_desc', page: 1 })
  })

  it('round-trips the active filters and omits defaults', () => {
    const search = { q: ' ORD ', status: 'Paid', from: '2026-01-01', to: '2026-02-01', sort: 'total_desc', page: 3 }

    const params = writeMyOrdersSearch(search)

    expect(params.toString()).toBe('q=ORD&status=Paid&from=2026-01-01&to=2026-02-01&sort=total_desc&page=3')
    expect(readMyOrdersSearch(params)).toEqual({ ...search, q: 'ORD' })
    expect(writeMyOrdersSearch(readMyOrdersSearch(new URLSearchParams())).toString()).toBe('')
  })

  it('builds the API query with ten rows per page', () => {
    const query = toMyOrdersApiQuery({ q: 'a b', status: '', from: '', to: '', sort: 'date_desc', page: 2 })

    expect(query).toBe('q=a+b&page=2&pageSize=10')
  })

  it('treats whitespace-only text as no filter', () => {
    expect(hasActiveFilters({ q: '   ', status: '', from: '', to: '', sort: 'date_desc', page: 1 })).toBe(false)
    expect(hasActiveFilters({ q: '', status: 'Paid', from: '', to: '', sort: 'date_desc', page: 1 })).toBe(true)
  })
})
