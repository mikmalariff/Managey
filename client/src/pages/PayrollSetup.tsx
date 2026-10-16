import { useEffect, useState } from 'react'
import { api, type ContributionBand, type ContributionMethod, type ContributionRounding, type ContributionScheme } from '../api'
import { Empty, ErrorText, Field, Loading, Modal, Status, useFetch } from '../components/ui'
import { amount, currencyCode } from '../format'

interface Preset { key: string; name: string; currency: string; description: string }

const methods: { value: ContributionMethod; label: string; hint: string }[] = [
  { value: 'Percentage', label: 'Percentage of wages', hint: 'A flat percentage for employee and employer, optionally capped at a wage ceiling.' },
  { value: 'RateBands', label: 'Percentage by wage band', hint: 'Different percentages for different wage ranges. A band can round wages up to a step first.' },
  { value: 'AmountBands', label: 'Fixed amount by wage band', hint: 'A contribution table: each wage range has fixed amounts.' },
  { value: 'Fixed', label: 'Fixed amount', hint: 'The same amount every month for everyone in the scheme.' },
  { value: 'PerEmployee', label: 'Amount per employee', hint: 'Each employee has their own monthly amount, e.g. income tax set by the tax office.' },
]

const describe = (s: ContributionScheme) => {
  switch (s.method) {
    case 'Percentage': return `${s.employeeRate}% / ${s.employerRate}%${s.wageCeiling ? ` up to ${amount(s.wageCeiling)}` : ''}`
    case 'RateBands': return `${s.bands.length} rate band(s)`
    case 'AmountBands': return `Table, ${s.bands.length} band(s)`
    case 'Fixed': return `${amount(s.employeeRate)} / ${amount(s.employerRate)} a month`
    default: return 'Set per employee'
  }
}

