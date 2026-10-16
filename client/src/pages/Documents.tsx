import { useState, type FormEvent } from 'react'
import { api, type DocumentRow } from '../api'
import { useMe } from '../auth'
import { Empty, ErrorText, Field, Loading, Modal, PageHeader, useFetch } from '../components/ui'
import { fmt } from '../format'

export default function Documents() {
  const { isHR } = useMe()
  const { data, error, reload } = useFetch<DocumentRow[]>('/api/documents')
  const [uploading, setUploading] = useState(false)

  const groups = new Map<string, DocumentRow[]>()
  data?.forEach(d => {
    const key = d.category ?? 'General'
    groups.set(key, [...(groups.get(key) ?? []), d])
  })

  const remove = async (doc: DocumentRow) => {
    if (!confirm(`Delete ${doc.fileName}?`)) return
    await api.del(`/api/documents/${doc.id}`)
    reload()
  }

  return (
    <div className="stack">
      <PageHeader
        title="Documents"
        subtitle="Company policies, forms and handbooks"
        actions={isHR && <button className="primary" onClick={() => setUploading(true)}>Upload</button>}
      />
      <ErrorText error={error} />
      {!data ? <Loading /> : data.length === 0 ? <Empty>No documents yet.</Empty> : (
        [...groups.entries()].map(([category, docs]) => (
          <section key={category}>
            <h3 className="muted" style={{ marginBottom: 8 }}>{category}</h3>
            <div className="table-wrap">
              <table>
                <tbody>
                  {docs.map(d => (
                    <tr key={d.id}>
                      <td><a href={`/api/documents/${d.id}/download`}>{d.fileName}</a></td>
                      <td className="muted nowrap">{fmt.size(d.size)}</td>
                      <td className="muted nowrap">{fmt.date(d.uploadedAt)} · {d.uploadedBy}</td>
                      {isHR && <td className="right"><button className="link" onClick={() => remove(d)}>Delete</button></td>}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        ))
      )}
      {uploading && (
        <UploadModal categories={[...groups.keys()]} onClose={() => setUploading(false)} onDone={() => { setUploading(false); reload() }} />
      )}
    </div>
  )
}

function UploadModal({ categories, onClose, onDone }: { categories: string[]; onClose: () => void; onDone: () => void }) {
  const [file, setFile] = useState<File | null>(null)
  const [category, setCategory] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!file) return
    setBusy(true)
    const form = new FormData()
    form.set('file', file)
    form.set('category', category)
    try {
      await api.post('/api/documents', form)
      onDone()
    } catch (err) {
      setError((err as Error).message)
      setBusy(false)
    }
  }

  return (
    <Modal title="Upload document" onClose={onClose}>
      <form className="form" onSubmit={submit}>
        <Field label="File" hint="PDF, Office documents or images, up to 20 MB">
          <input type="file" onChange={e => setFile(e.target.files?.[0] ?? null)} required />
        </Field>
        <Field label="Category">
          <input list="doc-categories" value={category} onChange={e => setCategory(e.target.value)} placeholder="e.g. Policies, Forms" />
          <datalist id="doc-categories">{categories.map(c => <option key={c} value={c} />)}</datalist>
        </Field>
        <ErrorText error={error} />
        <div className="form-actions">
          <button className="primary" disabled={busy || !file}>{busy ? 'Uploading…' : 'Upload'}</button>
          <button type="button" onClick={onClose}>Cancel</button>
        </div>
      </form>
    </Modal>
  )
}
