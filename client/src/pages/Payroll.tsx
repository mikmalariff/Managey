import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, type PayrollRunRow } from '../api'
import { Empty, ErrorText, Field, Loading, Modal, PageHeader, Status, useFetch } from '../components/ui'
import { amount, currencyCode, monthName } from '../format'

export default function Payroll() {
  const navigate = useNavigate()
  const { data, error } = useFetch<PayrollRunRow[]>('/api/payroll/runs')
  const [creating, setCreating] = useState(false)

  return (
    <div className="stack">
      <PageHeader
        title="Payroll"
        subtitle="Run payroll once a month, check each payslip, then finalise so staff can see them"
        actions={<button className="primary" onClick={() => setCreating(true)}>Run payroll</button>}
      />
      <ErrorText error={error} />
      {!data ? <Loading /> : data.length === 0 ? (
        <Empty>No payroll yet. Set each employee's basic salary under People, then run payroll for a month.</Empty>
      ) : (
        <div className="table-wrap">
          <table>
            <thead><tr><th>Month</th><th className="right">Employees</th><th className="right">Net pay ({currencyCode()})</th><th>Status</th></tr></thead>
            <tbody>
              {data.map(r => (
                <tr key={r.id} className="clickable" onClick={() => navigate(`/payroll/${r.id}`)}>
                  <td>{monthName(r.year, r.month)}</td>
                  <td className="right mono">{r.employees}</td>
                  <td className="right mono">{amount(r.netPay)}</td>
                  <td><Status value={r.status === 'Draft' ? 'Draft' : 'Finalised'} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {creating && <NewRunModal onClose={() => setCreating(false)} onCreated={id => navigate(`/payroll/${id}`)} />}
    </div>
  )
}

function NewRunModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: number) => void }) {
  const now = new Date()
  const [value, setValue] = useState(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const create = async () => {
    const [year, month] = value.split('-').map(Number)
    setBusy(true)
    try {
      const run = await api.post<{ id: number }>('/api/payroll/runs', { year, month })
      onCreated(run.id)
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  return (
    <Modal title="Run payroll" onClose={onClose}>
      <div className="form">
        <Field label="Month" hint="Payslips are created for every active employee with a basic salary. Approved claims and unpaid leave are added automatically.">
          <input type="month" value={value} onChange={e => setValue(e.target.value)} />
        </Field>
        <ErrorText error={error} />
        <div className="form-actions">
          <button className="primary" disabled={busy || !value} onClick={create}>{busy ? 'Creating…' : 'Create payslips'}</button>
          <button onClick={onClose}>Cancel</button>
        </div>
      </div>
    </Modal>
  )
}
