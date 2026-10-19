import { fileURLToPath } from 'node:url'
import { adminPassword, expect, test } from '../support/fixtures'
import {
  buyAndReserve,
  createOrderViaApi,
  fillAddress,
  fillProduct,
  findProduct,
  getAdminProduct,
  payWithSimulatedProvider,
  placeOrder,
  savePaymentSettings,
  saveShippingSettings,
  shippingAddress,
  startCheckout,
} from '../support/shop'

const csvFixture = fileURLToPath(new URL('../../tests/fixtures/loanpro-products.csv', import.meta.url))

test('E2E-01 admin login with email and password', async ({ page }) => {
  await page.goto('/admin/products')
  await expect(page).toHaveURL(/\/login/)
  await page.getByLabel('Email', { exact: true }).fill('admin@example.com')
  await page.getByLabel('Password', { exact: true }).fill('invalid-password')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page.getByRole('alert')).toHaveText('The email or password is incorrect.')
  await page.getByLabel('Password', { exact: true }).fill(adminPassword)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/admin\/products$/)
  await expect(page.getByRole('heading', { name: 'Product catalog' })).toBeVisible()
})

test('E2E-02 product CRUD', async ({ loginAs }) => {
  const { page } = await loginAs('admin')
  await page.goto('/admin/products/new')
  await fillProduct(page, {
    name: 'E2E CRUD grinder',
    sku: 'E2E-CRUD-01',
    description: 'Created by the end-to-end suite.',
    category: 'Coffee tools',
    price: '21.50',
    stock: '7',
  })
  await page.getByRole('button', { name: 'Save product' }).click()
  await expect(page).toHaveURL(/\/admin\/products$/)
  await expect(page.getByText('E2E CRUD grinder')).toBeVisible()

  await page.goto('/?q=E2E-CRUD-01')
  await expect(page.getByRole('link', { name: 'E2E CRUD grinder' })).toBeVisible()
  await page.goto('/admin/products')
  const row = page.getByRole('row').filter({ hasText: 'E2E CRUD grinder' })
  await row.getByRole('link', { name: 'Edit' }).click()
  await page.getByLabel('Name').fill('E2E CRUD grinder edited')
  await page.getByRole('button', { name: 'Save product' }).click()
  await expect(page.getByText('E2E CRUD grinder edited')).toBeVisible()
  page.once('dialog', (dialog) => dialog.accept())
  await page.getByRole('row').filter({ hasText: 'E2E CRUD grinder edited' })
    .getByRole('button', { name: 'Delete' }).click()
  await expect(page.getByText('E2E CRUD grinder edited')).toHaveCount(0)
  await page.goto('/?q=E2E-CRUD-01')
  await expect(page.getByText('No products match your filters.')).toBeVisible()
})

test('E2E-03 catalog search, filters, and shareable pagination', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('searchbox', { name: 'Search products' }).fill('E2E')
  await expect(page).toHaveURL(/q=E2E/)
  await page.getByLabel('Category').selectOption('Coffee')
  await page.getByLabel('Minimum price').fill('10')
  await page.getByLabel('Maximum price').fill('13')
  await expect(page.getByRole('link', { name: 'E2E Espresso' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'E2E Green Tea' })).toHaveCount(0)
  await expect(page.getByRole('link', { name: 'E2E Limited Mug' })).toHaveCount(0)
  await expect(page).toHaveURL(/category=Coffee/)
  await page.reload()
  await expect(page.getByRole('link', { name: 'E2E Espresso' })).toBeVisible()
  await page.getByLabel('Category').selectOption('')
  await page.getByLabel('Minimum price').fill('')
  await page.getByLabel('Maximum price').fill('')
  await expect(page.getByRole('button', { name: 'Next' })).toBeEnabled()
  await page.getByRole('button', { name: 'Next' }).click()
  await expect(page).toHaveURL(/page=2/)
})

test('E2E-04 simulated-provider purchase', async ({ loginAs }) => {
  const customer = await loginAs('customer')
  const admin = await loginAs('admin')
  const product = await findProduct(customer.request, 'E2E-ESPRESSO')
  await buyAndReserve(customer.page, 'E2E Espresso')
  await payWithSimulatedProvider(customer.page)
  const after = await getAdminProduct(admin.request, product.id)
  expect(after.stock).toBe(11)
  expect(after.availableStock).toBe(11)
})

test('E2E-05 simulated payment rejection', async ({ loginAs }) => {
  const customer = await loginAs('customer')
  const admin = await loginAs('admin')
  await savePaymentSettings(admin.request, 'simulated', {
    mode: 'AlwaysReject',
    latencyMilliseconds: '0',
    rejectCents: '99',
  }, true)
  const product = await findProduct(customer.request, 'E2E-ESPRESSO')
  await buyAndReserve(customer.page, 'E2E Espresso')
  await customer.page.getByRole('button', { name: 'Pay now' }).click()
  await expect(customer.page.getByRole('heading', { name: 'Payment failed' })).toBeVisible({ timeout: 15_000 })
  const after = await getAdminProduct(admin.request, product.id)
  expect(after.stock).toBe(12)
  expect(after.availableStock).toBe(12)
})

