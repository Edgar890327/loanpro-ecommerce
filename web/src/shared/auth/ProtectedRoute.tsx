import { Navigate, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from './auth-context'
import type { UserRole } from './auth-context'

type ProtectedRouteProps = {
  role: UserRole
  children: React.ReactNode
}

export function ProtectedRoute({ role, children }: ProtectedRouteProps) {
  const { user, isLoading } = useAuth()
  const location = useLocation()
  const { t } = useTranslation()

  if (isLoading) {
    return <p role="status">{t('loading')}</p>
  }
  if (!user) {
    const returnUrl = encodeURIComponent(`${location.pathname}${location.search}`)
    return <Navigate to={`/login?returnUrl=${returnUrl}`} replace />
  }
  if (user.role !== role) {
    return <p role="alert" className="form-error">{t('serverError_FORBIDDEN')}</p>
  }
  return <>{children}</>
}
