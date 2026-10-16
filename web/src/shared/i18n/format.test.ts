import { describe, expect, it } from 'vitest'
import { formatCurrency, formatDate, formatNumber } from './format'

describe('locale formatting', () => {
  it('formats USD using the active locale', () => {
    expect(formatCurrency(1234.5, 'en-US')).toBe('$1,234.50')
    expect(formatCurrency(12345.5, 'es')).toContain('12.345,50')
    expect(formatCurrency(12345.5, 'es')).toContain('US$')
  })

  it('formats numbers and dates using the active locale', () => {
    expect(formatNumber(1234.5, 'en-US')).toBe('1,234.5')
    expect(formatNumber(12345.5, 'es')).toBe('12.345,5')

    const date = new Date('2026-05-15T12:30:00.000Z')
    expect(formatDate(date, 'en-US')).not.toBe(formatDate(date, 'es'))
  })
})
