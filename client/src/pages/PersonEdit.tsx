import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, type Department, type LeaveBalance, type Role, type UserRow } from '../api'
import { useMe } from '../auth'
import { ErrorText, Field, Loading, PageHeader, useFetch } from '../components/ui'

const blank = {
  employeeNo: '', username: '', fullName: '', email: '', phone: '', role: 'Staff' as Role,
  departmentId: '', position: '', managerId: '', joinDate: '', isActive: true, password: '',
}

export default function PersonEdit() {
  const { id } = useParams()
  const isNew = !id
  const { isAdmin, isHR } = useMe()
  const navigate = useNavigate()
  const [form, setForm] = useState(blank)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const [busy, setBusy] = useState(false)

  const { data: user } = useFetch<UserRow>(isNew ? null : `/api/users/${id}`)
  const { data: people } = useFetch<UserRow[]>('/api/users')
  const { data: departments } = useFetch<Department[]>('/api/departments')

  useEffect(() => {
    if (!user) return
    setForm({
      employeeNo: user.employeeNo, username: user.username, fullName: user.fullName, email: user.email ?? '',
      phone: user.phone ?? '', role: user.role, departmentId: user.departmentId?.toString() ?? '',
      position: user.position ?? '', managerId: user.managerId?.toString() ?? '',
      joinDate: user.joinDate?.slice(0, 10) ?? '', isActive: user.isActive, password: '',
    })
  }, [user])

  if (!isHR) return <p className="error">You do not have access to this page.</p>
  if (!isNew && !user) return <Loading />

  const set = <K extends keyof typeof form>(key: K) => (value: (typeof form)[K]) => {
    setForm(f => ({ ...f, [key]: value }))
    setSaved(false)
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    const body = {
      ...form,
      departmentId: form.departmentId ? Number(form.departmentId) : null,
      managerId: form.managerId ? Number(form.managerId) : null,
      joinDate: form.joinDate || null,
      password: form.password || null,
    }
    try {
      if (isNew) {
        const created = await api.post<{ id: number }>('/api/users', body)
        navigate(`/people/${created.id}`, { replace: true })
      } else {
        await api.put(`/api/users/${id}`, body)
        setForm(f => ({ ...f, password: '' }))
        setSaved(true)
      }
    } catch (err) {
      setError((err as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="stack">
      <PageHeader title={isNew ? 'Add employee' : form.fullName} subtitle={isNew ? 'Create a profile and sign-in for a new employee' : form.position} />

      <form className="form" onSubmit={submit}>
        <h2>Profile</h2>
        <div className="grid-2">
          <Field label="Full name"><input value={form.fullName} onChange={e => set('fullName')(e.target.value)} required /></Field>
          <Field label="Employee no."><input value={form.employeeNo} onChange={e => set('employeeNo')(e.target.value)} required /></Field>
          <Field label="Email"><input type="email" value={form.email} onChange={e => set('email')(e.target.value)} /></Field>
          <Field label="Phone"><input value={form.phone} onChange={e => set('phone')(e.target.value)} /></Field>
          <Field label="Position"><input value={form.position} onChange={e => set('position')(e.target.value)} /></Field>
          <Field label="Join date"><input type="date" value={form.joinDate} onChange={e => set('joinDate')(e.target.value)} /></Field>
          <Field label="Department">
            <select value={form.departmentId} onChange={e => set('departmentId')(e.target.value)}>
              <option value="">—</option>
              {departments?.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
            </select>
          </Field>
          <Field label="Reports to" hint="This person approves their requests first">
            <select value={form.managerId} onChange={e => set('managerId')(e.target.value)}>
              <option value="">—</option>
              {people?.filter(p => String(p.id) !== id).map(p => <option key={p.id} value={p.id}>{p.fullName}</option>)}
            </select>
          </Field>
        </div>

        <h2 style={{ marginTop: 8 }}>Sign-in</h2>
        <div className="grid-2">
          <Field label="Username"><input value={form.username} onChange={e => set('username')(e.target.value)} required autoComplete="off" /></Field>
          <Field label={isNew ? 'Password' : 'New password'} hint={isNew ? 'At least 8 characters' : 'Leave blank to keep the current password'}>
            <input type="password" value={form.password} onChange={e => set('password')(e.target.value)} required={isNew} autoComplete="new-password" />
          </Field>
          <Field label="Role" hint="HR can manage people and leave. Admin can change everything.">
            <select value={form.role} onChange={e => set('role')(e.target.value as Role)}>
              <option value="Staff">Staff</option>
              <option value="Manager">Manager</option>
              <option value="HR">HR</option>
              {(isAdmin || form.role === 'Admin') && <option value="Admin">Admin</option>}
            </select>
          </Field>
          <Field label="Status">
            <label className="check" style={{ paddingTop: 6 }}>
              <input type="checkbox" checked={form.isActive} onChange={e => set('isActive')(e.target.checked)} /> Active (can sign in)
            </label>
          </Field>
        </div>

        <ErrorText error={error} />
        <div className="form-actions">
          <button className="primary" disabled={busy}>{isNew ? 'Create employee' : 'Save changes'}</button>
          <button type="button" onClick={() => navigate('/people')}>Back</button>
          {saved && <span className="muted" style={{ alignSelf: 'center' }}>Saved</span>}
        </div>
      </form>

      {!isNew && <Entitlements userId={Number(id)} />}
    </div>
  )
}

function Entitlements({ userId }: { userId: number }) {
  const year = new Date().getFullYear()
  const { data, reload } = useFetch<LeaveBalance[]>(`/api/users/${userId}/leave?year=${year}`)
  const [values, setValues] = useState<Record<number, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    if (data) setValues(Object.fromEntries(data.map(b => [b.leaveTypeId, String(b.entitlement)])))
  }, [data])

  const save = async () => {
    setError(null)
    try {
      await api.put(`/api/users/${userId}/entitlements/${year}`,
        Object.entries(values).map(([leaveTypeId, days]) => ({ leaveTypeId: Number(leaveTypeId), days: days === '' ? null : Number(days) })))
      await reload()
      setSaved(true)
    } catch (e) {
      setError((e as Error).message)
    }
  }

  if (!data) return null
  return (
    <section style={{ maxWidth: 640 }}>
      <h2>Leave entitlement {year}</h2>
      <p className="muted" style={{ margin: '4px 0 12px' }}>Clear a value to use the leave type's default.</p>
      <div className="table-wrap">
        <table>
          <thead><tr><th>Leave type</th><th style={{ width: 120 }}>Days</th><th className="right">Used</th><th className="right">Pending</th><th className="right">Remaining</th></tr></thead>
          <tbody>
            {data.map(b => (
              <tr key={b.leaveTypeId}>
                <td>{b.leaveType}</td>
                <td>
                  <input type="number" min="0" step="0.5" value={values[b.leaveTypeId] ?? ''}
                    onChange={e => { setValues(v => ({ ...v, [b.leaveTypeId]: e.target.value })); setSaved(false) }} />
                </td>
                <td className="right mono">{b.used}</td>
                <td className="right mono">{b.pending}</td>
                <td className="right mono">{b.remaining}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ErrorText error={error} />
      <div className="form-actions" style={{ marginTop: 12 }}>
        <button onClick={save}>Save entitlement</button>
        {saved && <span className="muted" style={{ alignSelf: 'center' }}>Saved</span>}
      </div>
    </section>
  )
}
