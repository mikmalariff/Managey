import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import type { UserRow } from '../api'
import { useMe } from '../auth'
import { Empty, ErrorText, Loading, PageHeader, Status, useFetch } from '../components/ui'

export default function People() {
  const { isHR } = useMe()
  const navigate = useNavigate()
  const [q, setQ] = useState('')
  const [showInactive, setShowInactive] = useState(false)
  const { data, error } = useFetch<UserRow[]>(`/api/users?includeInactive=${showInactive}`)

  const term = q.trim().toLowerCase()
  const rows = data?.filter(u =>
    !term || [u.fullName, u.employeeNo, u.department, u.position, u.email].some(v => v?.toLowerCase().includes(term)))

  return (
    <div className="stack">
      <PageHeader
        title="People"
        subtitle={data && `${data.filter(u => u.isActive).length} active employees`}
        actions={isHR && <Link className="button primary" to="/people/new">Add employee</Link>}
      />
      <section>
        <div className="filters">
          <input placeholder="Search name, department, position…" value={q} onChange={e => setQ(e.target.value)} style={{ maxWidth: 320 }} />
          {isHR && (
            <label className="check muted">
              <input type="checkbox" checked={showInactive} onChange={e => setShowInactive(e.target.checked)} /> Show inactive
            </label>
          )}
        </div>
        <ErrorText error={error} />
        {!rows ? <Loading /> : rows.length === 0 ? <Empty>No one matches your search.</Empty> : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr><th>Name</th><th>Position</th><th>Department</th><th>Reports to</th><th>Contact</th>{isHR && <th>Role</th>}</tr>
              </thead>
              <tbody>
                {rows.map(u => (
                  <tr key={u.id} className={isHR ? 'clickable' : ''} onClick={() => isHR && navigate(`/people/${u.id}`)}>
                    <td>
                      <div>{u.fullName}</div>
                      <div className="faint mono">{u.employeeNo}</div>
                    </td>
                    <td>{u.position ?? <span className="faint">—</span>}</td>
                    <td>{u.department ?? <span className="faint">—</span>}</td>
                    <td className="muted">{u.manager ?? '—'}</td>
                    <td className="muted">
                      {u.email && <div>{u.email}</div>}
                      {u.phone && <div>{u.phone}</div>}
                    </td>
                    {isHR && <td>{u.isActive ? u.role : <Status value="Inactive" />}</td>}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}
