import { Link, useNavigate } from 'react-router-dom'
import type { Holiday, LeaveBalance, RequestStatus, RequestType } from '../api'
import { useMe } from '../auth'
import { Empty, Loading, PageHeader, Status, useFetch } from '../components/ui'
import { fmt } from '../format'

interface DashboardData {
  attendance: { clockIn: string; clockOut: string | null } | null
  leaveBalances: LeaveBalance[]
  myPending: number
  toApprove: number
  recentRequests: { id: number; refNo: string; type: RequestType; title: string; status: RequestStatus; createdAt: string }[]
  onLeaveToday: { fullName: string; leaveType: string; endDate: string }[]
  company: { headcount: number; present: number; onLeave: number; pendingRequests: number } | null
}

function greeting() {
  const h = new Date().getHours()
  return h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening'
}

export default function Dashboard() {
  const { me } = useMe()
  const navigate = useNavigate()
  const { data, error } = useFetch<DashboardData>('/api/dashboard')
  const { data: holidays } = useFetch<Holiday[]>('/api/holidays')
  const today = fmt.isoDate(new Date())
  const nextHoliday = holidays?.find(h => h.date.slice(0, 10) >= today)

  if (error) return <p className="error">{error}</p>
  if (!data) return <Loading />

  const annual = data.leaveBalances.find(b => b.leaveType === 'Annual') ?? data.leaveBalances[0]

  return (
    <div className="stack">
      <PageHeader
        title={`${greeting()}, ${me.fullName.split(' ')[0]}`}
        subtitle={new Intl.DateTimeFormat('en-GB', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(new Date())}
        actions={<>
          <Link className="button" to="/requests/new">New request</Link>
          <Link className="button primary" to="/requests/new?type=Leave">Apply leave</Link>
        </>}
      />

      <div className="stats">
        <div className="stat">
          <div className="label">Today</div>
          <div className="value">{data.attendance ? fmt.time(data.attendance.clockIn) : '—'}</div>
          <div className="sub">
            {!data.attendance ? <Link to="/attendance">Not clocked in</Link>
              : data.attendance.clockOut ? `Out at ${fmt.time(data.attendance.clockOut)}` : 'Clocked in'}
          </div>
        </div>
        {annual && (
          <div className="stat">
            <div className="label">{annual.leaveType} leave left</div>
            <div className="value">{annual.remaining}</div>
            <div className="sub">of {annual.entitlement} days</div>
          </div>
        )}
        <div className="stat">
          <div className="label">My pending requests</div>
          <div className="value">{data.myPending}</div>
          <div className="sub"><Link to="/requests">View all</Link></div>
        </div>
        <div className={`stat ${data.toApprove > 0 ? 'highlight' : ''}`}>
          <div className="label">Waiting for my approval</div>
          <div className="value">{data.toApprove}</div>
          <div className="sub"><Link to="/approvals">Review</Link></div>
        </div>
      </div>

      {data.company && (
        <div className="stats">
          <div className="stat"><div className="label">Active staff</div><div className="value">{data.company.headcount}</div></div>
          <div className="stat"><div className="label">Clocked in today</div><div className="value">{data.company.present}</div></div>
          <div className="stat"><div className="label">On leave today</div><div className="value">{data.company.onLeave}</div></div>
          <div className="stat"><div className="label">Open requests</div><div className="value">{data.company.pendingRequests}</div></div>
        </div>
      )}

      <div className="grid-2">
        <section className="card">
          <div className="card-head">
            <h2>Recent requests</h2>
            <Link to="/requests" className="muted">All</Link>
          </div>
          {data.recentRequests.length === 0 ? <p className="muted">You have no requests yet.</p> : (
            <table>
              <tbody>
                {data.recentRequests.map(r => (
                  <tr key={r.id} className="clickable" onClick={() => navigate(`/requests/${r.id}`)}>
                    <td style={{ paddingLeft: 0 }}>
                      <div>{r.title}</div>
                      <div className="faint mono">{r.refNo}</div>
                    </td>
                    <td className="right" style={{ paddingRight: 0 }}><Status value={r.status} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>

        <section className="card">
          <h2>Out of office today</h2>
          {nextHoliday && (
            <p className="muted" style={{ marginBottom: 8 }}>Next public holiday: {nextHoliday.name}, {fmt.date(nextHoliday.date)}</p>
          )}
          {data.onLeaveToday.length === 0 ? <p className="muted">Everyone is in today.</p> : (
            <table>
              <tbody>
                {data.onLeaveToday.map((p, i) => (
                  <tr key={i}>
                    <td style={{ paddingLeft: 0 }}>{p.fullName}</td>
                    <td className="muted">{p.leaveType}</td>
                    <td className="right muted nowrap" style={{ paddingRight: 0 }}>until {fmt.date(p.endDate)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      </div>

      <section>
        <div className="card-head">
          <h2>Leave balance {new Date().getFullYear()}</h2>
          <Link to="/leave" className="muted">Details</Link>
        </div>
        {data.leaveBalances.length === 0 ? <Empty>No leave types configured.</Empty> : (
          <div className="stats">
            {data.leaveBalances.map(b => (
              <div className="stat" key={b.leaveTypeId}>
                <div className="label">{b.leaveType}</div>
                <div className="value">{b.remaining}</div>
                <div className="sub">{b.used} used{b.pending > 0 && ` · ${b.pending} pending`}</div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  )
}
