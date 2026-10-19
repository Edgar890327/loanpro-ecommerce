import { createContext, useContext } from 'react'

export type UserRole = 'Admin' | 'Customer'

export type SessionUser = {
  id: string
  fullName: string
  role: UserRole
  preferredLocale: string | null
}

export type RegisterPayload = {
  email: string
  fullName: string
  password: string
  preferredLocale?: string
}

export type AuthContextValue = {
  user: SessionUser | null
  isLoading: boolean
  login: (email: string, password: string) => Promise<SessionUser>
  register: (payload: RegisterPayload) => Promise<SessionUser>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider.')
  }

  return context
}
