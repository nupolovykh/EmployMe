import { useEffect, useState, type FormEvent } from 'react'
import { fetchVacancies } from './api'
import type { Paged, Seniority, Vacancy, VacancyFilters } from './types'
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
  const [page, setPage] = useState(1)
  const [result, setResult] = useState<Paged<Vacancy> | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)

    fetchVacancies({ ...filters, page })
      .then((data) => {
        if (!cancelled) setResult(data)
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
  }, [filters, page])

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    // A new filter invalidates the current position: page 3 of the old result
    // is very unlikely to exist in the new one.
    setPage(1)
    setFilters(pendingFilters)
  }

  const vacancies = result?.items ?? []
  const total = result?.total ?? 0
  const pageSize = result?.pageSize ?? 0
  const lastPage = pageSize > 0 ? Math.ceil(total / pageSize) : 1

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
        <select
          value={pendingFilters.seniority ?? ''}
          onChange={(e) =>
            setPendingFilters((f) => ({
              ...f,
              seniority: (e.target.value || undefined) as Seniority | undefined,
            }))
          }
        >
          {/* Unknown is offered explicitly rather than folded into "any level".
              Most rows are Unknown — Greenhouse and Lever state no level at all —
              so hiding it would hide the majority of the database. */}
          <option value="">Any level</option>
          <option value="Intern">Intern</option>
          <option value="Junior">Junior</option>
          <option value="Mid">Mid</option>
          <option value="Senior">Senior</option>
          <option value="Lead">Lead</option>
          <option value="Unknown">Level not stated</option>
        </select>
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

      {!loading && !error && total > 0 && (
        <p className="status">
          {`${(page - 1) * pageSize + 1}–${(page - 1) * pageSize + vacancies.length} of ${total}`}
        </p>
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
                {v.seniority !== 'Unknown' && <span>{v.seniority}</span>}
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

      {!loading && !error && lastPage > 1 && (
        <nav className="pagination">
          <button type="button" onClick={() => setPage((p) => p - 1)} disabled={page <= 1}>
            Previous
          </button>
          <span>{`Page ${page} of ${lastPage}`}</span>
          <button type="button" onClick={() => setPage((p) => p + 1)} disabled={page >= lastPage}>
            Next
          </button>
        </nav>
      )}
    </main>
  )
}

export default App
