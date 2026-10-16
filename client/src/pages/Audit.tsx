import { useState } from 'react'
import type { AuditRow } from '../api'
import { Empty, ErrorText, Loading, PageHeader, useFetch } from '../components/ui'
import { fmt } from '../format'

export default function Audit() {
  const [page, setPage] = useState(1)
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const { data, error } = useFetch<{ total: number; pageSize: number; items: AuditRow[] }>(
    `/api/audit?page=${page}&q=${encodeURIComponent(search)}`)
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1

  return (
    <div className="stack">
      <PageHeader title="Audit log" subtitle="Who did what, and when" />
      <section>
        <form className="filters" onSubmit={e => { e.preventDefault(); setPage(1); setSearch(q) }}>
          <input placeholder="Search user, action, details…" value={q} onChange={e => setQ(e.target.value)} style={{ maxWidth: 320 }} />
          <button>Search</button>
        </form>
        <ErrorText error={error} />
        {!data ? <Loading /> : data.items.length === 0 ? <Empty>No entries.</Empty> : (
          <>
            <div className="table-wrap">
              <table>
                <thead><tr><th>Time</th><th>User</th><th>Action</th><th>Item</th><th>Details</th></tr></thead>
                <tbody>
                  {data.items.map(a => (
                    <tr key={a.id}>
                      <td className="muted nowrap">{fmt.dateTime(a.timestamp)}</td>
                      <td>{a.username}</td>
                      <td>{a.action}</td>
                      <td className="muted">{a.entityType}{a.entityId && <span className="mono"> #{a.entityId}</span>}</td>
                      <td className="muted">{a.details}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="spread" style={{ marginTop: 12 }}>
              <span className="muted">{data.total} entries</span>
              <div className="row">
                <button disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Previous</button>
                <span className="muted">{page} / {pages}</span>
                <button disabled={page >= pages} onClick={() => setPage(p => p + 1)}>Next</button>
              </div>
            </div>
          </>
        )}
      </section>
    </div>
  )
}
