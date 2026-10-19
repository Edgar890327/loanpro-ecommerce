import type { OrderState } from '../types/OrderState'

export type OrderPresentation = {
  state: OrderState
  canContinuePayment: boolean
  secondsRemaining: number
}

export function describeOrder(state: OrderState, reservedUntilUtc: string, now: number): OrderPresentation {
  const secondsRemaining = Math.max(0, Math.ceil((Date.parse(reservedUntilUtc) - now) / 1000))
  if (state !== 'Reserved') {
    return { state, canContinuePayment: false, secondsRemaining: 0 }
  }
  if (secondsRemaining === 0) {
    return { state: 'Expired', canContinuePayment: false, secondsRemaining: 0 }
  }
  return { state, canContinuePayment: true, secondsRemaining }
}
