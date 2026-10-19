export type Order = {
  id: string
  orderNumber: string
  contactEmail: string
  shippingAddress: {
    recipientName: string
    line1: string
    line2: string | null
    city: string
    region: string
    postalCode: string
    country: string
  }
  state: 'Reserved' | 'Paid' | 'Failed' | 'Expired' | 'Cancelled'
  reservedUntilUtc: string
  createdAtUtc: string
  subtotal: number
  shipping: number
  total: number
  items: Array<{
    productId: string
    name: string
    sku: string
    quantity: number
    unitPrice: number
    subtotal: number
  }>
}
