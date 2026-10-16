import type { Product } from './Product'

export type ProductInput = Omit<Product, 'id' | 'availableStock' | 'isDeleted' | 'createdAtUtc' | 'updatedAtUtc' | 'version'> & {
  version?: number
}
