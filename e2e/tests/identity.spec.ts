import { expect, test, csrfHeaders, customerPassword } from '../support/fixtures'
import {
  addToCart,
  buyAndReserve,
  createOrderViaApi,
  findProduct,
  shippingAddress,
} from '../support/shop'

test('E2E-17 registration from checkout keeps the cart', async ({ page }) => {
  await addToCart(page, 'E2E Espresso')
  await page.goto('/cart')
  await page.getByRole('link', { name: 'Continue to checkout' }).click()
  await expect(page).toHaveURL(/\/login\?returnUrl=/)
  await page.locator('#main-content').getByRole('link', { name: 'Sign up' }).click()
  await expect(page.getByRole('heading', { name: 'Create your account' })).toBeVisible()
  await page.getByLabel('Full name', { exact: true }).fill('Checkout Newcomer')
  await page.getByLabel('Email', { exact: true }).fill(`newcomer-${crypto.randomUUID()}@example.test`)
  await page.getByLabel('Password', { exact: true }).fill(customerPassword)
  await page.getByRole('button', { name: 'Sign up' }).click()
  await expect(page).toHaveURL(/\/checkout$/)
  await expect(page.getByRole('heading', { name: 'Checkout' })).toBeVisible()
  await expect(page.getByLabel('Recipient name', { exact: true })).toHaveValue('Checkout Newcomer')
  await page.goto('/cart')
  await expect(page.getByText('E2E Espresso')).toBeVisible()
})

test('E2E-18 invalid credentials and duplicate email', async ({ page, registerCustomer }) => {
  const customer = await registerCustomer()
  await page.goto('/login')
  await page.getByLabel('Email', { exact: true }).fill(customer.email)
  await page.getByLabel('Password', { exact: true }).fill('wrong-password-value')
  await page.getByRole('button', { name: 'Sign in' }).click()
  const wrongPassword = await page.getByRole('alert').textContent()
  expect(wrongPassword).toBe('The email or password is incorrect.')

  await page.getByLabel('Email', { exact: true }).fill('nobody@example.test')
  await page.getByRole('button', { name: 'Sign in' }).click()
  await expect(page.getByRole('alert')).toHaveText(wrongPassword!)

  await page.goto('/register')
  await page.getByLabel('Full name', { exact: true }).fill('Duplicate Person')
  await page.getByLabel('Email', { exact: true }).fill(customer.email)
  await page.getByLabel('Password', { exact: true }).fill(customerPassword)
  await page.getByRole('button', { name: 'Sign up' }).click()
  await expect(page.getByRole('alert')).toHaveText('An account with this email already exists.')
})

test('E2E-19 my purchases search and filters', async ({ loginAs }) => {
  const { page, request } = await loginAs('customer')
  const espresso = await findProduct(request, 'E2E-ESPRESSO')
  const tea = await findProduct(request, 'E2E-TEA')
  const first = await createOrderViaApi(request, espresso.id)
  const second = await createOrderViaApi(request, tea.id)
  const cancel = await request.post(`/api/orders/${second.id}/cancel`)
  expect(cancel.ok(), await cancel.text()).toBeTruthy()

  await page.goto('/account/orders')
  const search = page.getByLabel('Search by order number, product or SKU')
  const rows = page.getByRole('row')

  await search.fill(first.orderNumber.toLowerCase())
  await expect(rows.filter({ hasText: first.orderNumber })).toHaveCount(1)
  await expect(rows.filter({ hasText: second.orderNumber })).toHaveCount(0)

  await search.fill('Green Tea')
  await expect(rows.filter({ hasText: second.orderNumber })).toHaveCount(1)
  await expect(rows.filter({ hasText: first.orderNumber })).toHaveCount(0)

  await search.fill('E2E-ESPRESSO')
  await expect(rows.filter({ hasText: first.orderNumber })).toHaveCount(1)
  await expect(rows.filter({ hasText: second.orderNumber })).toHaveCount(0)

  await page.getByRole('search').getByRole('button', { name: 'Clear filters' }).click()
  await expect(rows.filter({ hasText: first.orderNumber })).toHaveCount(1)
  await expect(rows.filter({ hasText: second.orderNumber })).toHaveCount(1)

  await page.getByRole('combobox', { name: /^Status/ }).selectOption('Cancelled')
  await expect(rows.filter({ hasText: second.orderNumber })).toHaveCount(1)
  await expect(rows.filter({ hasText: first.orderNumber })).toHaveCount(0)
  await page.getByRole('search').getByRole('button', { name: 'Clear filters' }).click()

  const yesterday = new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10)
  await page.locator('input[type=date]').nth(1).fill(yesterday)
  await expect(page.getByText('No purchases match your filters.')).toBeVisible()
  await page.getByRole('search').getByRole('button', { name: 'Clear filters' }).click()
  await expect(rows.filter({ hasText: first.orderNumber })).toHaveCount(1)
})

