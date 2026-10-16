export type OrderHistoryItem = {
  id: string
  state: string
  createdAtUtc: string
  subtotal: number
  shipping: number
  total: number
  paymentProviderKey: string | null
}

export type OrderHistoryPageData = {
  items: OrderHistoryItem[]
  page: number
  pageSize: number
  totalCount: number
}
