import { useState } from 'react'
import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { translateApiError } from '../../../shared/api/api'
import { useAuth } from '../../../shared/auth/auth-context'
import { resolveReturnUrl } from '../../../shared/auth/returnUrl'
import { validateRegisterForm } from '../../../shared/validation/authValidation'
import type { AuthFormErrors } from '../../../shared/validation/authValidation'
import { AuthField } from '../../../components/molecules/AuthField'

export function RegisterPage() {
  const { t, i18n } = useTranslation()
  const { register } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [email, setEmail] = useState('')
  const [fullName, setFullName] = useState('')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<AuthFormErrors>({})
  const [serverError, setServerError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const returnUrl = params.get('returnUrl')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setServerError('')
    const validation = validateRegisterForm({ email, fullName, password }, {
      emailInvalid: t('emailInvalid'),
      fullNameRequired: t('fullNameRequired'),
      passwordLength: t('passwordLength'),
      passwordRequired: t('passwordRequired'),
    })
    setErrors(validation)
    if (Object.keys(validation).length > 0) return
    setIsSubmitting(true)
    try {
      const user = await register({
        email: email.trim(),
        fullName: fullName.trim(),
        password,
        preferredLocale: i18n.language,
      })
      navigate(resolveReturnUrl(user, returnUrl), { replace: true })
    } catch (error) {
      setServerError(translateApiError(error, t))
    } finally {
      setIsSubmitting(false)
    }
  }

  const loginLink = returnUrl ? `/login?returnUrl=${encodeURIComponent(returnUrl)}` : '/login'

  return (
    <section className="panel form-panel">
      <h1>{t('registerTitle')}</h1>
      <form onSubmit={(event) => void submit(event)} noValidate>
        <AuthField
          label={t('fullName')}
          autoComplete="name"
          value={fullName}
          error={errors.fullName}
          onChange={setFullName}
        />
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
          autoComplete="new-password"
          value={password}
          error={errors.password}
          onChange={setPassword}
        />
        {serverError && <p role="alert" className="form-error">{serverError}</p>}
        <button className="primary-button" type="submit" disabled={isSubmitting}>{t('register')}</button>
      </form>
      <p>{t('haveAccount')} <Link to={loginLink}>{t('login')}</Link></p>
    </section>
  )
}
