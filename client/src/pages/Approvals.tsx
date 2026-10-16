import { useState } from 'react'
import type { RequestRow } from '../api'
import { Empty, ErrorText, Loading, PageHeader, Tabs, useFetch } from '../components/ui'
import { RequestTable } from './Requests'

export default function Approvals() {
  const [scope, setScope] = useState<'pending' | 'history'>('pending')
  const { data, error } = useFetch<RequestRow[]>(`/api/requests?scope=${scope}`)

  return (
    <div className="stack">
      <PageHeader title="Approvals" subtitle="Requests that need your decision" />
      <div>
        <Tabs value={scope} onChange={setScope} options={[
          { value: 'pending', label: 'Waiting for me' },
          { value: 'history', label: 'Decided by me' },
        ]} />
        <ErrorText error={error} />
        {!data ? <Loading /> : data.length === 0
          ? <Empty>{scope === 'pending' ? 'Nothing waiting for you.' : 'You have not decided on any requests yet.'}</Empty>
          : <RequestTable rows={data} showRequester />}
      </div>
    </div>
  )
}
