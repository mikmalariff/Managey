import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, type PayItemKind, type PayslipContribution, type PayslipDetail, type PayslipLine } from '../api'
import { ErrorText, Field, Loading, Modal, PageHeader, useFetch } from '../components/ui'
import { amount, currencyCode, monthName } from '../format'

interface RunDetail {
  id: number
  year: number
  month: number
  status: 'Draft' | 'Finalized'
  finalizedAt: string | null
  schemes: { code: string; name: string; employee: number; employer: number }[]
  payslips: {
    id: number
    employeeNo: string
    employeeName: string
    department: string | null
    grossPay: number
    totalDeductions: number
    employerContributions: number
    netPay: number
    contributions: { code: string; employee: number; employer: number }[]
    missingBank: boolean
    missingId: boolean
    missingMemberNo: string[]
  }[]
  totals: { grossPay: number; netPay: number; employee: number; employer: number; employerCost: number }
  exports: { kwsp: boolean; perkeso: boolean }
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
  const missingId = run.payslips.filter(p => p.missingId).map(p => p.employeeName)
  const missingMember = run.payslips.filter(p => p.missingMemberNo.length > 0).map(p => `${p.employeeName} (${p.missingMemberNo.join(', ')})`)
  const missingBank = run.payslips.filter(p => p.missingBank).map(p => p.employeeName)
  const employeeShare = (p: RunDetail['payslips'][number], code: string) =>
    p.contributions.filter(c => c.code === code).reduce((sum, c) => sum + c.employee, 0)

