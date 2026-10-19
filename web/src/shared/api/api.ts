export class ApiError extends Error {
  public readonly status: number

  public readonly detail?: string

  public readonly title?: string

  public readonly code?: string

  public readonly correlationId?: string

  constructor(
    status: number,
    detail?: string,
    title?: string,
    code?: string,
    correlationId?: string,
  ) {
    super(detail ?? `Request failed with status ${status}.`)
    this.status = status
    this.detail = detail
    this.title = title
    this.code = code
    this.correlationId = correlationId
  }
}

export const csrfHeaderName = 'X-Requested-With'
export const csrfHeaderValue = 'store'

const mutatingMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE'])

export async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  if (mutatingMethods.has((init.method ?? 'GET').toUpperCase())) {
    headers.set(csrfHeaderName, csrfHeaderValue)
  }

  const response = await fetch(path, { ...init, headers, credentials: 'same-origin' })
  if (response.status === 401) {
    window.dispatchEvent(new Event('store:unauthorized'))
  }
  if (response.status === 403) {
    window.dispatchEvent(new Event('store:forbidden'))
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as {
      detail?: string
      title?: string
      code?: string
      correlationId?: string
    } | null
    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title,
      problem?.title,
      problem?.code,
      problem?.correlationId,
    )
  }

  if (response.status === 204) {
    return undefined as T
  }
  return response.json() as Promise<T>
}

export function translateApiError(
  error: unknown,
  translate: (key: string, options?: Record<string, unknown>) => string,
  fallbackKey = 'requestFailed',
) {
  if (error instanceof ApiError && error.code) {
    const translationKey = `serverError_${error.code}`
    const translated = translate(translationKey, { correlationId: error.correlationId ?? '' })
    if (translated !== translationKey) return translated
  }
  return translate(fallbackKey)
}
