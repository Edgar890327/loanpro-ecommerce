import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router-dom'
import { ApiError, apiRequest } from '../../../shared/api/api'
import { useAuth } from '../../../shared/auth/auth-context'

type LoginResponse = {
  accessToken: string
  expiresAtUtc: string
}

export function LoginPage() {
  const { t } = useTranslation()
  const { signIn } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    try {
      const result = await apiRequest<LoginResponse>('/auth/login', null, {
        method: 'POST',
        body: JSON.stringify({ password }),
      })
      signIn(result.accessToken)
      const destination = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname
      navigate(destination ?? '/admin/products', { replace: true })
    } catch (loginError) {
      setError(loginError instanceof ApiError && loginError.status === 401
        ? t('invalidCredentials')
        : t('requestFailed'))
    }
  }

  return (
    <section className="panel form-panel">
      <h1>{t('loginTitle')}</h1>
      <form onSubmit={(event) => void submit(event)} noValidate>
        <label>
          {t('password')}
          <input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
          />
        </label>
        {error && <p role="alert" className="form-error">{error}</p>}
        <button className="primary-button" type="submit">{t('signIn')}</button>
      </form>
    </section>
  )
}
