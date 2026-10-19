import { mkdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import {
  expect,
  test as base,
  type APIRequestContext,
  type BrowserContext,
  type Page,
} from '@playwright/test'

export const csrfHeaders = { 'X-Requested-With': 'store' }
export const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@example.com'
export const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'e2e-admin-only-local'
export const customerPassword = 'e2e-customer-password'

const authDirectory = fileURLToPath(new URL('../.auth/', import.meta.url))

export type Role = 'admin' | 'customer'

export type Customer = {
  email: string
  password: string
  fullName: string
}

export type Session = {
  role: Role
  page: Page
  context: BrowserContext
  request: APIRequestContext
  customer?: Customer
}

type Fixtures = {
  resetData: void
  registerCustomer: (overrides?: Partial<Customer>) => Promise<Customer>
  loginAs: (role: Role, customer?: Customer) => Promise<Session>
}

export const test = base.extend<Fixtures>({
  resetData: [async ({ request }, use) => {
    const response = await request.post('/e2e/reset', { headers: csrfHeaders })
    expect(response.status(), 'E2E reset endpoint must be enabled for the E2E profile').toBe(204)
    await use()
  }, { auto: true }],

  registerCustomer: async ({ playwright, baseURL }, use) => {
    await use(async (overrides = {}) => {
      const customer: Customer = {
        email: `customer-${crypto.randomUUID()}@example.test`,
        password: customerPassword,
        fullName: 'E2E Customer',
        ...overrides,
      }
      const api = await playwright.request.newContext({ baseURL, extraHTTPHeaders: csrfHeaders })
      const response = await api.post('/auth/register', { data: customer })
      expect(response.status(), await response.text()).toBe(201)
      await api.dispose()
      return customer
    })
  },

  loginAs: async ({ playwright, browser, baseURL, registerCustomer }, use) => {
    const apis: APIRequestContext[] = []
    const contexts: BrowserContext[] = []
    let sequence = 0
    mkdirSync(authDirectory, { recursive: true })

    await use(async (role, existing) => {
      const customer = role === 'customer' ? existing ?? await registerCustomer() : undefined
      const credentials = customer ?? { email: adminEmail, password: adminPassword }
      const api = await playwright.request.newContext({ baseURL, extraHTTPHeaders: csrfHeaders })
      apis.push(api)
      const response = await api.post('/auth/login', {
        data: { email: credentials.email, password: credentials.password },
      })
      expect(response.ok(), await response.text()).toBeTruthy()
      sequence += 1
      const storageState = `${authDirectory}${role}-${process.pid}-${sequence}.json`
      await api.storageState({ path: storageState })
      const context = await browser.newContext({ baseURL, storageState })
      contexts.push(context)
      return { role, page: await context.newPage(), context, request: api, customer }
    })

    await Promise.all(contexts.map((context) => context.close()))
    await Promise.all(apis.map((api) => api.dispose()))
  },
})

export { expect }
