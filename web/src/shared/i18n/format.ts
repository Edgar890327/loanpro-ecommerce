export function formatCurrency(value: number, language: string) {
  return new Intl.NumberFormat(language, {
    style: 'currency',
    currency: 'USD',
  }).format(value)
}

export function formatNumber(value: number, language: string, maximumFractionDigits = 3) {
  return new Intl.NumberFormat(language, { maximumFractionDigits }).format(value)
}

export function formatDate(value: string | Date, language: string) {
  return new Intl.DateTimeFormat(language, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(typeof value === 'string' ? new Date(value) : value)
}
