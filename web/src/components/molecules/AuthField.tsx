type AuthFieldProps = {
  label: string
  value: string
  type?: 'text' | 'email' | 'password'
  autoComplete: string
  error?: string
  onChange: (value: string) => void
}

export function AuthField({ label, value, type = 'text', autoComplete, error, onChange }: AuthFieldProps) {
  return (
    <label>
      {label}
      <input
        type={type}
        autoComplete={autoComplete}
        value={value}
        aria-invalid={error ? true : undefined}
        onChange={(event) => onChange(event.target.value)}
      />
      {error && <span role="alert" className="field-error">{error}</span>}
    </label>
  )
}
