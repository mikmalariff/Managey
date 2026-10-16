import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import type { RequestRow } from '../api'
import { Empty, ErrorText, Field, Loading, PageHeader, Status, useFetch } from '../components/ui'
import { fmt } from '../format'

export function RequestTable({ rows, showRequester }: { rows: RequestRow[]; showRequester?: boolean }) {
  const navigate = useNavigate()
  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Ref</th>
            {showRequester && <th>Requester</th>}
            <th>Request</th>
            <th>Details</th>
            <th>Submitted</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(r => (
            <tr key={r.id} className="clickable" onClick={() => navigate(`/requests/${r.id}`)}>
              <td className="mono nowrap">{r.refNo}</td>
              {showRequester && <td>{r.requester}</td>}
              <td>
                <div>{r.title}</div>
                <div className="faint">{r.type}</div>
              </td>
              <td className="muted nowrap">
                {r.type === 'Leave' ? `${fmt.date(r.startDate)} · ${fmt.days(r.days)}` : r.amount != null ? fmt.money(r.amount) : ''}
              </td>
              <td className="muted nowrap">{fmt.date(r.createdAt)}</td>
              <td>
                <Status value={r.status} />
                {r.status === 'Pending' && r.currentStep && <div className="faint" style={{ fontSize: 12 }}>with {r.currentStep}</div>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

export default function Requests({ all = false }: { all?: boolean }) {
  const [status, setStatus] = useState('')
  const [type, setType] = useState('')

  const params = new URLSearchParams({ scope: all ? 'all' : 'mine' })
  if (status) params.set('status', status)
  if (type) params.set('type', type)
  const { data, error } = useFetch<RequestRow[]>(`/api/requests?${params}`)

  return (
    <div className="stack">
      <PageHeader
        title={all ? 'All requests' : 'My requests'}
        subtitle={all ? 'Every request submitted in the company' : 'Leave, claims, purchases and other requests you have made'}
        actions={<Link className="button primary" to="/requests/new">New request</Link>}
      />
      <section>
        <div className="filters">
          <Field label="Type">
            <select value={type} onChange={e => setType(e.target.value)}>
              <option value="">All types</option>
              <option>Leave</option><option>Claim</option><option>Purchase</option><option>General</option>
            </select>
          </Field>
          <Field label="Status">
            <select value={status} onChange={e => setStatus(e.target.value)}>
              <option value="">All statuses</option>
              <option>Pending</option><option>Approved</option><option>Rejected</option><option>Cancelled</option>
            </select>
          </Field>
        </div>
        <ErrorText error={error} />
        {!data ? <Loading /> : data.length === 0 ? <Empty>No requests found.</Empty> : <RequestTable rows={data} showRequester={all} />}
      </section>
    </div>
  )
}
