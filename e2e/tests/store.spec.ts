import { expect, test, type APIRequestContext, type Page } from '@playwright/test'
import { fileURLToPath } from 'node:url'

const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'e2e-admin-only-local'
const csvFixture = fileURLToPath(new URL('../../tests/fixtures/loanpro-products.csv', import.meta.url))

test.beforeEach(async ({ request }) => {
  const response = await request.post('/e2e/reset')
  expect(response.status(), 'E2E reset endpoint must be enabled for the E2E profile').toBe(204)
})

test('E2E-01 admin login', async ({ page }) => {
  await page.goto('/admin/products')
  await expect(page).toHaveURL(/\/login$/)
  await page.getByLabel('Admin password').fill('invalid-password')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page.getByRole('alert')).toHaveText('The password is incorrect.')
  await page.getByLabel('Admin password').fill(adminPassword)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/admin\/products$/)
  await expect(page.getByRole('heading', { name: 'Product catalog' })).toBeVisible()
})

test('E2E-02 product CRUD', async ({ page }) => {
  await signIn(page)
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

test('E2E-04 simulated-provider purchase', async ({ page, request }) => {
  const token = await login(request)
  const product = await findProduct(request, 'E2E-ESPRESSO')
  await buyProduct(page, 'E2E Espresso')
  await fillCheckout(page)
  await page.getByRole('button', { name: 'Reserve and place order' }).click()
  await expect(page).toHaveURL(/\/orders\//)
  await page.getByRole('button', { name: 'Pay now' }).click()
  await expect(page.getByRole('heading', { name: 'Paid' })).toBeVisible({ timeout: 15_000 })
  const after = await getProduct(request, product.id, token)
  expect(after.stock).toBe(11)
  expect(after.availableStock).toBe(11)
})

test('E2E-05 simulated payment rejection', async ({ page, request }) => {
  const token = await login(request)
  await savePaymentSettings(request, token, 'simulated', {
    mode: 'AlwaysReject',
    latencyMilliseconds: '0',
    rejectCents: '99',
  }, true)
  const product = await findProduct(request, 'E2E-ESPRESSO')
  await buyProduct(page, 'E2E Espresso')
  await fillCheckout(page)
  await page.getByRole('button', { name: 'Reserve and place order' }).click()
  await expect(page).toHaveURL(/\/orders\//)
  await page.getByRole('button', { name: 'Pay now' }).click()
  await expect(page.getByRole('heading', { name: 'Payment failed' })).toBeVisible({ timeout: 15_000 })
  const after = await getProduct(request, product.id, token)
  expect(after.stock).toBe(12)
  expect(after.availableStock).toBe(12)
})

test('E2E-06 PayPal purchase with browser SDK and API stubs', async ({ page, request }) => {
  const token = await login(request)
  await savePaymentSettings(request, token, 'paypal', {
    clientId: 'e2e-paypal-client',
    sandbox: 'true',
  }, true, { clientSecret: 'e2e-paypal-secret' })
  await page.route('https://www.paypal.com/sdk/js**', async (route) => {
    await route.fulfill({
      contentType: 'application/javascript',
      body: `window.paypal={Buttons:function(options){return{render:async function(selector){const root=document.querySelector(selector);const button=document.createElement('button');button.type='button';button.textContent='Pay with PayPal (E2E)';button.onclick=async()=>{try{const orderID=await options.createOrder();await options.onApprove({orderID})}catch(error){options.onError(error)}};root.appendChild(button)}}}}`,
    })
  })
  await buyProduct(page, 'E2E Espresso')
  await fillCheckout(page)
  await page.getByRole('button', { name: 'Reserve and place order' }).click()
  await expect(page).toHaveURL(/\/orders\//)
  const orderId = page.url().split('/').at(-1)!
  await page.getByRole('button', { name: 'Pay with PayPal (E2E)' }).click()
  await expect(page.getByRole('heading', { name: 'Paid' })).toBeVisible({ timeout: 15_000 })
  const history = await request.get('/api/admin/orders', { headers: bearer(token) })
  expect(history.ok()).toBeTruthy()
  const order = (await history.json()).items.find((item: { id: string }) => item.id === orderId)
  expect(order.paymentProviderKey).toBe('paypal')
})

test('E2E-07 switching the active payment provider', async ({ page, request }) => {
  const token = await login(request)
  await savePaymentSettings(request, token, 'paypal', {
    clientId: 'e2e-paypal-client',
    sandbox: 'true',
  }, true, { clientSecret: 'e2e-paypal-secret' })
  const settings = await request.get('/api/admin/payment-providers', {
    headers: bearer(token),
  })
  const providers = await settings.json()
  expect(providers.filter((provider: { isActive: boolean }) => provider.isActive)).toHaveLength(1)
  expect(providers.find((provider: { key: string }) => provider.key === 'paypal').isActive).toBe(true)

  await savePaymentSettings(request, token, 'simulated', {
    mode: 'AlwaysApprove',
    latencyMilliseconds: '0',
    rejectCents: '99',
  }, true)
  await buyProduct(page, 'E2E Espresso')
  await fillCheckout(page)
  await page.getByRole('button', { name: 'Reserve and place order' }).click()
  await expect(page).toHaveURL(/\/orders\//)
  await expect(page.getByRole('button', { name: 'Pay now' })).toBeVisible()
})

test('E2E-08 shipping setting changes affect only new orders', async ({ page, request }) => {
  const token = await login(request)
  await saveShippingSettings(request, token, '2.00')
  await buyProduct(page, 'E2E Espresso')
  await fillCheckout(page)
  await expect(page.getByRole('status')).toContainText('$2.00')
  await page.getByRole('button', { name: 'Reserve and place order' }).click()
  await expect(page).toHaveURL(/\/orders\//)
  await expect(page.locator('.order-totals')).toContainText('$2.00')
  await saveShippingSettings(request, token, '5.00')
  await page.reload()
  await expect(page.locator('.order-totals')).toContainText('$2.00')
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
  await expect(page.getByRole('heading', { name: 'Admin login' })).toBeVisible()
  await signIn(page)
  await page.goto('/admin/settings')
  await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible()
})

test('E2E-10 CSV preview and import golden totals', async ({ page, request }) => {
  await signIn(page)
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

test('E2E-11 duplicate SKU aggregation and confirmed reimport', async ({ page, request }) => {
  await signIn(page)
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

test('E2E-12 cart quantity limits and server inventory validation', async ({ page, request }) => {
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
    data: {
      customerName: 'E2E Guest',
      customerEmail: 'e2e@example.test',
      shippingAddress: '123 Test Street',
      items: [{ productId: product.id, quantity: 2 }],
    },
  })
  expect(conflict.status()).toBe(409)
  expect((await conflict.json()).code).toBe('inventory_changed')
})

test('E2E-13 reservation expiry releases inventory and blocks payment', async ({ page, request }) => {
  await buyProduct(page, 'E2E Espresso')
  await fillCheckout(page)
  await page.getByRole('button', { name: 'Reserve and place order' }).click()
  await expect(page).toHaveURL(/\/orders\//)
  const orderId = page.url().split('/').at(-1)!
  const capability = await page.evaluate((key) => localStorage.getItem(key), `store:order-access:${orderId}`)
  const payment = await request.post(`/api/orders/${orderId}/payment`, {
    headers: { 'Idempotency-Key': capability ?? '' },
  })
  expect(payment.ok()).toBeTruthy()
  const { externalReference } = await payment.json()
  await expect(page.getByRole('heading', { name: 'Reservation expired' }))
    .toBeVisible({ timeout: 35_000 })
  const capturePayment = await request.post(`/api/orders/${orderId}/payment/capture`, {
    headers: { 'Idempotency-Key': capability ?? '' },
    data: { externalReference },
  })
  expect(capturePayment.status()).toBe(409)
  expect((await capturePayment.json()).code).toBe('reservation_expired')
})

test('E2E-14 concurrent buyers competing for the final unit', async ({ browser, request }) => {
  const product = await findProduct(request, 'E2E-MUG')
  const contexts = await Promise.all([browser.newContext(), browser.newContext()])
  const pages = await Promise.all(contexts.map((context) => context.newPage()))
  for (const page of pages) {
    await page.goto(`/products/${product.id}`)
    await page.getByRole('button', { name: 'Add to cart' }).click()
    await page.goto('/checkout')
    await fillCheckout(page)
  }
  await Promise.all(pages.map((page) =>
    page.getByRole('button', { name: 'Reserve and place order' }).click(),
  ))
  await expect.poll(() => pages.filter((page) => page.url().includes('/orders/')).length)
    .toBe(1)
  const winners = pages.filter((page) => page.url().includes('/orders/')).length
  expect(winners).toBe(1)
  const loser = pages.find((page) => !page.url().includes('/orders/'))!
  await expect(loser.getByRole('alert'))
    .toContainText('Inventory changed')
  await Promise.all(contexts.map((context) => context.close()))
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

test('E2E-16 admin order history shows completed purchases', async ({ page }) => {
  await buyProduct(page, 'E2E Espresso')
  await fillCheckout(page)
  await page.getByRole('button', { name: 'Reserve and place order' }).click()
  await expect(page).toHaveURL(/\/orders\//)
  await page.getByRole('button', { name: 'Pay now' }).click()
  await expect(page.getByRole('heading', { name: 'Paid' })).toBeVisible({ timeout: 15_000 })
  await signIn(page)
  await page.goto('/admin/orders')
  await expect(page.getByRole('table')).toContainText('Simulated')
  await expect(page.getByRole('table')).toContainText('$12.50')
})

async function signIn(page: Page) {
  await page.goto('/login')
  await page.getByLabel('Admin password').fill(adminPassword)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page).toHaveURL(/\/admin\/products$/)
}

async function login(request: APIRequestContext) {
  const response = await request.post('/auth/login', { data: { password: adminPassword } })
  expect(response.ok()).toBeTruthy()
  return (await response.json()).accessToken as string
}

async function buyProduct(page: Page, name: string) {
  await page.goto('/')
  await page.getByRole('link', { name }).click()
  await page.getByRole('button', { name: 'Add to cart' }).click()
  await page.goto('/cart')
  await page.getByRole('link', { name: 'Continue to checkout' }).click()
  await expect(page.getByRole('heading', { name: 'Guest checkout' })).toBeVisible()
}

async function fillCheckout(page: Page) {
  await page.getByLabel('Full name').fill('E2E Guest')
  await page.getByLabel('Email address').fill('e2e@example.test')
  await page.getByLabel('Shipping address').fill('123 Test Street')
}

async function fillProduct(
  page: Page,
  product: { name: string; sku: string; description: string; category: string; price: string; stock: string },
) {
  await page.getByLabel('Name', { exact: true }).fill(product.name)
  await page.getByLabel('SKU', { exact: true }).fill(product.sku)
  await page.getByLabel('Description', { exact: true }).fill(product.description)
  await page.getByLabel('Category', { exact: true }).fill(product.category)
  await page.getByLabel('Price (USD)', { exact: true }).fill(product.price)
  await page.getByLabel('Stock', { exact: true }).fill(product.stock)
}

async function findProduct(request: APIRequestContext, sku: string) {
  const response = await request.get(`/api/catalog?q=${encodeURIComponent(sku)}`)
  expect(response.ok()).toBeTruthy()
  const page = await response.json()
  return page.items.find((product: { sku: string }) => product.sku === sku)
}

async function getProduct(request: APIRequestContext, id: string, token: string) {
  const response = await request.get(`/api/admin/products/${id}`, { headers: bearer(token) })
  expect(response.ok()).toBeTruthy()
  return response.json()
}

async function savePaymentSettings(
  request: APIRequestContext,
  token: string,
  key: string,
  publicConfiguration: Record<string, string>,
  activate: boolean,
  secrets: Record<string, string> = {},
) {
  const response = await request.put(`/api/admin/payment-providers/${key}`, {
    headers: bearer(token),
    data: { publicConfiguration, secrets, activate, enabled: true },
  })
  expect(response.ok(), await response.text()).toBeTruthy()
}

async function saveShippingSettings(request: APIRequestContext, token: string, amountUsd: string) {
  const response = await request.put('/api/admin/settings/shipping/flat-rate', {
    headers: bearer(token),
    data: { publicConfiguration: { amountUsd }, secrets: {}, enabled: true, activate: true },
  })
  expect(response.ok(), await response.text()).toBeTruthy()
}

function bearer(token: string) {
  return { Authorization: `Bearer ${token}` }
}
