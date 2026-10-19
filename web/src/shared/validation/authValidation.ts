export type AuthFormMessages = {
  emailInvalid: string
  fullNameRequired: string
  passwordLength: string
  passwordRequired: string
}

export type AuthFormErrors = Partial<Record<'email' | 'fullName' | 'password', string>>

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function validateLoginForm(
  values: { email: string, password: string },
  messages: AuthFormMessages,
): AuthFormErrors {
  const errors: AuthFormErrors = {}
  if (!emailPattern.test(values.email.trim())) errors.email = messages.emailInvalid
  if (!values.password) errors.password = messages.passwordRequired
  return errors
}

export function validateRegisterForm(
  values: { email: string, fullName: string, password: string },
  messages: AuthFormMessages,
): AuthFormErrors {
  const errors: AuthFormErrors = {}
  if (!emailPattern.test(values.email.trim())) errors.email = messages.emailInvalid
  if (!values.fullName.trim()) errors.fullName = messages.fullNameRequired
  if (values.password.length < 8 || values.password.length > 128) errors.password = messages.passwordLength
  return errors
}
