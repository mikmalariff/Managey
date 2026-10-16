import { useEffect, useState } from 'react'
import { api, type ApproverKind, type Department, type Holiday, type LeaveType, type RequestType, type UserRow } from '../api'
import { useMe } from '../auth'
import { ErrorText, Field, Loading, Modal, PageHeader, Status, Tabs, useFetch } from '../components/ui'
import { approverLabel, fmt } from '../format'

type Section = 'departments' | 'leave' | 'holidays' | 'workflows' | 'email' | 'company'

export default function Setup() {
  const { isAdmin } = useMe()
  const [section, setSection] = useState<Section>('departments')
  const options: { value: Section; label: string }[] = [
    { value: 'departments', label: 'Departments' },
    { value: 'leave', label: 'Leave types' },
    { value: 'holidays', label: 'Public holidays' },
    ...(isAdmin ? [
      { value: 'workflows' as Section, label: 'Approval flows' },
      { value: 'email' as Section, label: 'Email' },
      { value: 'company' as Section, label: 'Company' },
    ] : []),
  ]

  return (
    <div className="stack">
      <PageHeader title="Setup" subtitle="Company structure, leave policy and approval rules" />
      <div>
        <Tabs value={section} options={options} onChange={setSection} />
        {section === 'departments' && <Departments />}
        {section === 'leave' && <LeaveTypes />}
        {section === 'holidays' && <Holidays />}
        {section === 'workflows' && <Workflows />}
        {section === 'email' && <EmailSettings />}
        {section === 'company' && <Company />}
      </div>
    </div>
  )
}