test('E2E-06 PayPal purchase with browser SDK and API stubs', async ({ loginAs }) => {
  const customer = await loginAs('customer')
  const admin = await loginAs('admin')
  await savePaymentSettings(admin.request, 'paypal', {
    clientId: 'e2e-paypal-client',
    sandbox: 'true',
  }, true, { clientSecret: 'e2e-paypal-secret' })
  await customer.page.route('https://www.paypal.com/sdk/js**', async (route) => {
    await route.fulfill({
      contentType: 'application/javascript',
      body: `window.paypal={Buttons:function(options){return{render:async function(selector){const root=document.querySelector(selector);const button=document.createElement('button');button.type='button';button.textContent='Pay with PayPal (E2E)';button.onclick=async()=>{try{const orderID=await options.createOrder();await options.onApprove({orderID})}catch(error){options.onError(error)}};root.appendChild(button)}}}}`,
    })
  })
  await buyAndReserve(customer.page, 'E2E Espresso')
  const orderId = customer.page.url().split('/').at(-1)!
  await customer.page.getByRole('button', { name: 'Pay with PayPal (E2E)' }).click()
  await expect(customer.page).toHaveURL(/\/account\/orders\//, { timeout: 15_000 })
  await expect(customer.page.getByText(/^Paid ·/)).toBeVisible()
  const history = await admin.request.get('/api/admin/orders')
  expect(history.ok()).toBeTruthy()
  const order = (await history.json()).items.find((item: { id: string }) => item.id === orderId)
  expect(order.paymentProviderKey).toBe('paypal')
})

test('E2E-07 switching the active payment provider', async ({ loginAs }) => {
  const customer = await loginAs('customer')
  const admin = await loginAs('admin')
  await savePaymentSettings(admin.request, 'paypal', {
    clientId: 'e2e-paypal-client',
    sandbox: 'true',
  }, true, { clientSecret: 'e2e-paypal-secret' })
  const settings = await admin.request.get('/api/admin/payment-providers')
  const providers = await settings.json()
  expect(providers.filter((provider: { isActive: boolean }) => provider.isActive)).toHaveLength(1)
  expect(providers.find((provider: { key: string }) => provider.key === 'paypal').isActive).toBe(true)

  await savePaymentSettings(admin.request, 'simulated', {
    mode: 'AlwaysApprove',
    latencyMilliseconds: '0',
    rejectCents: '99',
  }, true)
  await buyAndReserve(customer.page, 'E2E Espresso')
  await expect(customer.page.getByRole('button', { name: 'Pay now' })).toBeVisible()
})

test('E2E-08 shipping setting changes affect only new orders', async ({ loginAs }) => {
  const customer = await loginAs('customer')
  const admin = await loginAs('admin')
  await saveShippingSettings(admin.request, '2.00')
  await startCheckout(customer.page, 'E2E Espresso')
  await fillAddress(customer.page)
  await expect(customer.page.getByRole('status')).toContainText('$2.00')
  await placeOrder(customer.page)
  await expect(customer.page.locator('.order-totals')).toContainText('$2.00')
  await saveShippingSettings(admin.request, '5.00')
  await customer.page.reload()
  await expect(customer.page.locator('.order-totals')).toContainText('$2.00')
})

test('E2E-09 bilingual UI and persistent locale selection', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'Español' }).click()
  await expect(page.getByRole('heading', { name: 'Catálogo de productos' })).toBeVisible()
  await expect(page.locator('.catalog-card').first()).toContainText('US$')
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Catálogo de productos' })).toBeVisible()
  await page.goto('/login')
  await page.getByRole('button', { name: 'English' }).click()
  await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible()
})

test('E2E-10 CSV preview and import golden totals', async ({ loginAs }) => {
  const { page, request } = await loginAs('admin')
  await page.goto('/admin/products/import')
  await page.locator('input[type=file]').setInputFiles(csvFixture)
  await page.getByRole('button', { name: 'Preview import' }).click()
  await expect(page.getByRole('definition').filter({ hasText: /^87$/ })).toBeVisible()
  await expect(page.getByRole('definition').filter({ hasText: /^121737$/ })).toBeVisible()
  const dryRunProduct = await request.get('/api/catalog?q=RS-001')
  const dryRunCatalog = await dryRunProduct.json()
  expect(dryRunCatalog.totalCount).toBe(0)

  await page.getByRole('button', { name: 'Apply import' }).click()
  await expect(page.getByRole('heading', { name: 'Import result' })).toBeVisible()
  const imported = await request.get('/api/catalog?q=RS-001')
  expect((await imported.json()).totalCount).toBeGreaterThan(0)
})

