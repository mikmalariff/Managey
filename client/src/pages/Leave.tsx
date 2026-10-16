import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import type { LeaveBalance, RequestRow } from '../api'
import { useMe } from '../auth'
import { Empty, Loading, PageHeader, Status, useFetch } from '../components/ui'
import { fmt } from '../format'

export default function Leave() {
  const { me } = useMe()
  const navigate = useNavigate()
  const thisYear = new Date().getFullYear()
  const [year, setYear] = useState(thisYear)
  const { data: balances } = useFetch<LeaveBalance[]>(`/api/users/${me.id}/leave?year=${year}`)
  const { data: requests } = useFetch<RequestRow[]>('/api/requests?type=Leave')
  const yearRequests = requests?.filter(r => r.startDate?.startsWith(String(year)))

  return (
    <div className="stack">
      <PageHeader
        title="Leave"
        subtitle="Your entitlement, what you've used, and your applications"
        actions={<>
          <select value={year} onChange={e => setYear(Number(e.target.value))} style={{ width: 'auto' }}>
            {[thisYear + 1, thisYear, thisYear - 1, thisYear - 2].map(y => <option key={y}>{y}</option>)}
          </select>
          <Link className="button primary" to="/requests/new?type=Leave">Apply leave</Link>
        </>}
      />

      {!balances ? <Loading /> : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Leave type</th><th className="right">Entitlement</th><th className="right">Used</th><th className="right">Pending</th><th className="right">Remaining</th></tr>
            </thead>
            <tbody>
              {balances.map(b => (
                <tr key={b.leaveTypeId}>
                  <td>{b.leaveType}</td>
                  <td className="right mono">{b.entitlement}</td>
                  <td className="right mono">{b.used}</td>
                  <td className="right mono">{b.pending}</td>
                  <td className="right mono"><strong>{b.remaining}</strong></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <section>
        <h2 style={{ marginBottom: 12 }}>Applications in {year}</h2>
        {!yearRequests ? <Loading /> : yearRequests.length === 0 ? <Empty>No leave applications for {year}.</Empty> : (
          <div className="table-wrap">
            <table>
              <thead><tr><th>Ref</th><th>Type</th><th>Dates</th><th className="right">Days</th><th>Status</th></tr></thead>
              <tbody>
                {yearRequests.map(r => (
                  <tr key={r.id} className="clickable" onClick={() => navigate(`/requests/${r.id}`)}>
                    <td className="mono">{r.refNo}</td>
                    <td>{r.leaveType}</td>
                    <td className="nowrap">{fmt.date(r.startDate)}{r.endDate !== r.startDate && ` – ${fmt.date(r.endDate)}`}</td>
                    <td className="right mono">{r.days}</td>
                    <td><Status value={r.status} /></td>
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
