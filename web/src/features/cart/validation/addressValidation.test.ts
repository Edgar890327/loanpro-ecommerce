import { describe, expect, it } from 'vitest'
import { validateShippingAddress } from './addressValidation'

const messages = { required: 'required', tooLong: 'too long' }
const valid = {
  recipientName: 'Buyer',
  line1: '1 Test Street',
  line2: '',
  city: 'Austin',
  region: 'TX',
  postalCode: '73301',
  country: 'US',
}

describe('validateShippingAddress', () => {
  it('accepts a complete address without a second line', () => {
    expect(validateShippingAddress(valid, messages)).toEqual({})
  })

  it('requires every field except the second line', () => {
    const errors = validateShippingAddress({ ...valid, recipientName: ' ', line1: '', city: '', region: '', postalCode: '', country: '' }, messages)
    expect(Object.keys(errors).sort()).toEqual(['city', 'country', 'line1', 'postalCode', 'recipientName', 'region'])
  })

  it('rejects values over the server limits', () => {
    expect(validateShippingAddress({ ...valid, postalCode: '1'.repeat(21), line2: 'x'.repeat(201) }, messages))
      .toEqual({ postalCode: 'too long', line2: 'too long' })
  })
})
