type NumberFieldProps = {
  label: string
  value: number | ''
  error?: string
  integer?: boolean
  nullable?: boolean
  precision?: number
  onChange: (value: number | '') => void
}

export function NumberField({
  label,
  value,
  error,
  integer = false,
  nullable = false,
  precision = 2,
  onChange,
}: NumberFieldProps) {
  return (
    <label>
      {label}
      <input
        type="number"
        min="0"
        step={integer ? '1' : precision === 3 ? '0.001' : '0.01'}
        value={value}
        onChange={(event) => {
          if (nullable && event.target.value === '') onChange('')
          else onChange(event.target.value === '' ? Number.NaN : Number(event.target.value))
        }}
      />
      {error && <span role="alert" className="field-error">{error}</span>}
    </label>
  )
}
