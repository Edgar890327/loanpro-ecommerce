import type { Product } from './Product'

export type ProductPageData = {
  items: Product[]
  page: number
  pageSize: number
  totalCount: number
}
