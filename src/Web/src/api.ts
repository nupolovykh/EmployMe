import type { Paged, Vacancy, VacancyFilters } from './types'

export async function fetchVacancies(filters: VacancyFilters): Promise<Paged<Vacancy>> {
  const params = new URLSearchParams()
  if (filters.keyword) params.set('keyword', filters.keyword)
  if (filters.location) params.set('location', filters.location)
  if (filters.publishedAfter) params.set('publishedAfter', filters.publishedAfter)
  if (filters.publishedBefore) params.set('publishedBefore', filters.publishedBefore)
  if (filters.page && filters.page > 1) params.set('page', String(filters.page))

  const response = await fetch(`/api/vacancies?${params.toString()}`)
  if (!response.ok) {
    throw new Error(`Failed to load vacancies: ${response.status}`)
  }
  return response.json() as Promise<Paged<Vacancy>>
}
