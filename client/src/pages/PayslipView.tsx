import { Link, useParams } from 'react-router-dom'
import type { PayslipDetail } from '../api'
import { useMe } from '../auth'
import { Loading, useFetch } from '../components/ui'
import { amount, currencyCode, monthName } from '../format'

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
          <div><dt>{p.icNumber || !p.passportNo ? 'ID no.' : 'Passport no.'}</dt><dd>{p.icNumber ?? p.passportNo ?? '—'}</dd></div>
          <div><dt>Department</dt><dd>{p.department ?? '—'}</dd></div>
          <div><dt>Position</dt><dd>{p.position ?? '—'}</dd></div>
          <div><dt>Tax no.</dt><dd>{p.taxNo ?? '—'}</dd></div>
          <div><dt>Bank</dt><dd>{p.bankName ? `${p.bankName} ${p.bankAccountNo ?? ''}` : '—'}</dd></div>
        </dl>

        <div className="payslip-cols">
          <table>
            <thead><tr><th>Earnings</th><th className="right">{currencyCode()}</th></tr></thead>
            <tbody>
              <tr><td>Basic salary</td><td className="right mono">{amount(p.basicSalary)}</td></tr>
              {earnings.map((l, i) => <tr key={i}><td>{l.name}</td><td className="right mono">{amount(l.amount)}</td></tr>)}
            </tbody>
            <tfoot><tr><td>Gross pay</td><td className="right mono">{amount(p.grossPay)}</td></tr></tfoot>
          </table>
          <table>
            <thead><tr><th>Deductions</th><th className="right">{currencyCode()}</th></tr></thead>
            <tbody>
              {deductions.map((l, i) => <tr key={i}><td>{l.name}</td><td className="right mono">{amount(l.amount)}</td></tr>)}
              {p.contributions.filter(c => c.employee > 0).map((c, i) => (
                <tr key={`c${i}`}>
                  <td>{c.name}{c.memberNo && <span className="faint"> · {c.memberNo}</span>}</td>
                  <td className="right mono">{amount(c.employee)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot><tr><td>Total deductions</td><td className="right mono">{amount(p.totalDeductions)}</td></tr></tfoot>
          </table>
        </div>

        <div className="payslip-net">
          <span>Net pay</span>
          <strong className="mono">{currencyCode()} {amount(p.netPay)}</strong>
        </div>

        {p.contributions.some(c => c.employer > 0) && (
          <div className="payslip-employer">
            <span className="muted">Employer contributions</span>
            {p.contributions.filter(c => c.employer > 0).map((c, i) => <span key={i}>{c.code} {amount(c.employer)}</span>)}
          </div>
        )}

        <p className="faint payslip-foot">This is a computer-generated payslip. No signature is required.</p>
      </article>
    </div>
  )
}
