export type ProductFormValues = {
  name: string
  sku: string
  description?: string
  category: string
  price: number
  stock: number
  weightKg: number | null
}

export type ProductFormErrors = Partial<
  Record<'name' | 'sku' | 'category' | 'price' | 'stock' | 'weightKg', string>
>

export function validateProductForm(
  values: ProductFormValues,
  messages: {
    required: string
    positivePrice: string
    nonNegative: string
    invalidNumber: string
    tooLong: string
  },
): ProductFormErrors {
  const errors: ProductFormErrors = {}
  if (!values.name.trim()) errors.name = messages.required
  else if (values.name.trim().length > 200) errors.name = messages.tooLong
  if (!values.sku.trim()) errors.sku = messages.required
  else if (values.sku.trim().length > 64) errors.sku = messages.tooLong
  if (!values.category.trim()) errors.category = messages.required
  else if (values.category.trim().length > 100) errors.category = messages.tooLong
  if (!Number.isFinite(values.price)) errors.price = messages.invalidNumber
  else if (values.price <= 0) errors.price = messages.positivePrice
  else if (values.price > 9_999_999_999.99) errors.price = messages.invalidNumber
  else if (!hasPrecision(values.price, 2)) errors.price = messages.invalidNumber
  if (!Number.isInteger(values.stock)) errors.stock = messages.invalidNumber
  else if (values.stock < 0) errors.stock = messages.nonNegative
  if (values.weightKg !== null) {
    if (!Number.isFinite(values.weightKg)) errors.weightKg = messages.invalidNumber
    else if (values.weightKg < 0) errors.weightKg = messages.nonNegative
    else if (values.weightKg > 999_999_999.999) errors.weightKg = messages.invalidNumber
    else if (!hasPrecision(values.weightKg, 3)) errors.weightKg = messages.invalidNumber
  }
  return errors
}

function hasPrecision(value: number, decimalPlaces: number) {
  const scaled = value * 10 ** decimalPlaces
  return Math.abs(scaled - Math.round(scaled)) <= Number.EPSILON * Math.max(1, Math.abs(scaled)) * 2
}
