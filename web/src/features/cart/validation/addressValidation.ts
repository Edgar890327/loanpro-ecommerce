export type ShippingAddressValues = {
  recipientName: string
  line1: string
  line2: string
  city: string
  region: string
  postalCode: string
  country: string
}

export type ShippingAddressErrors = Partial<Record<keyof ShippingAddressValues, string>>

const maxLengths: Record<keyof ShippingAddressValues, number> = {
  recipientName: 200,
  line1: 200,
  line2: 200,
  city: 100,
  region: 100,
  postalCode: 20,
  country: 100,
}

export function validateShippingAddress(
  values: ShippingAddressValues,
  messages: { required: string, tooLong: string },
): ShippingAddressErrors {
  const errors: ShippingAddressErrors = {}
  for (const key of Object.keys(maxLengths) as Array<keyof ShippingAddressValues>) {
    const value = values[key].trim()
    if (!value && key !== 'line2') errors[key] = messages.required
    else if (value.length > maxLengths[key]) errors[key] = messages.tooLong
  }
  return errors
}
