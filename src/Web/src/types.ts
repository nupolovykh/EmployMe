export interface Vacancy {
  id: number
  externalId: string
  title: string
  company: string | null
  url: string
  location: string | null
  workFormat: string | null
  salaryMin: number | null
  salaryMax: number | null
  currency: string | null
  publishedAt: string | null
  fetchedAt: string
  sourceName: string
}

export interface VacancyFilters {
  keyword?: string
  location?: string
  publishedAfter?: string
  publishedBefore?: string
  page?: number
}

/** Mirrors the API's PagedResult envelope: without `total` the UI cannot tell
 *  a short last page from a full one, so it cannot offer a next page. */
export interface Paged<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}
