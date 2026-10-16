import { useState, type FormEvent } from 'react'
import { useAuth } from '../auth'
import { ErrorText, Field } from '../components/ui'

export default function Login() {
  const { login } = useAuth()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await login(username, password)
    } catch (err) {
      setError((err as Error).message)
      setBusy(false)
    }
  }

  return (
    <div className="login">
      <form onSubmit={submit}>
        <div>
          <h1>Managey</h1>
          <p className="muted">Sign in to continue</p>
        </div>
        <Field label="Username">
          <input value={username} onChange={e => setUsername(e.target.value)} autoFocus autoComplete="username" required />
        </Field>
        <Field label="Password">
          <input type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="current-password" required />
        </Field>
        <ErrorText error={error} />
        <button className="primary" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>
    </div>
  )
}
