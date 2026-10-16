import { useEffect, useState } from 'react'
import { api, type AttendanceRow, type UserRow } from '../api'
import { useMe } from '../auth'
import { Empty, ErrorText, Field, Loading, PageHeader, useFetch } from '../components/ui'
import { fmt } from '../format'

function daysAgo(n: number) {
  const d = new Date()
  d.setDate(d.getDate() - n)
  return fmt.isoDate(d)
}

export default function Attendance({ team = false }: { team?: boolean }) {
  const { me, isHR } = useMe()
  const [from, setFrom] = useState(daysAgo(team ? 0 : 30))
  const [to, setTo] = useState(daysAgo(0))
  const [userId, setUserId] = useState('')

  const params = new URLSearchParams({ from, to })
  if (team && userId) params.set('userId', userId)
  else if (team) params.set('everyone', 'true')
  const { data: rows, error, reload } = useFetch<AttendanceRow[]>(`/api/attendance?${params}`)
  const { data: people } = useFetch<UserRow[]>(team ? '/api/users' : null)
  const visiblePeople = isHR ? people : people?.filter(p => p.managerId === me.id || p.id === me.id)

  return (
    <div className="stack">
      <PageHeader
        title={team ? (isHR ? 'Attendance report' : 'Team attendance') : 'Attendance'}
        subtitle={team ? 'Clock-in records for your people' : 'Clock in when you start, clock out when you leave'}
        actions={team && <a className="button" href={`/api/attendance/export?${params}`}>Export CSV</a>}
      />

      {!team && <ClockCard onChange={reload} />}

      <section>
        <div className="filters">
          <Field label="From"><input type="date" value={from} max={to} onChange={e => setFrom(e.target.value)} /></Field>
          <Field label="To"><input type="date" value={to} min={from} onChange={e => setTo(e.target.value)} /></Field>
          {team && (
            <Field label="Person">
              <select value={userId} onChange={e => setUserId(e.target.value)}>
                <option value="">Everyone</option>
                {visiblePeople?.map(p => <option key={p.id} value={p.id}>{p.fullName}</option>)}
              </select>
            </Field>
          )}
        </div>

        <ErrorText error={error} />
        {!rows ? <Loading /> : rows.length === 0 ? <Empty>No attendance records in this period.</Empty> : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  {team && <th>Name</th>}
                  <th>In</th>
                  <th>Out</th>
                  <th className="right">Hours</th>
                  <th></th>
                  <th>Note</th>
                </tr>
              </thead>
              <tbody>
                {rows.map(r => (
                  <tr key={r.id}>
                    <td className="nowrap">{fmt.date(r.date)}</td>
                    {team && <td>{r.name} <span className="faint mono">{r.employeeNo}</span></td>}
                    <td className="mono">{fmt.time(r.clockIn)}</td>
                    <td className="mono">{fmt.time(r.clockOut)}</td>
                    <td className="right mono">{r.hours?.toFixed(2) ?? '—'}</td>
                    <td>{r.late && <span className="muted">Late</span>}</td>
                    <td className="muted">{r.note}</td>
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

function ClockCard({ onChange }: { onChange: () => void }) {
  const { data: today, loading, reload } = useFetch<AttendanceRow | null>('/api/attendance/today')
  const [now, setNow] = useState(new Date())
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    const t = setInterval(() => setNow(new Date()), 1000)
    return () => clearInterval(t)
  }, [])

  const clock = async (action: 'clock-in' | 'clock-out') => {
    setBusy(true)
    setError(null)
    try {
      await api.post(`/api/attendance/${action}`, { note })
      setNote('')
      await reload()
      onChange()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card">
      <div className="clock">
        <div>
          <div className="now">{now.toLocaleTimeString('en-GB')}</div>
          <p className="muted">
            {loading ? '…'
              : !today ? 'You have not clocked in today.'
              : today.clockOut ? `Done for today: ${fmt.time(today.clockIn)} to ${fmt.time(today.clockOut)} (${today.hours?.toFixed(2)} h)`
              : `Clocked in at ${fmt.time(today.clockIn)}${today.late ? ' (late)' : ''}`}
          </p>
        </div>
        {!loading && !today?.clockOut && (
          <div className="row">
            <input placeholder="Note (optional)" value={note} onChange={e => setNote(e.target.value)} style={{ width: 220 }} />
            <button className="primary large" disabled={busy} onClick={() => clock(today ? 'clock-out' : 'clock-in')}>
              {today ? 'Clock out' : 'Clock in'}
            </button>
          </div>
        )}
      </div>
      <ErrorText error={error} />
    </section>
  )
}
