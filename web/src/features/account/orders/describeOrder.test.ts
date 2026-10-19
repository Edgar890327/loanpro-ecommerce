import { describe, expect, it } from 'vitest'
import { describeOrder } from './describeOrder'

const now = Date.parse('2026-05-15T12:00:00Z')

describe('describeOrder', () => {
  it('lets a reserved order with time left continue its payment', () => {
    const result = describeOrder('Reserved', '2026-05-15T12:10:00Z', now)

    expect(result).toEqual({ state: 'Reserved', canContinuePayment: true, secondsRemaining: 600 })
  })

  it('presents a reserved order past its deadline as expired without an action', () => {
    const result = describeOrder('Reserved', '2026-05-15T11:59:59Z', now)

    expect(result).toEqual({ state: 'Expired', canContinuePayment: false, secondsRemaining: 0 })
  })

  it.each(['Paid', 'Failed', 'Expired', 'Cancelled'] as const)('keeps the %s state without an action', (state) => {
    const result = describeOrder(state, '2026-05-15T12:10:00Z', now)

    expect(result).toEqual({ state, canContinuePayment: false, secondsRemaining: 0 })
  })
})
