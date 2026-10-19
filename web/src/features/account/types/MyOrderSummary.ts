import type { OrderState } from './OrderState'

export type MyOrderSummary = {
  id: string
  orderNumber: string
  state: OrderState
  total: number
  createdAtUtc: string
  reservedUntilUtc: string
  paymentProviderKey: string | null
}
