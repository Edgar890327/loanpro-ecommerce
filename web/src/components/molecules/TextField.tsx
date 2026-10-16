type TextFieldProps = {
  label: string
  value: string
  error?: string
  maxLength?: number
  multiline?: boolean
  onChange: (value: string) => void
}

export function TextField({ label, value, error, maxLength, multiline = false, onChange }: TextFieldProps) {
  return (
    <label>
      {label}
      {multiline
        ? <textarea value={value} maxLength={2000} onChange={(event) => onChange(event.target.value)} />
        : <input value={value} maxLength={maxLength} onChange={(event) => onChange(event.target.value)} />}
      {error && <span role="alert" className="field-error">{error}</span>}
    </label>
  )
}
