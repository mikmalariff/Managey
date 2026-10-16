import { Link, useParams } from 'react-router-dom'
import type { PayslipDetail } from '../api'
import { useMe } from '../auth'
import { Loading, useFetch } from '../components/ui'
import { amount, monthName } from '../format'

export default function PayslipView() {
  const { id } = useParams()
  const { isHR } = useMe()
  const { data: p, error } = useFetch<PayslipDetail>(`/api/payslips/${id}`)

  if (error) return <p className="error">{error === 'Request failed (404).' ? 'Payslip not found.' : error}</p>
  if (!p) return <Loading />

  const earnings = p.lines.filter(l => l.kind === 'Earning')
  const deductions = p.lines.filter(l => l.kind === 'Deduction')

  return (
    <div className="stack">
      <div className="spread no-print">
        {isHR ? <Link to={`/payroll/${p.payrollRunId}`} className="muted">← Back to payroll</Link> : <Link to="/payslips" className="muted">← All payslips</Link>}
        <button className="primary" onClick={() => window.print()}>Print / Save as PDF</button>
      </div>

      <article className="payslip">
        <header className="payslip-head">
          <div>
            <div className="payslip-company">{p.companyName}</div>
            <div className="muted">Payslip for {monthName(p.year, p.month)}</div>
          </div>
          {p.runStatus === 'Draft' && <span className="draft-tag">Draft</span>}
        </header>

        <dl className="payslip-info">
          <div><dt>Name</dt><dd>{p.employeeName}</dd></div>
          <div><dt>Employee no.</dt><dd>{p.employeeNo}</dd></div>
          <div><dt>IC no.</dt><dd>{p.icNumber ?? '—'}</dd></div>
          <div><dt>Department</dt><dd>{p.department ?? '—'}</dd></div>
          <div><dt>Position</dt><dd>{p.position ?? '—'}</dd></div>
          <div><dt>EPF no.</dt><dd>{p.epfNo ?? '—'}</dd></div>
          <div><dt>SOCSO no.</dt><dd>{p.socsoNo ?? '—'}</dd></div>
          <div><dt>Tax no.</dt><dd>{p.taxNo ?? '—'}</dd></div>
          <div><dt>Bank</dt><dd>{p.bankName ? `${p.bankName} ${p.bankAccountNo ?? ''}` : '—'}</dd></div>
        </dl>

        <div className="payslip-cols">
          <table>
            <thead><tr><th>Earnings</th><th className="right">RM</th></tr></thead>
            <tbody>
              <tr><td>Basic salary</td><td className="right mono">{amount(p.basicSalary)}</td></tr>
              {earnings.map((l, i) => <tr key={i}><td>{l.name}</td><td className="right mono">{amount(l.amount)}</td></tr>)}
            </tbody>
            <tfoot><tr><td>Gross pay</td><td className="right mono">{amount(p.grossPay)}</td></tr></tfoot>
          </table>
          <table>
            <thead><tr><th>Deductions</th><th className="right">RM</th></tr></thead>
            <tbody>
              {deductions.map((l, i) => <tr key={i}><td>{l.name}</td><td className="right mono">{amount(l.amount)}</td></tr>)}
              <tr><td>EPF (employee)</td><td className="right mono">{amount(p.epfEmployee)}</td></tr>
              <tr><td>SOCSO (employee)</td><td className="right mono">{amount(p.socsoEmployee)}</td></tr>
              <tr><td>EIS (employee)</td><td className="right mono">{amount(p.eisEmployee)}</td></tr>
              <tr><td>PCB / MTD</td><td className="right mono">{amount(p.pcb)}</td></tr>
            </tbody>
            <tfoot><tr><td>Total deductions</td><td className="right mono">{amount(p.totalDeductions)}</td></tr></tfoot>
          </table>
        </div>

        <div className="payslip-net">
          <span>Net pay</span>
          <strong className="mono">RM {amount(p.netPay)}</strong>
        </div>

        <div className="payslip-employer">
          <span className="muted">Employer contributions</span>
          <span>EPF {amount(p.epfEmployer)}</span>
          <span>SOCSO {amount(p.socsoEmployer)}</span>
          <span>EIS {amount(p.eisEmployer)}</span>
        </div>

        <p className="faint payslip-foot">This is a computer-generated payslip. No signature is required.</p>
      </article>
    </div>
  )
}
