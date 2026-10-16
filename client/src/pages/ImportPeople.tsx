import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { ErrorText, PageHeader, Status } from '../components/ui'

interface ImportRow { row: number; employeeNo: string | null; fullName: string | null; action: 'Create' | 'Update' | 'Error'; messages: string[]; tempPassword: string | null }
interface ImportResult { committed: boolean; created: number; updated: number; errors: number; rows: ImportRow[] }

export default function ImportPeople() {
  const [file, setFile] = useState<File | null>(null)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const send = async (commit: boolean) => {
    if (!file) return
    setBusy(true)
    setError(null)
    const form = new FormData()
    form.set('file', file)
    try {
      setResult(await api.post<ImportResult>(`/api/users/import?commit=${commit}`, form))
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  const passwords = result?.committed ? result.rows.filter(r => r.tempPassword) : []
  const downloadPasswords = () => {
    const csv = 'Employee No,Name,Temporary Password\n' + passwords.map(r => `${r.employeeNo},"${r.fullName}",${r.tempPassword}`).join('\n')
    const link = document.createElement('a')
    link.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    link.download = 'new-employee-passwords.csv'
    link.click()
  }

  return (
    <div className="stack">
      <PageHeader title="Import employees" subtitle="Add or update many employees at once from Excel" />

      <section className="card stack" style={{ gap: 16, maxWidth: 720 }}>
        <ol className="steps">
          <li><a href="/api/users/import/template">Download the Excel template</a> and fill in one employee per row.</li>
          <li>Rows are matched on <strong>Employee No</strong>: existing employees are updated, new ones are created.</li>
          <li>Upload the file and check the preview. Nothing is saved until you confirm.</li>
        </ol>
        <div className="row">
          <input type="file" accept=".xlsx,.csv" onChange={e => { setFile(e.target.files?.[0] ?? null); setResult(null) }} style={{ flex: 1 }} />
          <button className="primary" disabled={!file || busy} onClick={() => send(false)}>{busy ? 'Checking…' : 'Preview'}</button>
        </div>
        <ErrorText error={error} />
      </section>

      {result && !result.committed && (
        <section className="stack" style={{ gap: 12 }}>
          <div className="spread">
            <p>
              <strong>{result.created}</strong> new, <strong>{result.updated}</strong> to update
              {result.errors > 0 && <>, <strong className="error">{result.errors} with problems</strong></>}
            </p>
            {result.errors === 0
              ? <button className="primary" disabled={busy} onClick={() => send(true)}>Import {result.created + result.updated} employees</button>
              : <span className="muted">Fix the rows below in your file and upload it again.</span>}
          </div>
          <RowsTable rows={result.rows} />
        </section>
      )}

      {result?.committed && (
        <section className="card stack" style={{ gap: 12 }}>
          <h2>Import complete</h2>
          <p>{result.created} employees created and {result.updated} updated.</p>
          {passwords.length > 0 && (
            <>
              <p className="muted">
                New employees without a password were given a temporary one. <strong>Download the list now:</strong> it is not shown again.
                Ask them to change it from Account after signing in.
              </p>
              <div className="row">
                <button className="primary" onClick={downloadPasswords}>Download passwords (CSV)</button>
              </div>
              <div className="table-wrap">
                <table>
                  <thead><tr><th>Employee no.</th><th>Name</th><th>Temporary password</th></tr></thead>
                  <tbody>
                    {passwords.map(r => <tr key={r.row}><td className="mono">{r.employeeNo}</td><td>{r.fullName}</td><td className="mono">{r.tempPassword}</td></tr>)}
                  </tbody>
                </table>
              </div>
            </>
          )}
          <div><Link className="button" to="/people">Go to People</Link></div>
        </section>
      )}
    </div>
  )
}

function RowsTable({ rows }: { rows: ImportRow[] }) {
  const sorted = [...rows].sort((a, b) => Number(b.action === 'Error') - Number(a.action === 'Error') || a.row - b.row)
  return (
    <div className="table-wrap">
      <table>
        <thead><tr><th>Row</th><th>Employee no.</th><th>Name</th><th>Result</th></tr></thead>
        <tbody>
          {sorted.map(r => (
            <tr key={r.row}>
              <td className="mono faint">{r.row}</td>
              <td className="mono">{r.employeeNo ?? '—'}</td>
              <td>{r.fullName ?? '—'}</td>
              <td>
                {r.action === 'Error' ? (
                  <>
                    <Status value="Rejected" />
                    <ul className="error" style={{ margin: '4px 0 0', paddingLeft: 18 }}>{r.messages.map((m, i) => <li key={i}>{m}</li>)}</ul>
                  </>
                ) : <span className="muted">{r.action === 'Create' ? 'New employee' : 'Update'}</span>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
