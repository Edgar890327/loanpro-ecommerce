import type { CatalogProduct } from './CatalogProduct'

export type CatalogPageData = {
  items: CatalogProduct[]
  page: number
  pageSize: number
  totalCount: number
}
