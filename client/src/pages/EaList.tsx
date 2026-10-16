import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Empty, ErrorText, Loading, PageHeader, useFetch } from '../components/ui'
import { amount } from '../format'

interface EaRow { userId: number; employeeNo: string; employeeName: string; taxNo: string | null; months: number; totalIncome: number; pcb: number; epf: number }

export default function EaList() {
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()
  const thisYear = new Date().getFullYear()
  const year = Number(params.get('year')) || thisYear - (new Date().getMonth() < 3 ? 1 : 0)
  const { data, error } = useFetch<EaRow[]>(`/api/ea/${year}`)

  return (
    <div className="stack">
      <PageHeader
        title="EA forms"
        subtitle="Borang EA (C.P.8A) for each employee, built from finalised payslips. Give them to staff by end of February."
        actions={<>
          <select value={year} onChange={e => setParams({ year: e.target.value })} style={{ width: 'auto' }}>
            {[thisYear, thisYear - 1, thisYear - 2].map(y => <option key={y}>{y}</option>)}
          </select>
          {data && data.length > 0 && <Link className="button primary" to={`/ea/${year}/all`}>Print all</Link>}
        </>}
      />
      <ErrorText error={error} />
      {!data ? <Loading /> : data.length === 0 ? <Empty>No finalised payroll in {year}.</Empty> : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Employee</th><th>Tax no.</th><th className="right">Months</th><th className="right">Total income (RM)</th><th className="right">PCB (RM)</th><th className="right">EPF (RM)</th></tr>
            </thead>
            <tbody>
              {data.map(r => (
                <tr key={r.userId} className="clickable" onClick={() => navigate(`/ea/${year}/${r.userId}`)}>
                  <td>{r.employeeName} <span className="faint mono">{r.employeeNo}</span></td>
                  <td className={r.taxNo ? 'mono' : 'faint'}>{r.taxNo ?? 'missing'}</td>
                  <td className="right mono">{r.months}</td>
                  <td className="right mono">{amount(r.totalIncome)}</td>
                  <td className="right mono">{amount(r.pcb)}</td>
                  <td className="right mono">{amount(r.epf)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <p className="faint" style={{ fontSize: 12 }}>
        Amounts come from each pay line's EA category (salary, bonus, allowance, tax-exempt, zakat, CP38). Claims are
        reimbursements and are left out. Check the figures before handing the forms out; sections not tracked here, such as
        benefits in kind, show zero.
      </p>
    </div>
  )
}
