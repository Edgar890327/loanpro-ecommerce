import type { MyOrderSummary } from './MyOrderSummary'

export type MyOrdersPage = {
  items: MyOrderSummary[]
  page: number
  pageSize: number
  totalCount: number
}