export default function PayrollSetup() {
  const { data: schemes, reload } = useFetch<ContributionScheme[]>('/api/payroll/schemes')
  const { data: presets } = useFetch<Preset[]>('/api/payroll/presets')
  const [editing, setEditing] = useState<Partial<ContributionScheme> | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const applyPreset = async (p: Preset) => {
    if (!confirm(`Add the ${p.name} schemes and switch the currency to ${p.currency}? Current staff are enrolled in the default schemes.`)) return
    setError(null)
    try {
      const r = await api.post<{ added: number }>(`/api/payroll/presets/${p.key}`)
      setMessage(r.added > 0 ? `${r.added} scheme(s) added. Reload the page to use the new currency.` : 'All schemes from this preset already exist.')
      reload()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  return (
    <div className="stack" style={{ maxWidth: 880 }}>
      <GeneralSettings />

      <section className="stack" style={{ gap: 12 }}>
        <div className="spread">
          <div>
            <h2>Contribution schemes</h2>
            <p className="muted" style={{ marginTop: 4 }}>
              Pension, social security, insurance and tax deductions. Define your country's rules here, or start from a preset.
            </p>
          </div>
          <button className="primary" onClick={() => setEditing({ method: 'Percentage', rounding: 'Cents', isActive: true, bands: [], sortOrder: (schemes?.length ?? 0) * 10 + 10 })}>
            Add scheme
          </button>
        </div>
        <ErrorText error={error} />
        {message && <p className="muted">{message}</p>}
        {!schemes ? <Loading /> : schemes.length === 0 ? (
          <Empty>No schemes yet. Payslips will only have basic pay, allowances and deductions.</Empty>
        ) : (
          <div className="table-wrap">
            <table>
              <thead><tr><th>Scheme</th><th>How it works</th><th className="right">Employees</th><th>Status</th><th></th></tr></thead>
              <tbody>
                {schemes.map(s => (
                  <tr key={s.id}>
                    <td>
                      <div>{s.name}</div>
                      <div className="faint mono">{s.code}{s.defaultForNewEmployees && ' · default'}{s.effectiveFrom && ` · from ${s.effectiveFrom.slice(0, 7)}`}</div>
                    </td>
                    <td className="muted">{describe(s)}</td>
                    <td className="right mono">{s.members}</td>
                    <td><Status value={s.isActive ? 'Active' : 'Inactive'} /></td>
                    <td className="right"><button className="link" onClick={() => setEditing(s)}>Edit</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {presets && presets.length > 0 && (
        <section className="card">
          <h2>Country presets</h2>
          <p className="muted" style={{ marginBottom: 12 }}>Adds ready-made schemes you can edit afterwards. Schemes whose code already exists are skipped.</p>
          {presets.map(p => (
            <div className="spread" key={p.key} style={{ alignItems: 'flex-start' }}>
              <div>
                <strong>{p.name}</strong> <span className="faint mono">{p.currency}</span>
                <p className="muted" style={{ fontSize: 13 }}>{p.description}</p>
              </div>
              <button onClick={() => applyPreset(p)}>Apply</button>
            </div>
          ))}
        </section>
      )}

      {editing && <SchemeModal value={editing} onClose={() => setEditing(null)} onDone={() => { setEditing(null); reload() }} />}
    </div>
  )
}

function GeneralSettings() {
  const { data } = useFetch<Record<string, number>>('/api/payroll/settings')
  const [days, setDays] = useState('')
  const [status, setStatus] = useState<string | null>(null)

  useEffect(() => { if (data) setDays(String(data.DaysPerMonth)) }, [data])
  if (!data) return null

  const save = async () => {
    await api.put('/api/payroll/settings', { DaysPerMonth: Number(days) })
    setStatus('Saved.')
  }

  return (
    <section className="row" style={{ alignItems: 'flex-end' }}>
      <Field label="Days per month" hint="Daily rate for unpaid leave = basic salary ÷ this">
        <input type="number" min="1" max="31" value={days} onChange={e => { setDays(e.target.value); setStatus(null) }} style={{ width: 120 }} />
      </Field>
      <button onClick={save} style={{ marginBottom: 20 }}>Save</button>
      {status && <span className="muted" style={{ marginBottom: 26 }}>{status}</span>}
    </section>
  )
}

function SchemeModal({ value, onClose, onDone }: { value: Partial<ContributionScheme>; onClose: () => void; onDone: () => void }) {
  const [form, setForm] = useState<Partial<ContributionScheme>>({ employeeRate: 0, employerRate: 0, ...value })
  const [paste, setPaste] = useState('')
  const [error, setError] = useState<string | null>(null)
  const method = form.method ?? 'Percentage'
  const banded = method === 'RateBands' || method === 'AmountBands'
  const bands = form.bands ?? []
  const unit = method === 'RateBands' ? '%' : currencyCode()

  const set = (patch: Partial<ContributionScheme>) => setForm({ ...form, ...patch })
  const setBand = (i: number, patch: Partial<ContributionBand>) => set({ bands: bands.map((b, j) => (j === i ? { ...b, ...patch } : b)) })
  const num = (v: string) => (v === '' ? 0 : Number(v))

  /** Rows copied from a spreadsheet: "up to, employee, employer[, step]" separated by tabs, commas or spaces. */
  const importRows = () => {
    const rows = paste.split(/\r?\n/).map(l => l.trim()).filter(Boolean).map(l => l.replace(/[^\d.\s,\t-]/g, '').split(/[\t,; ]+/).filter(Boolean).map(Number))
    const parsed = rows.filter(r => r.length >= 3 && r.every(n => !Number.isNaN(n)))
      .map(r => ({ upTo: r[0], employee: r[1], employer: r[2], step: r[3] ?? 0 }))
    if (parsed.length === 0) { setError('No rows found. Use one band per line: up to, employee, employer.'); return }
    set({ bands: parsed })
    setPaste('')
    setError(null)
  }

  const save = async () => {
    setError(null)
    const body = { ...form, method, effectiveFrom: form.effectiveFrom ? `${form.effectiveFrom.slice(0, 7)}-01` : null, bands: banded ? bands : [] }
    try {
      if (form.id) await api.put(`/api/payroll/schemes/${form.id}`, body)
      else await api.post('/api/payroll/schemes', body)
      onDone()
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const remove = async () => {
    if (!confirm(`Delete ${form.name}? Employees leave the scheme; existing payslips keep their amounts.`)) return
    await api.del(`/api/payroll/schemes/${form.id}`)
    onDone()
  }

  return (
    <Modal title={form.id ? `Edit ${form.name}` : 'Add contribution scheme'} onClose={onClose}>
      <div className="form" style={{ maxWidth: 'none' }}>
        <div className="grid-2">
          <Field label="Name"><input value={form.name ?? ''} onChange={e => set({ name: e.target.value })} placeholder="e.g. Provident fund" autoFocus /></Field>
          <Field label="Code" hint="Short label on payslips"><input value={form.code ?? ''} maxLength={20} onChange={e => set({ code: e.target.value.toUpperCase() })} placeholder="e.g. CPF" /></Field>
        </div>
        <Field label="How it's calculated" hint={methods.find(m => m.value === method)?.hint}>
          <select value={method} onChange={e => set({ method: e.target.value as ContributionMethod })}>
            {methods.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}
          </select>
        </Field>

        {method === 'Percentage' && (
          <div className="grid-2">
            <Field label="Employee %"><input type="number" min="0" step="0.01" value={form.employeeRate} onChange={e => set({ employeeRate: num(e.target.value) })} /></Field>
            <Field label="Employer %"><input type="number" min="0" step="0.01" value={form.employerRate} onChange={e => set({ employerRate: num(e.target.value) })} /></Field>
          </div>
        )}
        {method === 'Fixed' && (
          <div className="grid-2">
            <Field label={`Employee (${currencyCode()})`}><input type="number" min="0" step="0.01" value={form.employeeRate} onChange={e => set({ employeeRate: num(e.target.value) })} /></Field>
            <Field label={`Employer (${currencyCode()})`}><input type="number" min="0" step="0.01" value={form.employerRate} onChange={e => set({ employerRate: num(e.target.value) })} /></Field>
          </div>
        )}
        {method === 'PerEmployee' && <p className="muted">Set each person's amount on their profile under Pay details.</p>}

        {(method === 'Percentage' || method === 'RateBands' || method === 'AmountBands') && (
          <div className="grid-2">
            <Field label={`Wage ceiling (${currencyCode()})`} hint="Wages above this are ignored. Leave empty for none.">
              <input type="number" min="0" step="0.01" value={form.wageCeiling ?? ''} onChange={e => set({ wageCeiling: e.target.value === '' ? null : Number(e.target.value) })} />
            </Field>
            {method !== 'AmountBands' && (
              <Field label="Rounding">
                <select value={form.rounding ?? 'Cents'} onChange={e => set({ rounding: e.target.value as ContributionRounding })}>
                  <option value="Cents">To the cent</option>
                  <option value="NearestWhole">To the nearest whole unit</option>
                  <option value="UpWhole">Up to the next whole unit</option>
                </select>
              </Field>
            )}
          </div>
        )}

        {banded && (
          <div className="stack" style={{ gap: 8 }}>
            <div className="spread">
              <h3>Wage bands</h3>
              <button className="link" onClick={() => set({ bands: [...bands, { upTo: 0, employee: 0, employer: 0, step: 0 }] })}>Add band</button>
            </div>
            <p className="faint" style={{ fontSize: 12 }}>Each band covers wages above the previous band up to its limit. Wages above the last band use the last band.</p>
            {bands.length > 0 && (
              <div className="band-table">
                <table>
                  <thead>
                    <tr>
                      <th>Up to ({currencyCode()})</th><th>Employee ({unit})</th><th>Employer ({unit})</th>
                      {method === 'RateBands' && <th title="Round wages up to a multiple of this first">Step</th>}<th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {bands.map((b, i) => (
                      <tr key={i}>
                        <td><input type="number" min="0" step="0.01" value={b.upTo} onChange={e => setBand(i, { upTo: num(e.target.value) })} /></td>
                        <td><input type="number" min="0" step="0.01" value={b.employee} onChange={e => setBand(i, { employee: num(e.target.value) })} /></td>
                        <td><input type="number" min="0" step="0.01" value={b.employer} onChange={e => setBand(i, { employer: num(e.target.value) })} /></td>
                        {method === 'RateBands' && <td><input type="number" min="0" step="0.01" value={b.step} onChange={e => setBand(i, { step: num(e.target.value) })} /></td>}
                        <td><button className="ghost icon" aria-label="Remove band" onClick={() => set({ bands: bands.filter((_, j) => j !== i) })}>×</button></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <details>
              <summary className="muted" style={{ cursor: 'pointer' }}>Paste a table from a spreadsheet</summary>
              <div className="stack" style={{ gap: 8, marginTop: 8 }}>
                <textarea value={paste} onChange={e => setPaste(e.target.value)} placeholder={'One band per line: up to, employee, employer\n30\t0.10\t0.40\n50\t0.20\t0.70'} />
                <div><button onClick={importRows} disabled={!paste.trim()}>Replace bands</button></div>
              </div>
            </details>
          </div>
        )}

        <div className="grid-2">
          <Field label="Starts from" hint="Leave empty to always apply">
            <input type="month" value={form.effectiveFrom?.slice(0, 7) ?? ''} onChange={e => set({ effectiveFrom: e.target.value || null })} />
          </Field>
          <Field label="Employer reference" hint="Your registration no. with this scheme">
            <input value={form.employerRef ?? ''} onChange={e => set({ employerRef: e.target.value })} />
          </Field>
          <Field label="Order on payslips"><input type="number" value={form.sortOrder ?? 0} onChange={e => set({ sortOrder: num(e.target.value) })} /></Field>
        </div>
        <label className="check"><input type="checkbox" checked={!!form.defaultForNewEmployees} onChange={e => set({ defaultForNewEmployees: e.target.checked })} /> Add new employees automatically</label>
        <label className="check"><input type="checkbox" checked={!!form.memberNoRequired} onChange={e => set({ memberNoRequired: e.target.checked })} /> Warn when an employee has no member no.</label>
        <label className="check"><input type="checkbox" checked={form.isActive ?? true} onChange={e => set({ isActive: e.target.checked })} /> Active</label>

        <ErrorText error={error} />
        <div className="form-actions">
          <button className="primary" onClick={save}>Save</button>
          <button onClick={onClose}>Cancel</button>
          {form.id && <button className="danger" onClick={remove} style={{ marginLeft: 'auto' }}>Delete</button>}
        </div>
      </div>
    </Modal>
  )
}
