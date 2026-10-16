import { useCallback, useEffect, useMemo, useState } from 'react'
import { AuthContext } from './auth-context'

const storageKey = 'store-admin-token'

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState(() => sessionStorage.getItem(storageKey))

  const signOut = useCallback(() => {
    sessionStorage.removeItem(storageKey)
    setToken(null)
  }, [])

  useEffect(() => {
    const handleUnauthorized = () => signOut()
    window.addEventListener('store:unauthorized', handleUnauthorized)
    return () => window.removeEventListener('store:unauthorized', handleUnauthorized)
  }, [signOut])

  const value = useMemo(
    () => ({
      token,
      signIn: (newToken: string) => {
        sessionStorage.setItem(storageKey, newToken)
        setToken(newToken)
      },
      signOut,
    }),
    [signOut, token],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
