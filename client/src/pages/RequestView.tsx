import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { api, type RequestDetail } from '../api'
import { ErrorText, Field, Loading, Modal, PageHeader, Status, useFetch } from '../components/ui'
import { approverLabel, fmt } from '../format'

export default function RequestView() {
  const { id } = useParams()
  const { data: r, error, reload } = useFetch<RequestDetail>(`/api/requests/${id}`)
  const [deciding, setDeciding] = useState<'approve' | 'reject' | null>(null)

  if (error) return <p className="error">{error}</p>
  if (!r) return <Loading />

  const cancel = async () => {
    if (!confirm('Cancel this request?')) return
    await api.post(`/api/requests/${r.id}/cancel`)
    reload()
  }

  return (
    <div className="stack">
      <PageHeader
        title={r.title}
        subtitle={<><span className="mono">{r.refNo}</span> · {r.type} · <Status value={r.status} /></>}
        actions={<>
          {r.canCancel && <button className="danger" onClick={cancel}>Cancel request</button>}
          {r.canAct && <>
            <button onClick={() => setDeciding('reject')}>Reject</button>
            <button className="primary" onClick={() => setDeciding('approve')}>Approve</button>
          </>}
        </>}
      />

      <div className="grid-2" style={{ gridTemplateColumns: 'minmax(0, 3fr) minmax(0, 2fr)', alignItems: 'start' }}>
        <section className="card">
          <dl className="details">
            <dt>Requested by</dt>
            <dd>{r.requester.fullName} <span className="faint mono">{r.requester.employeeNo}</span></dd>
            <dt>Submitted</dt>
            <dd>{fmt.dateTime(r.createdAt)}</dd>
            {r.type === 'Leave' && <>
              <dt>Leave type</dt>
              <dd>{r.leaveType}</dd>
              <dt>Dates</dt>
              <dd>{fmt.date(r.startDate)}{r.endDate !== r.startDate && ` – ${fmt.date(r.endDate)}`}{r.halfDay && ' (half day)'}</dd>
              <dt>Working days</dt>
              <dd>{fmt.days(r.days)}</dd>
            </>}
            {r.amount != null && <>
              <dt>Amount</dt>
              <dd>{fmt.money(r.amount)}</dd>
            </>}
            <dt>{r.type === 'Leave' ? 'Reason' : 'Details'}</dt>
            <dd style={{ whiteSpace: 'pre-wrap' }}>{r.description || <span className="faint">—</span>}</dd>
            <dt>Attachments</dt>
            <dd>
              {r.attachments.length === 0 ? <span className="faint">None</span> : r.attachments.map(a => (
                <div key={a.id}>
                  <a href={`/api/documents/${a.id}/download`}>{a.fileName}</a> <span className="faint">{fmt.size(a.size)}</span>
                </div>
              ))}
            </dd>
            {r.completedAt && <>
              <dt>Closed</dt>
              <dd>{fmt.dateTime(r.completedAt)}</dd>
            </>}
          </dl>
        </section>

        <section className="card">
          <h2>Approval</h2>
          {r.steps.length === 0 ? <p className="muted">No approval needed.</p> : (
            <ol className="timeline">
              {r.steps.map(s => (
                <li key={s.id} className={s.status.toLowerCase()}>
                  <div className="spread">
                    <span>{s.approver ?? approverLabel[s.approverKind]}</span>
                    <Status value={s.status} />
                  </div>
                  {s.actedBy && (
                    <div className="faint" style={{ fontSize: 12 }}>
                      {s.actedBy}{s.approver && s.actedBy !== s.approver && ' (on behalf)'} · {fmt.dateTime(s.actedAt)}
                    </div>
                  )}
                  {s.status === 'Skipped' && !s.actedBy && <div className="faint" style={{ fontSize: 12 }}>Not required</div>}
                  {s.comment && <div className="comment">{s.comment}</div>}
                </li>
              ))}
            </ol>
          )}
        </section>
      </div>

      {deciding && (
        <DecisionModal id={r.id} action={deciding} onClose={() => setDeciding(null)} onDone={() => { setDeciding(null); reload() }} />
      )}
    </div>
  )
}

function DecisionModal({ id, action, onClose, onDone }: {
  id: number
  action: 'approve' | 'reject'
  onClose: () => void
  onDone: () => void
}) {
  const [comment, setComment] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async () => {
    setBusy(true)
    try {
      await api.post(`/api/requests/${id}/${action}`, { comment })
      onDone()
    } catch (e) {
      setError((e as Error).message)
      setBusy(false)
    }
  }

  return (
    <Modal title={action === 'approve' ? 'Approve request' : 'Reject request'} onClose={onClose}>
      <div className="form">
        <Field label={action === 'approve' ? 'Comment (optional)' : 'Reason'}>
          <textarea value={comment} onChange={e => setComment(e.target.value)} autoFocus />
        </Field>
        <ErrorText error={error} />
        <div className="form-actions">
          <button className="primary" disabled={busy} onClick={submit}>{action === 'approve' ? 'Approve' : 'Reject'}</button>
          <button onClick={onClose}>Cancel</button>
        </div>
      </div>
    </Modal>
  )
}
