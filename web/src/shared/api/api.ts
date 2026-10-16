export class ApiError extends Error {
  public readonly status: number

  public readonly detail?: string

  constructor(
    status: number,
    detail?: string,
  ) {
    super(detail ?? `Request failed with status ${status}.`)
    this.status = status
    this.detail = detail
  }
}

export async function apiRequest<T>(
  path: string,
  token?: string | null,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(path, { ...init, headers })
  if (response.status === 401) {
    window.dispatchEvent(new Event('store:unauthorized'))
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string; title?: string } | null
    throw new ApiError(response.status, problem?.detail ?? problem?.title)
  }
  if (response.status === 204) {
    return undefined as T
  }
  return response.json() as Promise<T>
}
