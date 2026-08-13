import type { Vacancy, VacancyFilters } from './types'

const API_BASE = import.meta.env.VITE_API_URL ?? ''

export async function fetchVacancies(filters: VacancyFilters): Promise<Vacancy[]> {
  const params = new URLSearchParams()
  if (filters.keyword) params.set('keyword', filters.keyword)
  if (filters.location) params.set('location', filters.location)
  if (filters.publishedAfter) params.set('publishedAfter', filters.publishedAfter)
  if (filters.publishedBefore) params.set('publishedBefore', filters.publishedBefore)

  const response = await fetch(`${API_BASE}/api/vacancies?${params.toString()}`)
  if (!response.ok) {
    throw new Error(`Failed to load vacancies: ${response.status}`)
  }
  return response.json() as Promise<Vacancy[]>
}
