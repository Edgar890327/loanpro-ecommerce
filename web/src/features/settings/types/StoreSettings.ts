export type SettingSchema = {
  key: string
  name: string
  configurationFields: Array<{
    key: string
    label: string
    type: string
    required: boolean
    secret: boolean
    options: string[] | null
    minimum?: number | null
    maximum?: number | null
  }>
  secretFields: string[]
  publicConfiguration: Record<string, string>
  maskedSecrets: Record<string, string>
  isEnabled: boolean
  isActive: boolean
}

export type StoreSettings = {
  paymentProviders: SettingSchema[]
  shippingStrategies: SettingSchema[]
}
