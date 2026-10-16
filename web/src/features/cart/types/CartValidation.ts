export type CartValidation = {
  isAvailable: boolean
  items: Array<{
    productId: string
    name: string
    sku: string
    quantity: number
    available: number
    unitPrice: number
    subtotal: number
  }>
  issues: Array<{ productId: string; requested: number; available: number }>
  subtotal: number
  shipping: number
  total: number
}
