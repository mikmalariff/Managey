import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, type PayItemKind, type PayslipDetail, type PayslipLine } from '../api'
import { ErrorText, Field, Loading, Modal, PageHeader, useFetch } from '../components/ui'
import { amount, monthName } from '../format'

interface RunDetail {
  id: number
  year: number
  month: number
  status: 'Draft' | 'Finalized'
  finalizedAt: string | null
  payslips: {
    id: number
    employeeNo: string
    employeeName: string
    department: string | null
    grossPay: number
    totalDeductions: number
    netPay: number
    epfEmployee: number
    epfEmployer: number
    socsoEmployee: number
    eisEmployee: number
    pcb: number
    missingBank: boolean
  }[]
  totals: { grossPay: number; netPay: number; epf: number; socso: number; eis: number; pcb: number; employerCost: number }
}

export default function PayrollRun() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { data: run, error, reload } = useFetch<RunDetail>(`/api/payroll/runs/${id}`)
  const [editing, setEditing] = useState<number | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  if (error) return <p className="error">{error}</p>
  if (!run) return <Loading />
  const draft = run.status === 'Draft'

  const act = async (message: string, action: () => Promise<unknown>, after?: () => void) => {
    if (!confirm(message)) return
    setActionError(null)
    try {
      await action()
      if (after) after()
      else reload()
    } catch (e) {
      setActionError((e as Error).message)
    }
  }

  return (
    <div className="stack">
      <PageHeader
        title={`Payroll · ${monthName(run.year, run.month)}`}
        subtitle={draft ? 'Draft: check and adjust each payslip, then finalise.' : 'Finalised. Staff can see their payslips.'}
        actions={<>
          <a className="button" href={`/api/payroll/runs/${run.id}/export.xlsx`}>Excel</a>
          <a className="button" href={`/api/payroll/runs/${run.id}/bank.csv`}>Bank file</a>
          {draft && <>
            <button onClick={() => act('Rebuild all payslips from current salaries, claims and leave? Manual changes will be lost.',
              () => api.post(`/api/payroll/runs/${run.id}/regenerate`))}>Rebuild</button>
            <button className="danger" onClick={() => act('Delete this draft payroll?',
              () => api.del(`/api/payroll/runs/${run.id}`), () => navigate('/payroll'))}>Delete</button>
            <button className="primary" onClick={() => act('Finalise payroll? Payslips will be locked and visible to staff.',
              () => api.post(`/api/payroll/runs/${run.id}/finalize`))}>Finalise</button>
          </>}
        </>}
      />
      <ErrorText error={actionError} />

      <div className="stats">
        <div className="stat"><div className="label">Net pay</div><div className="value">{amount(run.totals.netPay)}</div><div className="sub">to bank</div></div>
        <div className="stat"><div className="label">EPF</div><div className="value">{amount(run.totals.epf)}</div><div className="sub">employee + employer</div></div>
        <div className="stat"><div className="label">SOCSO + EIS</div><div className="value">{amount(run.totals.socso + run.totals.eis)}</div><div className="sub">employee + employer</div></div>
        <div className="stat"><div className="label">PCB</div><div className="value">{amount(run.totals.pcb)}</div><div className="sub">to LHDN</div></div>
        <div className="stat highlight"><div className="label">Total cost</div><div className="value">{amount(run.totals.employerCost)}</div><div className="sub">gross + employer share</div></div>
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Employee</th>
              <th className="right">Gross</th>
              <th className="right">EPF</th>
              <th className="right">SOCSO</th>
              <th className="right">EIS</th>
              <th className="right">PCB</th>
              <th className="right">Deductions</th>
              <th className="right">Net pay</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {run.payslips.map(p => (
              <tr key={p.id}>
                <td>
                  <div>{p.employeeName}</div>
                  <div className="faint"><span className="mono">{p.employeeNo}</span>{p.department && ` · ${p.department}`}{p.missingBank && ' · no bank account'}</div>
                </td>
                <td className="right mono">{amount(p.grossPay)}</td>
                <td className="right mono">{amount(p.epfEmployee)}</td>
                <td className="right mono">{amount(p.socsoEmployee)}</td>
                <td className="right mono">{amount(p.eisEmployee)}</td>
                <td className="right mono">{amount(p.pcb)}</td>
                <td className="right mono">{amount(p.totalDeductions)}</td>
                <td className="right mono"><strong>{amount(p.netPay)}</strong></td>
                <td className="right nowrap">
                  {draft && <button className="link" onClick={() => setEditing(p.id)}>Edit</button>}{'  '}
                  <Link to={`/payslips/${p.id}`} className="muted">View</Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="faint" style={{ fontSize: 12 }}>
        EPF, SOCSO and EIS are calculated from the rates under Setup → Payroll. Check them against the official contribution
        tables, and edit a payslip to enter exact amounts where needed.
      </p>

      {editing !== null && <EditPayslip id={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload() }} />}
    </div>
  )
}

function EditPayslip({ id, onClose, onSaved }: { id: number; onClose: () => void; onSaved: () => void }) {
  const { data } = useFetch<PayslipDetail>(`/api/payslips/${id}`)
  const [form, setForm] = useState<PayslipDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (data && !form) setForm(data)
  if (!form) return <Modal title="Edit payslip" onClose={onClose}><Loading /></Modal>

  const setLine = (i: number, patch: Partial<PayslipLine>) =>
    setForm({ ...form, lines: form.lines.map((l, j) => (j === i ? { ...l, ...patch } : l)) })
  const addLine = (kind: PayItemKind) =>
    setForm({ ...form, lines: [...form.lines, { kind, name: '', amount: 0, statutory: kind === 'Earning', source: 'Manual', requestId: null }] })
  const num = (v: string) => (v === '' ? 0 : Number(v))

  const save = async () => {
    setBusy(true)
    setError(null)
    try {
      await api.put(`/api/payroll/payslips/${id}`, form)
      onSaved()
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  const statutory: [keyof PayslipDetail, string][] = [
    ['epfEmployee', 'EPF employee'], ['epfEmployer', 'EPF employer'],
    ['socsoEmployee', 'SOCSO employee'], ['socsoEmployer', 'SOCSO employer'],
    ['eisEmployee', 'EIS employee'], ['eisEmployer', 'EIS employer'],
  ]

  return (
    <Modal title={`${form.employeeName} · ${monthName(form.year, form.month)}`} onClose={onClose}>
      <div className="form" style={{ maxWidth: 'none' }}>
        <div className="grid-2">
          <Field label="Basic salary (RM)">
            <input type="number" min="0" step="0.01" value={form.basicSalary} onChange={e => setForm({ ...form, basicSalary: num(e.target.value) })} />
          </Field>
          <Field label="PCB / MTD (RM)">
            <input type="number" min="0" step="0.01" value={form.pcb} onChange={e => setForm({ ...form, pcb: num(e.target.value) })} />
          </Field>
        </div>

        {(['Earning', 'Deduction'] as const).map(kind => (
          <div key={kind} className="stack" style={{ gap: 8 }}>
            <div className="spread">
              <h3>{kind === 'Earning' ? 'Allowances & other earnings' : 'Other deductions'}</h3>
              <button className="link" onClick={() => addLine(kind)}>Add</button>
            </div>
            {form.lines.map((l, i) => l.kind === kind && (
              <div className="row" key={i} style={{ flexWrap: 'nowrap' }}>
                <input value={l.name} placeholder={kind === 'Earning' ? 'e.g. Overtime' : 'e.g. Salary advance'} onChange={e => setLine(i, { name: e.target.value })} />
                <input type="number" min="0" step="0.01" value={l.amount} onChange={e => setLine(i, { amount: num(e.target.value) })} style={{ width: 110 }} />
                <label className="check muted nowrap" title="Counts as wages for EPF, SOCSO and EIS">
                  <input type="checkbox" checked={l.statutory} onChange={e => setLine(i, { statutory: e.target.checked })} /> EPF
                </label>
                <button className="ghost icon" aria-label="Remove" onClick={() => setForm({ ...form, lines: form.lines.filter((_, j) => j !== i) })}>×</button>
              </div>
            ))}
          </div>
        ))}

        <label className="check">
          <input type="checkbox" checked={form.statutoryOverride} onChange={e => setForm({ ...form, statutoryOverride: e.target.checked })} />
          Enter EPF, SOCSO and EIS manually
        </label>
        {form.statutoryOverride && (
          <div className="grid-2">
            {statutory.map(([key, label]) => (
              <Field key={key} label={`${label} (RM)`}>
                <input type="number" min="0" step="0.01" value={form[key] as number}
                  onChange={e => setForm({ ...form, [key]: num(e.target.value) })} />
              </Field>
            ))}
          </div>
        )}

        <ErrorText error={error} />
        <div className="form-actions">
          <button className="primary" disabled={busy} onClick={save}>Save & recalculate</button>
          <button onClick={onClose}>Cancel</button>
        </div>
      </div>
    </Modal>
  )
}
