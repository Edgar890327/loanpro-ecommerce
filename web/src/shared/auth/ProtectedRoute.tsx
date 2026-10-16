import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './auth-context'

export function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { token } = useAuth()
  const location = useLocation()
  return token ? <>{children}</> : <Navigate to="/login" replace state={{ from: location }} />
}
