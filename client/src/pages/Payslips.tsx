import { Link, useNavigate } from 'react-router-dom'
import { useMe } from '../auth'
import { Empty, ErrorText, Loading, PageHeader, useFetch } from '../components/ui'
import { amount, monthName } from '../format'

interface MyPayslip { id: number; year: number; month: number; grossPay: number; totalDeductions: number; netPay: number }

export default function Payslips() {
  const navigate = useNavigate()
  const { me } = useMe()
  const { data, error } = useFetch<MyPayslip[]>('/api/payslips/mine')
  const { data: eaYears } = useFetch<number[]>('/api/ea/mine')

  return (
    <div className="stack">
      <PageHeader title="Payslips" subtitle="Your monthly payslips. Open one to print or save it as PDF." />
      <ErrorText error={error} />
      {!data ? <Loading /> : data.length === 0 ? <Empty>No payslips yet. They appear here once HR finalises payroll.</Empty> : (
        <div className="table-wrap">
          <table>
            <thead><tr><th>Month</th><th className="right">Gross (RM)</th><th className="right">Deductions (RM)</th><th className="right">Net pay (RM)</th></tr></thead>
            <tbody>
              {data.map(p => (
                <tr key={p.id} className="clickable" onClick={() => navigate(`/payslips/${p.id}`)}>
                  <td>{monthName(p.year, p.month)}</td>
                  <td className="right mono">{amount(p.grossPay)}</td>
                  <td className="right mono">{amount(p.totalDeductions)}</td>
                  <td className="right mono"><strong>{amount(p.netPay)}</strong></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {eaYears && eaYears.length > 0 && (
        <section>
          <h2 style={{ marginBottom: 8 }}>EA forms</h2>
          <p className="muted" style={{ marginBottom: 12 }}>Your yearly statement of remuneration (Borang EA), for filing your income tax.</p>
          <div className="row">
            {eaYears.map(y => <Link key={y} className="button" to={`/ea/${y}/${me.id}`}>EA {y}</Link>)}
          </div>
        </section>
      )}
    </div>
  )
}
