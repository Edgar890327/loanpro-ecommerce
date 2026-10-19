import { useCallback, useEffect, useMemo, useState } from 'react'
import { apiRequest } from '../api/api'
import { AuthContext } from './auth-context'
import type { RegisterPayload, SessionUser } from './auth-context'

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<SessionUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    let active = true
    apiRequest<SessionUser>('/me')
      .then((profile) => {
        if (active) setUser(profile)
      })
      .catch(() => undefined)
      .finally(() => {
        if (active) setIsLoading(false)
      })
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    const handleUnauthorized = () => setUser(null)
    window.addEventListener('store:unauthorized', handleUnauthorized)
    return () => window.removeEventListener('store:unauthorized', handleUnauthorized)
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const profile = await apiRequest<SessionUser>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    setUser(profile)
    return profile
  }, [])

  const register = useCallback(async (payload: RegisterPayload) => {
    const profile = await apiRequest<SessionUser>('/auth/register', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
    setUser(profile)
    return profile
  }, [])

  const logout = useCallback(async () => {
    try {
      await apiRequest<void>('/auth/logout', { method: 'POST' })
    } finally {
      setUser(null)
    }
  }, [])

  const value = useMemo(
    () => ({ user, isLoading, login, register, logout }),
    [user, isLoading, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
