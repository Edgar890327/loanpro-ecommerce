export type PaymentProvider = {
  key: string
  name: string
  configurationFields: Array<{
    key: string
    label: string
    type: string
    required: boolean
    secret: boolean
    options: string[] | null
  }>
  secretFields: string[]
  publicConfiguration: Record<string, string>
}

export type PaymentCreation = {
  orderId: string
  providerKey: string
  externalReference: string
  provider: PaymentProvider
}

export type PaymentCapture = {
  orderId: string
  orderState: string
  paymentStatus: string
}
