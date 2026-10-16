const dateFmt = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
const timeFmt = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit' })
const money = new Intl.NumberFormat('en-MY', { style: 'currency', currency: 'MYR' })

/** Calendar dates are sent as midnight UTC; read only the date part so time zones don't shift them. */
function calendarDate(value: string) {
  const [y, m, d] = value.slice(0, 10).split('-').map(Number)
  return new Date(y, m - 1, d)
}

export const fmt = {
  date: (value: string | null | undefined) => (value ? dateFmt.format(calendarDate(value)) : '—'),
  dateTime: (value: string | null | undefined) =>
    value ? `${dateFmt.format(new Date(value))}, ${timeFmt.format(new Date(value))}` : '—',
  time: (value: string | null | undefined) => (value ? timeFmt.format(new Date(value)) : '—'),
  money: (value: number | null | undefined) => (value == null ? '—' : money.format(value)),
  days: (value: number | null | undefined) => (value == null ? '—' : `${value} ${value === 1 ? 'day' : 'days'}`),
  size: (bytes: number) =>
    bytes < 1024 ? `${bytes} B` : bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(0)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`,
  isoDate: (d: Date) =>
    `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`,
}

export const monthName = (year: number, month: number) =>
  new Intl.DateTimeFormat('en-GB', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1))

/** Plain number with two decimals and thousands separators, for tables of money. */
export const amount = (value: number) =>
  value.toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export const approverLabel: Record<string, string> = {
  DirectManager: 'Direct manager',
  HR: 'HR',
  Admin: 'Admin',
  SpecificUser: 'Specific person',
}

/** Earnings and deductions grouped the way they are reported on the EA form. */
export const earningCategories: { value: string; label: string }[] = [
  { value: 'Allowance', label: 'Allowance (taxable)' },
  { value: 'Salary', label: 'Salary / overtime' },
  { value: 'Bonus', label: 'Bonus / commission' },
  { value: 'Exempt', label: 'Tax-exempt' },
  { value: 'Reimbursement', label: 'Reimbursement' },
]

export const deductionCategories: { value: string; label: string }[] = [
  { value: 'Other', label: 'Other deduction' },
  { value: 'Zakat', label: 'Zakat' },
  { value: 'Cp38', label: 'CP38' },
  { value: 'Salary', label: 'Reduces salary (e.g. unpaid leave)' },
]
