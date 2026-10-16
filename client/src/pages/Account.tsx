import { useState, type FormEvent } from 'react'
import { api } from '../api'
import { useMe } from '../auth'
import { ErrorText, Field, PageHeader } from '../components/ui'

export default function Account() {
  const { me } = useMe()
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setDone(false)
    if (next !== confirm) {
      setError('New passwords do not match.')
      return
    }
    try {
      await api.post('/api/auth/change-password', { currentPassword: current, newPassword: next })
      setCurrent('')
      setNext('')
      setConfirm('')
      setDone(true)
    } catch (err) {
      setError((err as Error).message)
    }
  }

  return (
    <div className="stack">
      <PageHeader title="Account" />
      <section className="card" style={{ maxWidth: 640 }}>
        <dl className="details">
          <dt>Name</dt><dd>{me.fullName}</dd>
          <dt>Employee no.</dt><dd className="mono">{me.employeeNo}</dd>
          <dt>Username</dt><dd>{me.username}</dd>
          <dt>Department</dt><dd>{me.department ?? '—'}</dd>
          <dt>Role</dt><dd>{me.role}</dd>
        </dl>
      </section>

      <form className="form" onSubmit={submit} style={{ maxWidth: 360 }}>
        <h2>Change password</h2>
        <Field label="Current password">
          <input type="password" value={current} onChange={e => setCurrent(e.target.value)} required autoComplete="current-password" />
        </Field>
        <Field label="New password" hint="At least 8 characters">
          <input type="password" value={next} onChange={e => setNext(e.target.value)} required minLength={8} autoComplete="new-password" />
        </Field>
        <Field label="Confirm new password">
          <input type="password" value={confirm} onChange={e => setConfirm(e.target.value)} required autoComplete="new-password" />
        </Field>
        <ErrorText error={error} />
        {done && <p className="muted">Password changed.</p>}
        <div className="form-actions"><button className="primary">Change password</button></div>
      </form>
    </div>
  )
}
