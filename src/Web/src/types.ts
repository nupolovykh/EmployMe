/** Mirrors the API's Seniority enum. `unknown` is the default and is never a match. */
export type Seniority = 'Unknown' | 'Intern' | 'Junior' | 'Mid' | 'Senior' | 'Lead'

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
  seniority: Seniority
  sourceName: string
  sourceSlug: string
  sourceUrl: string | null
  attributionRequired: boolean
}

export interface VacancyFilters {
  keyword?: string
  location?: string
  publishedAfter?: string
  publishedBefore?: string
  seniority?: Seniority
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