test('E2E-11 duplicate SKU aggregation and confirmed reimport', async ({ loginAs }) => {
  const { page, request } = await loginAs('admin')
  await page.goto('/admin/products/import')
  const csv = 'name,sku,description,category,price,stock,weight_kg\nFirst Duplicate,E2E-DUP-01,first,Test,4.50,3,0.2\nFirst Duplicate,E2E-DUP-01,first,Test,4.50,5,0.2\n'
  await page.locator('input[type=file]').setInputFiles({
    name: 'duplicate-import.csv',
    mimeType: 'text/csv',
    buffer: Buffer.from(csv),
  })
  await page.getByRole('button', { name: 'Preview import' }).click()
  await expect(page.getByText('1', { exact: true }).first()).toBeVisible()
  await page.getByRole('button', { name: 'Apply import' }).click()
  await expect(page.getByRole('heading', { name: 'Import result' })).toBeVisible()
  const firstImport = await findProduct(request, 'E2E-DUP-01')
  expect(firstImport.stock).toBe(8)
  await page.getByRole('button', { name: 'Preview import' }).click()
  await expect(page.getByText(/previously imported/)).toBeVisible()
  await page.getByLabel('I understand this reimport will add stock again.').check()
  await page.getByRole('button', { name: 'Apply import' }).click()
  const product = await findProduct(request, 'E2E-DUP-01')
  expect(product.stock).toBe(16)
})

test('E2E-12 cart quantity limits and server inventory validation', async ({ loginAs }) => {
  const customer = await loginAs('customer')
  const { page, request } = customer
  const product = await findProduct(request, 'E2E-MUG')
  await page.goto(`/products/${product.id}`)
  await page.getByRole('button', { name: 'Add to cart' }).click()
  await page.goto('/cart')
  const quantity = page.getByRole('spinbutton', { name: 'Quantity for E2E Limited Mug' })
  await expect(quantity).toHaveAttribute('max', '1')
  await quantity.fill('2')
  await expect(quantity).toHaveValue('1')

  const conflict = await request.post('/api/orders', {
    headers: { 'Idempotency-Key': crypto.randomUUID() },
    data: { shippingAddress, items: [{ productId: product.id, quantity: 2 }] },
  })
  expect(conflict.status()).toBe(409)
  expect((await conflict.json()).code).toBe('inventory_changed')
})

test('E2E-13 reservation expiry releases inventory and blocks payment', async ({ loginAs }) => {
  const { page, request } = await loginAs('customer')
  await buyAndReserve(page, 'E2E Espresso')
  const orderId = page.url().split('/').at(-1)!
  const payment = await request.post(`/api/orders/${orderId}/payment`)
  expect(payment.ok(), await payment.text()).toBeTruthy()
  const { externalReference } = await payment.json()
  await expect(page.getByRole('heading', { name: 'Reservation expired' }))
    .toBeVisible({ timeout: 35_000 })
  const capturePayment = await request.post(`/api/orders/${orderId}/payment/capture`, {
    data: { externalReference },
  })
  expect(capturePayment.status()).toBe(409)
  expect((await capturePayment.json()).code).toBe('reservation_expired')
})

test('E2E-14 concurrent customers competing for the final unit', async ({ loginAs, registerCustomer, request }) => {
  const product = await findProduct(request, 'E2E-MUG')
  const first = await loginAs('customer', await registerCustomer({ fullName: 'First Buyer' }))
  const second = await loginAs('customer', await registerCustomer({ fullName: 'Second Buyer' }))
  const pages = [first.page, second.page]
  for (const page of pages) {
    await page.goto(`/products/${product.id}`)
    await page.getByRole('button', { name: 'Add to cart' }).click()
    await page.goto('/checkout')
    await fillAddress(page)
  }
  await Promise.all(pages.map((page) =>
    page.getByRole('button', { name: 'Reserve and place order' }).click(),
  ))
  await expect.poll(() => pages.filter((page) => page.url().includes('/orders/')).length)
    .toBe(1)
  const loser = pages.find((page) => !page.url().includes('/orders/'))!
  await expect(loser.getByRole('alert')).toContainText('Inventory changed')
})

test('E2E-15 malicious-looking product text is rendered safely', async ({ page }) => {
  const dialogs: string[] = []
  page.on('dialog', async (dialog) => {
    dialogs.push(dialog.message())
    await dialog.dismiss()
  })
  await page.goto('/')
  await page.getByRole('searchbox', { name: 'Search products' }).fill('<script>')
  await expect(page.getByText("<script>window.alert('xss')</script> Coffee")).toBeVisible()
  await expect(page.locator('.catalog-card script')).toHaveCount(0)
  await page.getByRole('searchbox', { name: 'Search products' }).fill("' OR 1=1 --")
  await expect(page.getByText("Coffee ' OR 1=1 --")).toBeVisible()
  expect(dialogs).toEqual([])
})

test('E2E-16 admin order history shows completed purchases and customer name', async ({ loginAs }) => {
  const customer = await loginAs('customer', undefined)
  await buyAndReserve(customer.page, 'E2E Espresso')
  await payWithSimulatedProvider(customer.page)
  const admin = await loginAs('admin')
  await admin.page.goto('/admin/orders')
  await expect(admin.page.getByRole('table')).toContainText('Simulated')
  await expect(admin.page.getByRole('table')).toContainText('$12.50')
  await expect(admin.page.getByRole('table')).toContainText(customer.customer!.fullName)
})
