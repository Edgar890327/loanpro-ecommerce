export type Order = {
  id: string
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
