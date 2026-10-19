import { expect, type APIRequestContext, type Page } from '@playwright/test'

export const shippingAddress = {
  recipientName: 'E2E Customer',
  line1: '123 Test Street',
  line2: null,
  city: 'Austin',
  region: 'TX',
  postalCode: '73301',
  country: 'US',
}

export async function addToCart(page: Page, productName: string) {
  await page.goto('/')
  await page.getByRole('link', { name: productName }).click()
  await page.getByRole('button', { name: 'Add to cart' }).click()
}

export async function startCheckout(page: Page, productName: string) {
  await addToCart(page, productName)
  await page.goto('/cart')
  await page.getByRole('link', { name: 'Continue to checkout' }).click()
  await expect(page.getByRole('heading', { name: 'Checkout' })).toBeVisible()
}

export async function fillAddress(page: Page) {
  await page.getByLabel('Address line 1', { exact: true }).fill(shippingAddress.line1)
  await page.getByLabel('City', { exact: true }).fill(shippingAddress.city)
  await page.getByLabel('State or region', { exact: true }).fill(shippingAddress.region)
  await page.getByLabel('Postal code', { exact: true }).fill(shippingAddress.postalCode)
  await page.getByLabel('Country', { exact: true }).fill(shippingAddress.country)
}

export async function placeOrder(page: Page) {
  await page.getByRole('button', { name: 'Reserve and place order' }).click()
  await expect(page).toHaveURL(/\/orders\/[0-9a-f-]+$/)
}

export async function buyAndReserve(page: Page, productName: string) {
  await startCheckout(page, productName)
  await fillAddress(page)
  await placeOrder(page)
}

export async function payWithSimulatedProvider(page: Page) {
  await page.getByRole('button', { name: 'Pay now' }).click()
  await expect(page).toHaveURL(/\/account\/orders\/[0-9a-f-]+$/, { timeout: 15_000 })
  await expect(page.getByText(/^Paid ·/)).toBeVisible()
}

export type CreatedOrder = { id: string, orderNumber: string }

export async function createOrderViaApi(
  request: APIRequestContext,
  productId: string,
  quantity = 1,
): Promise<CreatedOrder> {
  const response = await request.post('/api/orders', {
    headers: { 'Idempotency-Key': crypto.randomUUID() },
    data: { shippingAddress, items: [{ productId, quantity }] },
  })
  expect(response.status(), await response.text()).toBe(201)
  return response.json()
}

export async function findProduct(request: APIRequestContext, sku: string) {
  const response = await request.get(`/api/catalog?q=${encodeURIComponent(sku)}`)
  expect(response.ok()).toBeTruthy()
  const page = await response.json()
  return page.items.find((product: { sku: string }) => product.sku === sku)
}

export async function getAdminProduct(admin: APIRequestContext, id: string) {
  const response = await admin.get(`/api/admin/products/${id}`)
  expect(response.ok()).toBeTruthy()
  return response.json()
}

export async function savePaymentSettings(
  admin: APIRequestContext,
  key: string,
  publicConfiguration: Record<string, string>,
  activate: boolean,
  secrets: Record<string, string> = {},
) {
  const response = await admin.put(`/api/admin/payment-providers/${key}`, {
    data: { publicConfiguration, secrets, activate, enabled: true },
  })
  expect(response.ok(), await response.text()).toBeTruthy()
}

export async function saveShippingSettings(admin: APIRequestContext, amountUsd: string) {
  const response = await admin.put('/api/admin/settings/shipping/flat-rate', {
    data: { publicConfiguration: { amountUsd }, secrets: {}, enabled: true, activate: true },
  })
  expect(response.ok(), await response.text()).toBeTruthy()
}

export async function fillProduct(
  page: Page,
  product: { name: string, sku: string, description: string, category: string, price: string, stock: string },
) {
  await page.getByLabel('Name', { exact: true }).fill(product.name)
  await page.getByLabel('SKU', { exact: true }).fill(product.sku)
  await page.getByLabel('Description', { exact: true }).fill(product.description)
  await page.getByLabel('Category', { exact: true }).fill(product.category)
  await page.getByLabel('Price (USD)', { exact: true }).fill(product.price)
  await page.getByLabel('Stock', { exact: true }).fill(product.stock)
}
