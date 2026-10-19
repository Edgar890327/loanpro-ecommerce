import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiRequest, ApiError, translateApiError } from './api'

describe('API error handling', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('translates stable server error codes and preserves correlation references', () => {
    const error = new ApiError(
      409,
      'Product was modified.',
      'Conflict',
      'product_concurrency_conflict',
      'correlation-456',
    )
    const translate = vi.fn((key: string) =>
      key === 'serverError_product_concurrency_conflict'
        ? 'This product changed in another session.'
        : key,
    )

    expect(translateApiError(error, translate)).toBe(
      'This product changed in another session.',
    )
    expect(translate).toHaveBeenCalledWith(
      'serverError_product_concurrency_conflict',
      { correlationId: 'correlation-456' },
    )
  })

  it('dispatches the unauthorized event when the API returns 401', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(
      JSON.stringify({ title: 'Unauthorized' }),
      { status: 401, headers: { 'Content-Type': 'application/problem+json' } },
    )))
    const unauthorized = vi.fn()
    window.addEventListener('store:unauthorized', unauthorized)

    await expect(apiRequest('/api/admin/orders')).rejects.toBeInstanceOf(ApiError)

    expect(unauthorized).toHaveBeenCalledOnce()
    window.removeEventListener('store:unauthorized', unauthorized)
  })

  it('dispatches the forbidden event when the API returns 403', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 403 })))
    const forbidden = vi.fn()
    window.addEventListener('store:forbidden', forbidden)

    await expect(apiRequest('/api/admin/orders')).rejects.toBeInstanceOf(ApiError)

    expect(forbidden).toHaveBeenCalledOnce()
    window.removeEventListener('store:forbidden', forbidden)
  })

  it('adds the custom header only to methods that modify data', async () => {
    const fetchMock = vi.fn(async () => new Response('{}', {
      headers: { 'Content-Type': 'application/json' },
    }))
    vi.stubGlobal('fetch', fetchMock)

    await apiRequest('/api/catalog')
    await apiRequest('/api/admin/products', { method: 'POST', body: '{}' })
    await apiRequest('/api/admin/products/1', { method: 'DELETE' })

    const headersOf = (call: number) => new Headers(
      (fetchMock.mock.calls[call] as unknown as [string, RequestInit])[1].headers,
    )
    expect(headersOf(0).has('X-Requested-With')).toBe(false)
    expect(headersOf(1).get('X-Requested-With')).toBe('store')
    expect(headersOf(2).get('X-Requested-With')).toBe('store')
  })
})
