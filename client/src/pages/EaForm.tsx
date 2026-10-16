import { Fragment, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api'
import { useMe } from '../auth'
import { Loading, useFetch } from '../components/ui'
import { amount, fmt } from '../format'

export interface EaData {
  year: number
  serialNo: string
  employer: Record<string, string | null>
  employee: {
    name: string
    position: string | null
    employeeNo: string
    icNumber: string | null
    passportNo: string | null
    epfNo: string | null
    socsoNo: string | null
    taxNo: string | null
    joinDate: string | null
    months: number
  }
  amounts: {
    salary: number
    bonus: number
    allowances: number
    totalIncome: number
    pcb: number
    cp38: number
    zakat: number
    epf: number
    perkeso: number
    exempt: number
  }
}

/** One employee's Borang EA (C.P.8A), laid out like the LHDN form. */
export function EaSheet({ ea }: { ea: EaData }) {
  const e = ea.employee
  const a = ea.amounts
  const er = ea.employer
  const row = (no: string, label: string, value: number | null, indent = false) => (
    <tr>
      <td className="ea-no">{no}</td>
      <td style={indent ? { paddingLeft: 24 } : undefined}>{label}</td>
      <td className="right mono">{value == null ? '' : amount(value)}</td>
    </tr>
  )

  return (
    <article className="ea">
      <header className="ea-head">
        <div className="ea-meta">
          <div><span>No. Siri</span>{ea.serialNo}</div>
          <div><span>No. Majikan E</span>{er.EmployerTaxNo ?? '—'}</div>
          <div><span>No. Cukai Pendapatan Pekerja</span>{e.taxNo ?? '—'}</div>
        </div>
        <div className="ea-title">
          <div className="ea-code">EA</div>
          <div>
            <strong>PENYATA SARAAN DARIPADA PENGGAJIAN</strong>
            <div>BAGI TAHUN BERAKHIR 31 DISEMBER {ea.year}</div>
            <div className="muted">C.P.8A · Statement of remuneration from employment</div>
          </div>
        </div>
        <p className="ea-note">BORANG EA INI PERLU DISEDIAKAN UNTUK DISERAHKAN KEPADA PEKERJA BAGI TUJUAN CUKAI PENDAPATANNYA</p>
      </header>

      <section>
        <h3 className="ea-section">A &nbsp; BUTIRAN PEKERJA</h3>
        <dl className="ea-details">
          <dt>1. Nama penuh pekerja/pesara</dt><dd>{e.name}</dd>
          <dt>2. Jawatan</dt><dd>{e.position ?? '—'}</dd>
          <dt>3. No. kakitangan / No. gaji</dt><dd>{e.employeeNo}</dd>
          <dt>4. No. K.P. Baru</dt><dd>{e.icNumber ?? '—'}</dd>
          <dt>5. No. Pasport</dt><dd>{e.passportNo ?? '—'}</dd>
          <dt>6. No. KWSP</dt><dd>{e.epfNo ?? '—'}</dd>
          <dt>7. No. PERKESO</dt><dd>{e.socsoNo ?? e.icNumber ?? '—'}</dd>
          <dt>8. Bilangan anak yang layak untuk pelepasan cukai</dt><dd>—</dd>
          <dt>9. Jika bekerja tidak genap setahun, nyatakan tarikh mula bekerja</dt><dd>{e.joinDate ? fmt.date(e.joinDate) : '—'}</dd>
        </dl>
      </section>

      <table className="ea-table">
        <tbody>
          <tr className="ea-head-row"><th colSpan={2}>B &nbsp; PENDAPATAN PENGGAJIAN, MANFAAT DAN TEMPAT KEDIAMAN</th><th className="right">RM</th></tr>
          {row('1. (a)', 'Gaji kasar, upah atau gaji cuti (termasuk gaji lebih masa)', a.salary)}
          {row('(b)', 'Fi (termasuk fi pengarah), komisen atau bonus', a.bonus, true)}
          {row('(c)', 'Tip kasar, perkuisit, penerimaan sagu hati atau elaun-elaun lain', a.allowances, true)}
          {row('(d)', 'Cukai pendapatan yang dibayar oleh majikan bagi pihak pekerja', 0, true)}
          {row('(e)', 'Manfaat Skim Opsyen Saham Pekerja (ESOS)', 0, true)}
          {row('(f)', 'Ganjaran', 0, true)}
          {row('2.', 'Butiran bayaran tunggakan dan lain-lain bagi tahun-tahun terdahulu', 0)}
          {row('3.', 'Manfaat berupa barangan', 0)}
          {row('4.', 'Nilai tempat kediaman', 0)}
          {row('5.', 'Bayaran balik daripada Kumpulan Wang Simpanan/Pencen yang tidak diluluskan', 0)}
          {row('6.', 'Pampasan kerana kehilangan pekerjaan', 0)}
        <tr className="ea-head-row"><th colSpan={3}>C &nbsp; PENCEN DAN LAIN-LAIN</th></tr>
          {row('1.', 'Pencen', 0)}
          {row('2.', 'Anuiti atau bayaran berkala yang lain', 0)}
          <tr className="ea-total"><td></td><td>JUMLAH</td><td className="right mono">{amount(a.totalIncome)}</td></tr>
        <tr className="ea-head-row"><th colSpan={3}>D &nbsp; JUMLAH POTONGAN</th></tr>
          {row('1.', 'Potongan Cukai Bulanan (PCB) yang dibayar kepada LHDNM', a.pcb)}
          {row('2.', 'Arahan potongan CP38', a.cp38)}
          {row('3.', 'Zakat yang dibayar melalui potongan gaji', a.zakat)}
          {row('4.', 'Jumlah tuntutan potongan oleh pekerja melalui Borang TP1', null)}
          {row('5.', 'Jumlah pelepasan bagi anak yang layak', null)}
        <tr className="ea-head-row"><th colSpan={3}>E &nbsp; CARUMAN YANG DIBAYAR OLEH PEKERJA (BAHAGIAN PEKERJA SAHAJA)</th></tr>
          {row('1.', 'Kumpulan Wang Simpanan Pekerja (KWSP)', a.epf)}
          {row('2.', 'PERKESO (PERKESO, SIP dan LINDUNG 24 Jam)', a.perkeso)}
        <tr className="ea-head-row"><th colSpan={3}>F &nbsp; JUMLAH ELAUN / PERKUISIT / PEMBERIAN / MANFAAT YANG DIKECUALIKAN CUKAI</th></tr>
          {row('', 'Jumlah', a.exempt)}
        </tbody>
      </table>

      <footer className="ea-foot">
        <div><span>Nama pegawai</span>{er.EaSignatoryName ?? ''}</div>
        <div><span>Jawatan</span>{er.EaSignatoryPosition ?? ''}</div>
        <div><span>Nama dan alamat majikan</span>{er.CompanyName}{er.CompanyAddress ? `, ${er.CompanyAddress}` : ''}</div>
        <div><span>No. telefon majikan</span>{er.CompanyPhone ?? ''}</div>
        <div><span>Tarikh</span>{fmt.date(fmt.isoDate(new Date()))}</div>
      </footer>
    </article>
  )
}

export default function EaView() {
  const { year, userId } = useParams()
  const { me, isHR } = useMe()
  const { data, error } = useFetch<EaData>(`/api/ea/${year}/${userId}`)

  if (error) return <p className="error">{error === 'Request failed (404).' ? `No finalised payslips for ${year}.` : error}</p>
  if (!data) return <Loading />

  return (
    <div className="stack">
      <div className="spread no-print">
        {isHR && Number(userId) !== me.id ? <Link to={`/payroll/ea?year=${year}`} className="muted">← EA forms {year}</Link> : <Link to="/payslips" className="muted">← Payslips</Link>}
        <button className="primary" onClick={() => window.print()}>Print / Save as PDF</button>
      </div>
      <EaSheet ea={data} />
    </div>
  )
}

/** Every employee's EA for a year, one per printed page. */
export function EaPrintAll() {
  const { year } = useParams()
  const [forms, setForms] = useState<EaData[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    (async () => {
      try {
        const list = await api.get<{ userId: number }[]>(`/api/ea/${year}`)
        setForms(await Promise.all(list.map(r => api.get<EaData>(`/api/ea/${year}/${r.userId}`))))
      } catch (e) {
        setError((e as Error).message)
      }
    })()
  }, [year])

  if (error) return <p className="error">{error}</p>
  if (!forms) return <Loading />
  return (
    <div className="stack">
      <div className="spread no-print">
        <Link to={`/payroll/ea?year=${year}`} className="muted">← EA forms {year}</Link>
        <button className="primary" onClick={() => window.print()}>Print all ({forms.length})</button>
      </div>
      {forms.map((ea, i) => (
        <Fragment key={ea.serialNo}>
          {i > 0 && <div className="page-break" />}
          <EaSheet ea={ea} />
        </Fragment>
      ))}
    </div>
  )
}