test('E2E-20 a customer cannot open another customer purchase', async ({ loginAs }) => {
  const owner = await loginAs('customer')
  const intruder = await loginAs('customer')
  const product = await findProduct(owner.request, 'E2E-ESPRESSO')
  const order = await createOrderViaApi(owner.request, product.id)

  await intruder.page.goto(`/account/orders/${order.id}`)
  await expect(intruder.page.getByText('This purchase could not be found.')).toBeVisible()
  expect((await intruder.request.get(`/me/orders/${order.id}`)).status()).toBe(404)
  expect((await intruder.request.get(`/api/orders/${order.id}`)).status()).toBe(404)

  await intruder.page.goto('/account/orders')
  await expect(intruder.page.getByText(order.orderNumber)).toHaveCount(0)
  await expect(intruder.page.getByText('You have not made any purchases yet.')).toBeVisible()
})

test('E2E-21 role-based access', async ({ loginAs }) => {
  const customer = await loginAs('customer')
  await customer.page.goto('/admin/products')
  await expect(customer.page.getByText('You do not have permission to access this area.')).toBeVisible()
  expect((await customer.request.get('/api/admin/orders')).status()).toBe(403)

  const admin = await loginAs('admin')
  const product = await findProduct(admin.request, 'E2E-ESPRESSO')
  await admin.page.goto('/checkout')
  await expect(admin.page.getByText('You do not have permission to access this area.')).toBeVisible()
  const response = await admin.request.post('/api/orders', {
    headers: { 'Idempotency-Key': crypto.randomUUID() },
    data: { shippingAddress, items: [{ productId: product.id, quantity: 1 }] },
  })
  expect(response.status()).toBe(403)
})

test('E2E-22 sign out requires login again at checkout', async ({ loginAs }) => {
  const { page } = await loginAs('customer')
  await buyAndReserve(page, 'E2E Espresso')
  await page.getByRole('button', { name: 'Sign out' }).click()
  await page.goto('/checkout')
  await expect(page).toHaveURL(/\/login\?returnUrl=/)
  await expect(page.getByRole('heading', { name: 'Log in' })).toBeVisible()
})

test('E2E-23 CSRF defense requires the custom header', async ({ playwright, baseURL, registerCustomer }) => {
  const customer = await registerCustomer()
  const bare = await playwright.request.newContext({ baseURL })
  const withoutHeader = await bare.post('/auth/login', {
    data: { email: customer.email, password: customer.password },
  })
  expect(withoutHeader.status()).toBe(403)
  await bare.dispose()

  const protectedContext = await playwright.request.newContext({ baseURL, extraHTTPHeaders: csrfHeaders })
  const withHeader = await protectedContext.post('/auth/login', {
    data: { email: customer.email, password: customer.password },
  })
  expect(withHeader.ok()).toBeTruthy()
  await protectedContext.dispose()
})
