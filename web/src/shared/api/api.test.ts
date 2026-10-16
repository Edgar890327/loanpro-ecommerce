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

    await expect(apiRequest('/api/admin/orders', 'expired-token')).rejects.toBeInstanceOf(ApiError)

    expect(unauthorized).toHaveBeenCalledOnce()
    window.removeEventListener('store:unauthorized', unauthorized)
  })
})
