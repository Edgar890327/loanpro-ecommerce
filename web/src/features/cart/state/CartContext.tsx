import { createContext } from 'react'
import type { CatalogProduct } from '../../catalog/types/CatalogProduct'
import type { CartItem } from '../types/CartItem'

export type CartContextValue = {
  items: CartItem[]
  addProduct: (product: CatalogProduct) => void
  updateAvailability: (productId: string, available: number, product?: Pick<CatalogProduct, 'name' | 'sku' | 'price'>) => void
  setQuantity: (productId: string, quantity: number) => void
  removeProduct: (productId: string) => void
  clear: () => void
}

export const CartContext = createContext<CartContextValue | null>(null)
