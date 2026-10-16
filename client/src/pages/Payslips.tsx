import { useNavigate } from 'react-router-dom'
import { Empty, ErrorText, Loading, PageHeader, useFetch } from '../components/ui'
import { amount, currencyCode, monthName } from '../format'

interface MyPayslip { id: number; year: number; month: number; grossPay: number; totalDeductions: number; netPay: number }

export default function Payslips() {
  const navigate = useNavigate()
  const { data, error } = useFetch<MyPayslip[]>('/api/payslips/mine')

  return (
    <div className="stack">
      <PageHeader title="Payslips" subtitle="Your monthly payslips. Open one to print or save it as PDF." />
      <ErrorText error={error} />
      {!data ? <Loading /> : data.length === 0 ? <Empty>No payslips yet. They appear here once HR finalises payroll.</Empty> : (
        <div className="table-wrap">
          <table>
            <thead><tr><th>Month</th><th className="right">Gross ({currencyCode()})</th><th className="right">Deductions ({currencyCode()})</th><th className="right">Net pay ({currencyCode()})</th></tr></thead>
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
    </div>
  )
}