function Departments() {
  const { data, reload } = useFetch<Department[]>('/api/departments')
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)

  const run = async (action: () => Promise<unknown>) => {
    setError(null)
    try {
      await action()
      reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const add = () => run(async () => { await api.post('/api/departments', { name }); setName('') })
  const rename = (d: Department) => {
    const next = prompt('Rename department', d.name)
    if (next && next !== d.name) run(() => api.put(`/api/departments/${d.id}`, { name: next }))
  }
  const remove = (d: Department) => confirm(`Delete ${d.name}?`) && run(() => api.del(`/api/departments/${d.id}`))

  return (
    <div className="stack" style={{ maxWidth: 640 }}>
      <form className="row" onSubmit={e => { e.preventDefault(); add() }}>
        <input placeholder="New department name" value={name} onChange={e => setName(e.target.value)} style={{ flex: 1 }} required />
        <button className="primary">Add</button>
      </form>
      <ErrorText error={error} />
      {!data ? <Loading /> : (
        <div className="table-wrap">
          <table>
            <thead><tr><th>Department</th><th className="right">People</th><th></th></tr></thead>
            <tbody>
              {data.length === 0 && <tr><td colSpan={3} className="muted">No departments yet.</td></tr>}
              {data.map(d => (
                <tr key={d.id}>
                  <td>{d.name}</td>
                  <td className="right mono">{d.headcount}</td>
                  <td className="right nowrap">
                    <button className="link" onClick={() => rename(d)}>Rename</button>{'  '}
                    <button className="link" onClick={() => remove(d)}>Delete</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function LeaveTypes() {
  const { data, reload } = useFetch<LeaveType[]>('/api/leave-types')
  const [editing, setEditing] = useState<Partial<LeaveType> | null>(null)

  return (
    <div className="stack" style={{ maxWidth: 720 }}>
      <div className="spread">
        <p className="muted">Default days apply to everyone each year. Override per person on their profile.</p>
        <button className="primary" onClick={() => setEditing({ name: '', defaultDays: 0, requiresAttachment: false, isActive: true })}>Add leave type</button>
      </div>
      {!data ? <Loading /> : (
        <div className="table-wrap">
          <table>
            <thead><tr><th>Leave type</th><th className="right">Days / year</th><th>Document</th><th>Status</th><th></th></tr></thead>
            <tbody>
              {data.map(t => (
                <tr key={t.id}>
                  <td>{t.name}</td>
                  <td className="right mono">{t.defaultDays}</td>
                  <td className="muted">{t.requiresAttachment ? 'Required' : 'Optional'}</td>
                  <td><Status value={t.isActive ? 'Active' : 'Inactive'} /></td>
                  <td className="right"><button className="link" onClick={() => setEditing(t)}>Edit</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {editing && <LeaveTypeModal value={editing} onClose={() => setEditing(null)} onDone={() => { setEditing(null); reload() }} />}
    </div>
  )
}

function LeaveTypeModal({ value, onClose, onDone }: { value: Partial<LeaveType>; onClose: () => void; onDone: () => void }) {
  const [form, setForm] = useState(value)
  const [error, setError] = useState<string | null>(null)

  const save = async () => {
    try {
      if (form.id) await api.put(`/api/leave-types/${form.id}`, form)
      else await api.post('/api/leave-types', form)
      onDone()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  return (
    <Modal title={form.id ? 'Edit leave type' : 'Add leave type'} onClose={onClose}>
      <div className="form">
        <Field label="Name"><input value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} autoFocus /></Field>
        <Field label="Days per year">
          <input type="number" min="0" step="0.5" value={form.defaultDays} onChange={e => setForm({ ...form, defaultDays: Number(e.target.value) })} />
        </Field>
        <label className="check">
          <input type="checkbox" checked={form.requiresAttachment} onChange={e => setForm({ ...form, requiresAttachment: e.target.checked })} />
          Supporting document required (e.g. medical certificate)
        </label>
        <label className="check">
          <input type="checkbox" checked={form.isActive} onChange={e => setForm({ ...form, isActive: e.target.checked })} />
          Active
        </label>
        <ErrorText error={error} />
        <div className="form-actions">
          <button className="primary" onClick={save}>Save</button>
          <button onClick={onClose}>Cancel</button>
        </div>
      </div>
    </Modal>
  )
}

interface Flow { type: RequestType; steps: { approverKind: ApproverKind; approverUserId: number | null }[] }

function Workflows() {
  const { data, reload } = useFetch<Flow[]>('/api/workflows')
  const { data: people } = useFetch<UserRow[]>('/api/users')
  const [flows, setFlows] = useState<Flow[] | null>(null)
  const [message, setMessage] = useState<{ type: RequestType; text: string; error?: boolean } | null>(null)

  useEffect(() => setFlows(data), [data])
  if (!flows) return <Loading />

  const update = (type: RequestType, steps: Flow['steps']) =>
    setFlows(fs => fs!.map(f => (f.type === type ? { ...f, steps } : f)))

  const save = async (flow: Flow) => {
    try {
      await api.put(`/api/workflows/${flow.type}`, flow.steps)
      setMessage({ type: flow.type, text: 'Saved. Applies to new requests.' })
      reload()
    } catch (e) {
      setMessage({ type: flow.type, text: (e as Error).message, error: true })
    }
  }

  return (
    <div className="stack" style={{ maxWidth: 720 }}>
      <p className="muted">
        Each request goes through these steps in order. A direct-manager step is skipped when the requester has no manager.
        Admins can approve any step.
      </p>
      {flows.map(flow => (
        <section className="card" key={flow.type}>
          <div className="card-head"><h2>{flow.type}</h2></div>
          <div className="stack" style={{ gap: 8 }}>
            {flow.steps.length === 0 && <p className="muted">No approval needed: requests are approved straight away.</p>}
            {flow.steps.map((s, i) => (
              <div className="row" key={i}>
                <span className="faint mono" style={{ width: 20 }}>{i + 1}.</span>
                <select value={s.approverKind} style={{ width: 200 }}
                  onChange={e => update(flow.type, flow.steps.map((x, j) => j === i ? { approverKind: e.target.value as ApproverKind, approverUserId: null } : x))}>
                  {Object.entries(approverLabel).map(([k, label]) => <option key={k} value={k}>{label}</option>)}
                </select>
                {s.approverKind === 'SpecificUser' && (
                  <select value={s.approverUserId ?? ''} style={{ width: 220 }}
                    onChange={e => update(flow.type, flow.steps.map((x, j) => j === i ? { ...x, approverUserId: Number(e.target.value) || null } : x))}>
                    <option value="">Choose person…</option>
                    {people?.map(p => <option key={p.id} value={p.id}>{p.fullName}</option>)}
                  </select>
                )}
                <button className="ghost icon" aria-label="Remove step" onClick={() => update(flow.type, flow.steps.filter((_, j) => j !== i))}>×</button>
              </div>
            ))}
            <div className="row" style={{ marginTop: 8 }}>
              <button onClick={() => update(flow.type, [...flow.steps, { approverKind: 'HR', approverUserId: null }])} disabled={flow.steps.length >= 5}>
                Add step
              </button>
              <button className="primary" onClick={() => save(flow)}>Save</button>
              {message?.type === flow.type && <span className={message.error ? 'error' : 'muted'}>{message.text}</span>}
            </div>
          </div>
        </section>
      ))}
    </div>
  )
}

function Company() {
  const { data } = useFetch<Record<string, string>>('/api/settings')
  const [form, setForm] = useState<Record<string, string>>({})
  const [status, setStatus] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => { if (data) setForm(data) }, [data])
  if (!data) return <Loading />

  const save = async () => {
    setError(null)
    try {
      await api.put('/api/settings', form)
      setStatus('Saved. Reload the page to see the new company name.')
    } catch (e) {
      setError((e as Error).message)
    }
  }

  return (
    <div className="form">
      <Field label="Company name"><input value={form.CompanyName ?? ''} onChange={e => setForm({ ...form, CompanyName: e.target.value })} /></Field>
      <div className="grid-2">
        <Field label="Work starts" hint="Clock-ins after this are marked late">
          <input type="time" value={form.WorkStartTime ?? ''} onChange={e => setForm({ ...form, WorkStartTime: e.target.value })} />
        </Field>
        <Field label="Work ends">
          <input type="time" value={form.WorkEndTime ?? ''} onChange={e => setForm({ ...form, WorkEndTime: e.target.value })} />
        </Field>
      </div>
      <ErrorText error={error} />
      <div className="form-actions">
        <button className="primary" onClick={save}>Save</button>
        {status && <span className="muted" style={{ alignSelf: 'center' }}>{status}</span>}
      </div>
    </div>
  )
}

function Holidays() {
  const thisYear = new Date().getFullYear()
  const [year, setYear] = useState(thisYear)
  const { data, reload } = useFetch<Holiday[]>(`/api/holidays?year=${year}`)
  const [date, setDate] = useState('')
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)

  const add = async () => {
    setError(null)
    try {
      await api.post('/api/holidays', { date, name })
      setDate('')
      setName('')
      if (Number(date.slice(0, 4)) !== year) setYear(Number(date.slice(0, 4)))
      else reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const remove = async (h: Holiday) => {
    if (!confirm(`Remove ${h.name}?`)) return
    await api.del(`/api/holidays/${h.id}`)
    reload()
  }

  return (
    <div className="stack" style={{ maxWidth: 640 }}>
      <p className="muted">Public holidays are not counted as leave days. Add your state's holidays at the start of each year.</p>
      <form className="row" onSubmit={e => { e.preventDefault(); add() }}>
        <input type="date" value={date} onChange={e => setDate(e.target.value)} required style={{ width: 170 }} />
        <input placeholder="Holiday name, e.g. Hari Raya Aidilfitri" value={name} onChange={e => setName(e.target.value)} required style={{ flex: 1 }} />
        <button className="primary">Add</button>
      </form>
      <ErrorText error={error} />
      <div className="spread">
        <h2>{year}</h2>
        <select value={year} onChange={e => setYear(Number(e.target.value))} style={{ width: 'auto' }}>
          {[thisYear - 1, thisYear, thisYear + 1].map(y => <option key={y}>{y}</option>)}
        </select>
      </div>
      {!data ? <Loading /> : (
        <div className="table-wrap">
          <table>
            <tbody>
              {data.length === 0 && <tr><td className="muted">No public holidays for {year}.</td></tr>}
              {data.map(h => (
                <tr key={h.id}>
                  <td className="nowrap" style={{ width: 140 }}>{fmt.date(h.date)}</td>
                  <td className="muted nowrap" style={{ width: 90 }}>
                    {new Intl.DateTimeFormat('en-GB', { weekday: 'short' }).format(new Date(`${h.date.slice(0, 10)}T00:00:00`))}
                  </td>
                  <td>{h.name}</td>
                  <td className="right"><button className="link" onClick={() => remove(h)}>Remove</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

interface EmailForm {
  enabled: boolean
  host: string
  port: number
  security: string
  username: string
  from: string
  passwordSet: boolean
}

function EmailSettings() {
  const { data } = useFetch<EmailForm>('/api/settings/email')
  const [form, setForm] = useState<EmailForm | null>(null)
  const [password, setPassword] = useState('')
  const [testTo, setTestTo] = useState('')
  const [status, setStatus] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => { if (data) setForm({ ...data, host: data.host ?? '', username: data.username ?? '', from: data.from ?? '' }) }, [data])
  if (!form) return <Loading />

  const run = async (action: () => Promise<void>, done: string) => {
    setBusy(true)
    setError(null)
    setStatus(null)
    try {
      await action()
      setStatus(done)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const save = () => run(async () => {
    await api.put('/api/settings/email', { ...form, password: password === '' ? null : password })
    if (password) setForm({ ...form, passwordSet: true })
    setPassword('')
  }, 'Saved.')

  const test = () => run(() => api.post('/api/settings/email/test', { to: testTo }), `Test email sent to ${testTo}.`)

  return (
    <div className="form">
      <p className="muted">
        Managey emails approvers when a request needs them, and tells the requester when it is approved or rejected.
        Use your company mail server (for example Microsoft 365: smtp.office365.com, port 587).
      </p>
      <label className="check">
        <input type="checkbox" checked={form.enabled} onChange={e => setForm({ ...form, enabled: e.target.checked })} />
        Send email notifications
      </label>
      <div className="grid-2">
        <Field label="SMTP server"><input value={form.host} onChange={e => setForm({ ...form, host: e.target.value })} placeholder="smtp.example.com" /></Field>
        <div className="grid-2">
          <Field label="Port"><input type="number" value={form.port} onChange={e => setForm({ ...form, port: Number(e.target.value) })} /></Field>
          <Field label="Security">
            <select value={form.security} onChange={e => setForm({ ...form, security: e.target.value })}>
              <option value="Auto">Auto</option>
              <option value="StartTls">STARTTLS</option>
              <option value="Ssl">SSL/TLS</option>
              <option value="None">None</option>
            </select>
          </Field>
        </div>
        <Field label="Username" hint="Leave blank if the server does not need sign-in">
          <input value={form.username} onChange={e => setForm({ ...form, username: e.target.value })} autoComplete="off" />
        </Field>
        <Field label="Password" hint={form.passwordSet ? 'Saved. Leave blank to keep it.' : 'Stored encrypted on the server'}>
          <input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="new-password"
            placeholder={form.passwordSet ? '••••••••' : ''} />
        </Field>
        <Field label="Send from" hint="e.g. hr@yourcompany.com">
          <input type="email" value={form.from} onChange={e => setForm({ ...form, from: e.target.value })} />
        </Field>
      </div>
      <div className="form-actions">
        <button className="primary" disabled={busy} onClick={save}>Save</button>
      </div>

      <h2 style={{ marginTop: 8 }}>Send a test</h2>
      <div className="row">
        <input type="email" placeholder="you@yourcompany.com" value={testTo} onChange={e => setTestTo(e.target.value)} style={{ flex: 1 }} />
        <button disabled={busy || !testTo} onClick={test}>{busy ? 'Sending…' : 'Send test email'}</button>
      </div>
      <ErrorText error={error} />
      {status && <p className="muted">{status}</p>}
    </div>
  )
}
