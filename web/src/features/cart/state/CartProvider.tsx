import { useCallback, useEffect, useState, type PropsWithChildren } from 'react'
import type { CatalogProduct } from '../../catalog/types/CatalogProduct'
import type { CartItem } from '../types/CartItem'
import { CartContext } from './CartContext'

const storageKey = 'store:cart'

function readCart(): CartItem[] {
  const stored = window.localStorage.getItem(storageKey)
  if (!stored) return []
  const parsed: unknown = JSON.parse(stored)
  if (!Array.isArray(parsed)) return []
  return parsed.filter(isCartItem)
}

function isCartItem(value: unknown): value is CartItem {
  if (!value || typeof value !== 'object') return false
  const item = value as Partial<CartItem>
  return typeof item.productId === 'string'
    && typeof item.name === 'string'
    && typeof item.sku === 'string'
    && typeof item.price === 'number'
    && typeof item.available === 'number'
    && typeof item.quantity === 'number'
}

export function CartProvider({ children }: PropsWithChildren) {
  const [items, setItems] = useState<CartItem[]>(readCart)

  useEffect(() => {
    window.localStorage.setItem(storageKey, JSON.stringify(items))
  }, [items])

  function addProduct(product: CatalogProduct) {
    if (product.stock <= 0) return
    setItems((current) => {
      const existing = current.find((item) => item.productId === product.id)
      if (existing) {
        return current.map((item) => item.productId === product.id
          ? { ...item, name: product.name, sku: product.sku, price: product.price, available: product.stock, quantity: Math.min(item.quantity + 1, product.stock) }
          : item)
      }

      return [...current, {
        productId: product.id,
        name: product.name,
        sku: product.sku,
        price: product.price,
        available: product.stock,
        quantity: 1,
      }]
    })
  }

  const updateAvailability = useCallback((
    productId: string,
    available: number,
    product?: Pick<CatalogProduct, 'name' | 'sku' | 'price'>,
  ) => {
    setItems((current) => {
      let changed = false
      const next = current.map((item) => {
        if (item.productId !== productId) return item
        const quantity = available > 0 ? Math.min(item.quantity, available) : item.quantity
        if (item.available === available &&
            item.quantity === quantity &&
            (!product || (item.name === product.name && item.sku === product.sku && item.price === product.price))) {
          return item
        }

        changed = true
        return { ...item, available, quantity, ...product }
      })
      return changed ? next : current
    })
  }, [])

  function setQuantity(productId: string, quantity: number) {
    setItems((current) => current.map((item) => item.productId === productId && item.available > 0
      ? { ...item, quantity: Math.max(1, Math.min(Math.floor(quantity || 1), item.available)) }
      : item))
  }

  function removeProduct(productId: string) {
    setItems((current) => current.filter((item) => item.productId !== productId))
  }

  function clear() {
    setItems([])
  }

  return (
    <CartContext.Provider value={{ items, addProduct, updateAvailability, setQuantity, removeProduct, clear }}>
      {children}
    </CartContext.Provider>
  )
}
