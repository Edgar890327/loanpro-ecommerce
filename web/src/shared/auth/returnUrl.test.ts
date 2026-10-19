import { describe, expect, it } from 'vitest'
import type { SessionUser } from './auth-context'
import { resolveReturnUrl } from './returnUrl'

const customer: SessionUser = { id: '1', fullName: 'Ana', email: 'user@example.com', role: 'Customer', preferredLocale: null }
const admin: SessionUser = { id: '2', fullName: 'Root', email: 'user@example.com', role: 'Admin', preferredLocale: null }

describe('resolveReturnUrl', () => {
  it('sends admins to the administration area', () => {
    expect(resolveReturnUrl(admin, '/checkout')).toBe('/admin/products')
  })

  it('returns customers to a local return url', () => {
    expect(resolveReturnUrl(customer, '/checkout')).toBe('/checkout')
  })

  it('falls back to the catalog for missing or external urls', () => {
    expect(resolveReturnUrl(customer, null)).toBe('/')
    expect(resolveReturnUrl(customer, '//evil.example')).toBe('/')
    expect(resolveReturnUrl(customer, 'https://evil.example')).toBe('/')
  })
})
