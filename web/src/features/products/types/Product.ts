export type Product = {
  id: string
  name: string
  sku: string
  description: string
  category: string
  price: number
  stock: number
  availableStock: number
  weightKg: number | null
  isDeleted: boolean
  createdAtUtc: string
  updatedAtUtc: string
  version: number
}
