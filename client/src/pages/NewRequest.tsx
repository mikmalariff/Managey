import { useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { api, type Holiday, type LeaveBalance, type LeaveType, type RequestType } from '../api'
import { useMe } from '../auth'
import { ErrorText, Field, PageHeader, Tabs, useFetch } from '../components/ui'
import { currencyCode, fmt, parseWorkDays } from '../format'

const types: { value: RequestType; label: string; hint: string }[] = [
  { value: 'Leave', label: 'Leave', hint: 'Time off from work' },
  { value: 'Claim', label: 'Claim', hint: 'Reimbursement for money you spent' },
  { value: 'Purchase', label: 'Purchase', hint: 'Ask the company to buy something' },
  { value: 'General', label: 'General', hint: 'Any other approval' },
]

/** Same rule as the server: the company's working days, inclusive, skipping public holidays. */
function workingDays(start: string, end: string, halfDay: boolean, holidays: Set<string>, workDays: Set<number>) {
  if (!start || !end || end < start) return 0
  let days = 0
  const d = new Date(`${start}T00:00:00`)
  const last = new Date(`${end}T00:00:00`)
  while (d <= last) {
    if (workDays.has(d.getDay()) && !holidays.has(fmt.isoDate(d))) days++
    d.setDate(d.getDate() + 1)
  }
  return halfDay && days === 1 ? 0.5 : days
}

export default function NewRequest() {
  const { me } = useMe()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [type, setType] = useState<RequestType>((params.get('type') as RequestType) || 'Leave')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [leaveTypeId, setLeaveTypeId] = useState('')
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [halfDay, setHalfDay] = useState(false)
  const [amount, setAmount] = useState('')
  const [files, setFiles] = useState<FileList | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const { data: leaveTypes } = useFetch<LeaveType[]>('/api/leave-types')
  const year = startDate ? Number(startDate.slice(0, 4)) : new Date().getFullYear()
  const { data: balances } = useFetch<LeaveBalance[]>(`/api/users/${me.id}/leave?year=${year}`)
  const { data: holidayList } = useFetch<Holiday[]>(`/api/holidays?year=${year}`)

  const selectedType = leaveTypes?.find(t => t.id === Number(leaveTypeId))
  const balance = balances?.find(b => b.leaveTypeId === Number(leaveTypeId))
  const holidaysInRange = useMemo(
    () => (holidayList ?? []).filter(h => h.date.slice(0, 10) >= startDate && h.date.slice(0, 10) <= endDate),
    [holidayList, startDate, endDate])
  const days = useMemo(
    () => workingDays(startDate, endDate, halfDay, new Set(holidaysInRange.map(h => h.date.slice(0, 10))), parseWorkDays(me.workDays)),
    [startDate, endDate, halfDay, holidaysInRange, me.workDays])

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    const form = new FormData()
    form.set('type', type)
    form.set('title', title)
    form.set('description', description)
    if (type === 'Leave') {
      form.set('leaveTypeId', leaveTypeId)
      form.set('startDate', startDate)
      form.set('endDate', endDate)
      form.set('halfDay', String(halfDay))
    }
    if (type === 'Claim' || type === 'Purchase') form.set('amount', amount)
    Array.from(files ?? []).forEach(f => form.append('files', f))

    try {
      const created = await api.post<{ id: number }>('/api/requests', form)
      navigate(`/requests/${created.id}`)
    } catch (err) {
      setError((err as Error).message)
      setBusy(false)
    }
  }

  return (
    <div className="stack">
      <PageHeader title="New request" subtitle={types.find(t => t.value === type)?.hint} />
      <Tabs value={type} options={types} onChange={setType} />

      <form className="form" onSubmit={submit}>
        {type === 'Leave' ? (
          <>
            <Field label="Leave type">
              <select value={leaveTypeId} onChange={e => setLeaveTypeId(e.target.value)} required>
                <option value="">Select…</option>
                {leaveTypes?.filter(t => t.isActive).map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
              </select>
            </Field>
            <div className="grid-2">
              <Field label="From">
                <input type="date" value={startDate} required
                  onChange={e => { setStartDate(e.target.value); if (!endDate || endDate < e.target.value) setEndDate(e.target.value) }} />
              </Field>
              <Field label="To">
                <input type="date" value={endDate} min={startDate} required onChange={e => setEndDate(e.target.value)} />
              </Field>
            </div>
            {startDate && startDate === endDate && (
              <label className="check">
                <input type="checkbox" checked={halfDay} onChange={e => setHalfDay(e.target.checked)} /> Half day
              </label>
            )}
            {startDate && endDate && (
              <p className="muted">
                {fmt.days(days)} (working days only{holidaysInRange.length > 0 && `, excluding ${holidaysInRange.map(h => h.name).join(', ')}`})
                {balance && ` · ${balance.remaining} ${balance.leaveType} day(s) available`}
              </p>
            )}
            <Field label="Reason">
              <textarea value={description} onChange={e => setDescription(e.target.value)} />
            </Field>
          </>
        ) : (
          <>
            <Field label="Title">
              <input value={title} onChange={e => setTitle(e.target.value)} required
                placeholder={type === 'Claim' ? 'e.g. Travel to client site' : type === 'Purchase' ? 'e.g. Printer toner' : ''} />
            </Field>
            {(type === 'Claim' || type === 'Purchase') && (
              <Field label={`Amount (${currencyCode()})`}>
                <input type="number" min="0.01" step="0.01" value={amount} onChange={e => setAmount(e.target.value)} required />
              </Field>
            )}
            <Field label="Details">
              <textarea value={description} onChange={e => setDescription(e.target.value)} />
            </Field>
          </>
        )}

        <Field label="Attachments" hint={
          selectedType?.requiresAttachment && type === 'Leave' ? 'Required for this leave type. Up to 5 files, 20 MB each.'
            : type === 'Claim' ? 'Attach your receipts. Up to 5 files, 20 MB each.'
            : 'Optional. Up to 5 files, 20 MB each.'}>
          <input type="file" multiple onChange={e => setFiles(e.target.files)} />
        </Field>

        <ErrorText error={error} />
        <div className="form-actions">
          <button className="primary" disabled={busy}>{busy ? 'Submitting…' : 'Submit request'}</button>
          <button type="button" onClick={() => navigate(-1)}>Cancel</button>
        </div>
      </form>
    </div>
  )
}
