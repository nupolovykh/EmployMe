import { useEffect, useState, type FormEvent } from 'react'
import { fetchVacancies } from './api'
import type { Vacancy, VacancyFilters } from './types'
import './App.css'

function formatSalary(v: Vacancy): string | null {
  if (v.salaryMin == null && v.salaryMax == null) return null

  const currency = v.currency ?? ''
  if (v.salaryMin != null && v.salaryMax != null) {
    return `${v.salaryMin.toLocaleString()}–${v.salaryMax.toLocaleString()} ${currency}`.trim()
  }

  const value = (v.salaryMin ?? v.salaryMax)!.toLocaleString()
  const prefix = v.salaryMin != null ? 'from' : 'up to'
  return `${prefix} ${value} ${currency}`.trim()
}

function App() {
  const [filters, setFilters] = useState<VacancyFilters>({})
  const [pendingFilters, setPendingFilters] = useState<VacancyFilters>({})
  const [vacancies, setVacancies] = useState<Vacancy[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)

    fetchVacancies(filters)
      .then((data) => {
        if (!cancelled) setVacancies(data)
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Failed to load vacancies')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [filters])

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFilters(pendingFilters)
  }

  return (
    <main>
      <h1>EmployMe</h1>

      <form className="filters" onSubmit={handleSubmit}>
        <input
          type="text"
          placeholder="Keyword (title, company, description)"
          value={pendingFilters.keyword ?? ''}
          onChange={(e) => setPendingFilters((f) => ({ ...f, keyword: e.target.value }))}
        />
        <input
          type="text"
          placeholder="Location"
          value={pendingFilters.location ?? ''}
          onChange={(e) => setPendingFilters((f) => ({ ...f, location: e.target.value }))}
        />
        <label className="date-filter">
          Published after
          <input
            type="date"
            value={pendingFilters.publishedAfter ?? ''}
            onChange={(e) => setPendingFilters((f) => ({ ...f, publishedAfter: e.target.value }))}
          />
        </label>
        <button type="submit">Search</button>
      </form>

      {loading && <p className="status">Loading…</p>}
      {error && <p className="status error">{error}</p>}
      {!loading && !error && vacancies.length === 0 && (
        <p className="status">No vacancies yet — run an ingest to populate the database.</p>
      )}

      <ul className="vacancy-list">
        {vacancies.map((v) => {
          const salary = formatSalary(v)
          return (
            <li key={v.id} className="vacancy-card">
              <a href={v.url} target="_blank" rel="noreferrer">
                <h2>{v.title}</h2>
              </a>
              <p className="meta">
                {v.company && <span>{v.company}</span>}
                {v.location && <span>{v.location}</span>}
                {v.workFormat && <span>{v.workFormat}</span>}
                {salary && <span>{salary}</span>}
              </p>
              <p className="source">
                {/* EM-54: Tier B terms make the credit and the link back a
                    condition of display, not a nicety. */}
                via{' '}
                {v.attributionRequired && v.sourceUrl ? (
                  <a href={v.sourceUrl} target="_blank" rel="noreferrer">
                    {v.sourceName}
                  </a>
                ) : (
                  v.sourceName
                )}
                {' · '}
                <a href={v.url} target="_blank" rel="noreferrer">
                  Apply on the original posting
                </a>
              </p>
            </li>
          )
        })}
      </ul>
    </main>
  )
}

export default App
