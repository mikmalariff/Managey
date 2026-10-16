import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, type ContributionScheme, type Department, type LeaveBalance, type Membership, type PayDetails, type PayItem, type Role, type UserRow } from '../api'
import { useMe } from '../auth'
import { ErrorText, Field, Loading, PageHeader, useFetch } from '../components/ui'
import { currencyCode } from '../format'

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

      {!isNew && <PayDetailsForm userId={Number(id)} />}
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

type PayForm = Omit<PayDetails, 'basicSalary'> & { basicSalary: string }

function PayDetailsForm({ userId }: { userId: number }) {
  const { data } = useFetch<PayDetails>(`/api/users/${userId}/pay`)
  const { data: schemes } = useFetch<ContributionScheme[]>('/api/payroll/schemes')
  const [form, setForm] = useState<PayForm | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    if (data) setForm({ ...data, basicSalary: data.basicSalary?.toString() ?? '' })
  }, [data])
  if (!form || !schemes) return null

  const set = (patch: Partial<PayForm>) => { setForm({ ...form, ...patch }); setSaved(false) }
  const setItem = (i: number, patch: Partial<PayItem>) => set({ items: form.items.map((x, j) => (j === i ? { ...x, ...patch } : x)) })
  const membership = (schemeId: number) => form.contributions.find(c => c.schemeId === schemeId)
  const setMembership = (schemeId: number, patch: Partial<Membership> | null) => {
    const others = form.contributions.filter(c => c.schemeId !== schemeId)
    if (patch === null) return set({ contributions: others })
    const current = membership(schemeId) ?? { schemeId, memberNo: null, employeeAmount: null, employerAmount: null }
    set({ contributions: [...others, { ...current, ...patch }] })
  }
  const text = (key: 'icNumber' | 'passportNo' | 'bankName' | 'bankAccountNo' | 'taxNo') => ({
    value: form[key] ?? '',
    onChange: (e: React.ChangeEvent<HTMLInputElement>) => set({ [key]: e.target.value }),
  })
  const optionalNumber = (v: string) => (v === '' ? null : Number(v))

  const save = async () => {
    setError(null)
    try {
      await api.put(`/api/users/${userId}/pay`, { ...form, basicSalary: form.basicSalary === '' ? null : Number(form.basicSalary) })
      setSaved(true)
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const activeSchemes = schemes.filter(s => s.isActive || membership(s.id))

  return (
    <section className="form">
      <div>
        <h2>Pay details</h2>
        <p className="muted" style={{ marginTop: 4 }}>Visible to HR and admin only. Used to build monthly payslips.</p>
      </div>
      <div className="grid-2">
        <Field label={`Basic salary (${currencyCode()} / month)`} hint="Leave blank to leave this person out of payroll">
          <input type="number" min="0" step="0.01" value={form.basicSalary} onChange={e => set({ basicSalary: e.target.value })} />
        </Field>
        <Field label="Tax no."><input {...text('taxNo')} /></Field>
        <Field label="ID number" hint="National ID, e.g. MyKad or NRIC"><input {...text('icNumber')} /></Field>
        <Field label="Passport no." hint="If there is no national ID"><input {...text('passportNo')} /></Field>
        <Field label="Bank"><input {...text('bankName')} /></Field>
        <Field label="Account no."><input {...text('bankAccountNo')} /></Field>
      </div>

      <div className="stack" style={{ gap: 8 }}>
        <h3>Contributions</h3>
        {activeSchemes.length === 0 ? (
          <p className="faint">No contribution schemes yet. Add them under Setup → Payroll.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <tbody>
                {activeSchemes.map(s => {
                  const m = membership(s.id)
                  return (
                    <tr key={s.id}>
                      <td style={{ width: 32 }}>
                        <input type="checkbox" checked={!!m} onChange={e => setMembership(s.id, e.target.checked ? {} : null)} aria-label={s.name} />
                      </td>
                      <td>
                        <div>{s.name}</div>
                        <div className="faint mono">{s.code}{!s.isActive && ' · inactive'}</div>
                      </td>
                      <td style={{ width: 180 }}>
                        {m && <input placeholder="Member no." value={m.memberNo ?? ''} onChange={e => setMembership(s.id, { memberNo: e.target.value })} />}
                      </td>
                      <td style={{ width: 150 }}>
                        {m && s.method === 'PerEmployee' && (
                          <input type="number" min="0" step="0.01" placeholder={`Amount (${currencyCode()})`} value={m.employeeAmount ?? ''}
                            onChange={e => setMembership(s.id, { employeeAmount: optionalNumber(e.target.value) })} />
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="stack" style={{ gap: 8 }}>
        <div className="spread">
          <h3>Fixed monthly allowances & deductions</h3>
          <div className="row">
            <button className="link" onClick={() => set({ items: [...form.items, { kind: 'Earning', name: '', amount: 0, statutory: true }] })}>Add allowance</button>
            <button className="link" onClick={() => set({ items: [...form.items, { kind: 'Deduction', name: '', amount: 0, statutory: false }] })}>Add deduction</button>
          </div>
        </div>
        {form.items.length === 0 && <p className="faint">None.</p>}
        {form.items.map((item, i) => (
          <div className="row" key={i} style={{ flexWrap: 'nowrap' }}>
            <span className="muted" style={{ width: 80 }}>{item.kind === 'Earning' ? 'Allowance' : 'Deduction'}</span>
            <input value={item.name} placeholder={item.kind === 'Earning' ? 'e.g. Transport allowance' : 'e.g. Staff loan'} onChange={e => setItem(i, { name: e.target.value })} />
            <input type="number" min="0" step="0.01" value={item.amount} onChange={e => setItem(i, { amount: Number(e.target.value) })} style={{ width: 110 }} />
            <label className="check muted nowrap" title="Counts as wages for contributions">
              <input type="checkbox" checked={item.statutory} onChange={e => setItem(i, { statutory: e.target.checked })} /> Contributable
            </label>
            <button className="ghost icon" aria-label="Remove" onClick={() => set({ items: form.items.filter((_, j) => j !== i) })}>×</button>
          </div>
        ))}
      </div>

      <ErrorText error={error} />
      <div className="form-actions">
        <button onClick={save}>Save pay details</button>
        {saved && <span className="muted" style={{ alignSelf: 'center' }}>Saved</span>}
      </div>
    </section>
  )
}