  /** Fetch first so a server error (e.g. missing employer code) shows here instead of as a JSON page. */
  const download = async (file: string) => {
    setActionError(null)
    const res = await fetch(`/api/payroll/runs/${run.id}/${file}`)
    if (!res.ok) {
      const body = await res.json().catch(() => null)
      setActionError(body?.message ?? `Download failed (${res.status}).`)
      return
    }
    const name = /filename=([^;]+)/.exec(res.headers.get('Content-Disposition') ?? '')?.[1] ?? file
    const link = document.createElement('a')
    link.href = URL.createObjectURL(await res.blob())
    link.download = name.replace(/"/g, '')
    link.click()
  }

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
      {(missingId.length > 0 || missingMember.length > 0 || missingBank.length > 0) && (
        <div className="notice">
          {missingId.length > 0 && <p>No ID or passport no. for {missingId.join(', ')}.</p>}
          {missingMember.length > 0 && <p>No member no. for {missingMember.join('; ')}.</p>}
          {missingBank.length > 0 && <p>No bank account for {missingBank.join(', ')}.</p>}
          <p className="muted">Add them under People → Pay details, then rebuild this payroll.</p>
        </div>
      )}

      <div className="stats">
        <div className="stat"><div className="label">Gross pay</div><div className="value">{amount(run.totals.grossPay)}</div><div className="sub">{currencyCode()}</div></div>
        <div className="stat"><div className="label">Net pay</div><div className="value">{amount(run.totals.netPay)}</div><div className="sub">to bank</div></div>
        <div className="stat"><div className="label">Contributions</div><div className="value">{amount(run.totals.employee + run.totals.employer)}</div><div className="sub">employee + employer</div></div>
        <div className="stat highlight"><div className="label">Total cost</div><div className="value">{amount(run.totals.employerCost)}</div><div className="sub">gross + employer share</div></div>
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Employee</th>
              <th className="right">Gross</th>
              {run.schemes.map(s => <th key={s.code} className="right" title={s.name}>{s.code}</th>)}
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
                  <div className="faint"><span className="mono">{p.employeeNo}</span>{p.department && ` · ${p.department}`}</div>
                </td>
                <td className="right mono">{amount(p.grossPay)}</td>
                {run.schemes.map(s => <td key={s.code} className="right mono">{amount(employeeShare(p, s.code))}</td>)}
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
      <p className="faint" style={{ fontSize: 12 }}>Scheme columns show the employee's share. Employer shares are in the summary below and in the Excel export.</p>

      <section className="grid-2" style={{ alignItems: 'start' }}>
        <div className="card">
          <h2>Contributions this month</h2>
          {run.schemes.length === 0 ? <p className="muted">No contributions. Set up schemes under Setup → Payroll.</p> : (
            <table>
              <thead><tr><th style={{ paddingLeft: 0 }}>Scheme</th><th className="right">Employee</th><th className="right">Employer</th><th></th></tr></thead>
              <tbody>
                {run.schemes.map(s => (
                  <tr key={s.code}>
                    <td style={{ paddingLeft: 0 }}>{s.name} <span className="faint mono">{s.code}</span></td>
                    <td className="right mono">{amount(s.employee)}</td>
                    <td className="right mono">{amount(s.employer)}</td>
                    <td className="right" style={{ paddingRight: 0 }}><button className="link" onClick={() => download(`contributions/${encodeURIComponent(s.code)}.csv`)}>CSV</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
        <div className="card">
          <h2>Downloads</h2>
          <div className="stack" style={{ gap: 8 }}>
            <div className="spread"><span>Payroll summary <span className="faint">(Excel)</span></span><button onClick={() => download('export.xlsx')}>Download</button></div>
            <div className="spread"><span>Bank payment file <span className="faint">(CSV)</span></span><button onClick={() => download('bank.csv')}>Download</button></div>
            {run.exports.kwsp && (
              <div className="spread"><span>KWSP i-Akaun file <span className="faint">(Malaysia)</span></span><button onClick={() => download('kwsp.csv')}>Download</button></div>
            )}
            {run.exports.perkeso && (
              <div className="spread"><span>PERKESO ASSIST file <span className="faint">(Malaysia)</span></span><button onClick={() => download('perkeso.txt')}>Download</button></div>
            )}
          </div>
        </div>
      </section>

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
  const setContribution = (i: number, patch: Partial<PayslipContribution>) =>
    setForm({ ...form, contributions: form.contributions.map((c, j) => (j === i ? { ...c, ...patch } : c)) })
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

  return (
    <Modal title={`${form.employeeName} · ${monthName(form.year, form.month)}`} onClose={onClose}>
      <div className="form" style={{ maxWidth: 'none' }}>
        <Field label={`Basic salary (${currencyCode()})`}>
          <input type="number" min="0" step="0.01" value={form.basicSalary} onChange={e => setForm({ ...form, basicSalary: num(e.target.value) })} />
        </Field>

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
                <label className="check muted nowrap" title="Counts as wages for contributions">
                  <input type="checkbox" checked={l.statutory} onChange={e => setLine(i, { statutory: e.target.checked })} /> Contrib.
                </label>
                <button className="ghost icon" aria-label="Remove" onClick={() => setForm({ ...form, lines: form.lines.filter((_, j) => j !== i) })}>×</button>
              </div>
            ))}
          </div>
        ))}

        <label className="check">
          <input type="checkbox" checked={form.statutoryOverride} onChange={e => setForm({ ...form, statutoryOverride: e.target.checked })} />
          Enter contributions manually
        </label>
        {form.statutoryOverride ? (
          <div className="stack" style={{ gap: 8 }}>
            {form.contributions.map((c, i) => (
              <div className="row" key={i} style={{ flexWrap: 'nowrap' }}>
                <span style={{ flex: 1 }}>{c.name} <span className="faint mono">{c.code}</span></span>
                <input type="number" min="0" step="0.01" title="Employee" value={c.employee} onChange={e => setContribution(i, { employee: num(e.target.value) })} style={{ width: 100 }} />
                <input type="number" min="0" step="0.01" title="Employer" value={c.employer} onChange={e => setContribution(i, { employer: num(e.target.value) })} style={{ width: 100 }} />
              </div>
            ))}
            <p className="faint" style={{ fontSize: 12 }}>Employee share, then employer share.</p>
          </div>
        ) : (
          <p className="faint" style={{ fontSize: 12 }}>Contributions are recalculated from the schemes when you save.</p>
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
