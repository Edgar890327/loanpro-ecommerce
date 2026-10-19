import type { OrderState } from './OrderState'

export type MyOrderDetail = {
  id: string
  orderNumber: string
  state: OrderState
  subtotal: number
  shipping: number
  total: number
  createdAtUtc: string
  reservedUntilUtc: string
  contactEmail: string
  paymentProviderKey: string | null
  paymentReference: string | null
  shippingAddress: {
    recipientName: string
    line1: string
    line2: string | null
    city: string
    region: string
    postalCode: string
    country: string
  }
  items: Array<{
    productName: string
    sku: string
    quantity: number
    unitPrice: number
    subtotal: number
  }>
}
