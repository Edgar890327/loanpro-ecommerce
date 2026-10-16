import { act, cleanup, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { useCart } from './useCart'
import { CartProvider } from './CartProvider'

function CartProbe() {
  const cart = useCart()
  return (
    <>
      <button type="button" onClick={() => cart.addProduct({
        id: 'product-1',
        name: 'Tea',
        sku: 'TEA-1',
        description: '',
        category: 'Food',
        price: 4,
        stock: 2,
        weightKg: null,
      })}>Add</button>
      <button type="button" onClick={() => cart.setQuantity('product-1', 9)}>Set high quantity</button>
      <button type="button" onClick={() => cart.updateAvailability('product-1', 1)}>Refresh availability</button>
      <output>{cart.items[0]?.quantity ?? 0}</output>
    </>
  )
}

describe('CartProvider', () => {
  afterEach(cleanup)
  beforeEach(() => window.localStorage.clear())

  it('persists a product and caps the selected quantity at available stock', () => {
    render(<CartProvider><CartProbe /></CartProvider>)
    act(() => screen.getByRole('button', { name: 'Add' }).click())
    act(() => screen.getByRole('button', { name: 'Add' }).click())
    act(() => screen.getByRole('button', { name: 'Set high quantity' }).click())

    expect(screen.getByText('2')).toBeInTheDocument()
    expect(JSON.parse(window.localStorage.getItem('store:cart') ?? '[]')).toMatchObject([
      { productId: 'product-1', quantity: 2, available: 2 },
    ])
  })

  it('reduces a cart quantity when the server reports less available stock', () => {
    render(<CartProvider><CartProbe /></CartProvider>)
    act(() => screen.getByRole('button', { name: 'Add' }).click())
    act(() => screen.getByRole('button', { name: 'Add' }).click())
    act(() => screen.getByRole('button', { name: 'Refresh availability' }).click())

    expect(screen.getByText('1')).toBeInTheDocument()
  })
})
