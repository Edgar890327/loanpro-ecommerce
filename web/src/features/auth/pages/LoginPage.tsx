import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { translateApiError } from '../../../shared/api/api'
import { useAuth } from '../../../shared/auth/auth-context'
import { resolveReturnUrl } from '../../../shared/auth/returnUrl'
import { validateLoginForm } from '../../../shared/validation/authValidation'
import type { AuthFormErrors } from '../../../shared/validation/authValidation'
import { AuthField } from '../../../components/molecules/AuthField'

export function LoginPage() {
  const { t } = useTranslation()
  const { login } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<AuthFormErrors>({})
  const [serverError, setServerError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const returnUrl = params.get('returnUrl')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setServerError('')
    const validation = validateLoginForm({ email, password }, {
      emailInvalid: t('emailInvalid'),
      fullNameRequired: t('fullNameRequired'),
      passwordLength: t('passwordLength'),
      passwordRequired: t('passwordRequired'),
    })
    setErrors(validation)
    if (Object.keys(validation).length > 0) return
    setIsSubmitting(true)
    try {
      const user = await login(email.trim(), password)
      navigate(resolveReturnUrl(user, returnUrl), { replace: true })
    } catch (error) {
      setServerError(translateApiError(error, t))
    } finally {
      setIsSubmitting(false)
    }
  }

  const registerLink = returnUrl ? `/register?returnUrl=${encodeURIComponent(returnUrl)}` : '/register'

  return (
    <section className="panel form-panel">
      <h1>{t('loginTitle')}</h1>
      <form onSubmit={(event) => void submit(event)} noValidate>
        <AuthField
          label={t('email')}
          type="email"
          autoComplete="email"
          value={email}
          error={errors.email}
          onChange={setEmail}
        />
        <AuthField
          label={t('password')}
          type="password"
          autoComplete="current-password"
          value={password}
          error={errors.password}
          onChange={setPassword}
        />
        {serverError && <p role="alert" className="form-error">{serverError}</p>}
        <button className="primary-button" type="submit" disabled={isSubmitting}>{t('signIn')}</button>
      </form>
      <p>{t('noAccount')} <Link to={registerLink}>{t('register')}</Link></p>
    </section>
  )
}
