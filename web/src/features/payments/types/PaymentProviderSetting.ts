export type PaymentProviderSetting = {
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
  isActive: boolean
}
