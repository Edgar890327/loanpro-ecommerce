import type { SessionUser } from './auth-context'

export function resolveReturnUrl(user: SessionUser, returnUrl: string | null) {
  if (user.role === 'Admin') return '/admin/products'
  if (returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//') && !returnUrl.startsWith('/\\')) {
    return returnUrl
  }
  return '/'
}
