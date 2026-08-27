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
  sourceSlug: string
  sourceUrl: string | null
  attributionRequired: boolean
}

export interface VacancyFilters {
  keyword?: string
  location?: string
  publishedAfter?: string
  publishedBefore?: string
}
