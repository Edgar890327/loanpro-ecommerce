export type ErrorLog = {
  id: string
  timestampUtc: string
  level: string
  correlationId: string
  message: string
  exceptionType: string
  stackTrace: string | null
  httpMethod: string
  path: string
  statusCode: number
  orderId: string | null
  paymentProviderKey: string | null
}

export type ErrorLogPageData = {
  items: ErrorLog[]
  page: number
  pageSize: number
  totalCount: number
}
