const dateFmt = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
const timeFmt = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit' })
let currency = 'MYR'
let money = new Intl.NumberFormat('en-MY', { style: 'currency', currency })

/** Company currency (ISO code), set once the signed-in user is loaded. */
export function setCurrency(code: string) {
  try {
    money = new Intl.NumberFormat(undefined, { style: 'currency', currency: code })
    currency = code
  } catch {
    // unknown code: keep the previous one
  }
}

export const currencyCode = () => currency

/** Working days as JS day numbers (Sunday = 0), from the company setting "1,2,3,4,5". */
export const parseWorkDays = (value: string) =>
  new Set(value.split(',').map(v => Number(v.trim())).filter(n => Number.isInteger(n) && n >= 0 && n <= 6))

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
  value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export const approverLabel: Record<string, string> = {
  DirectManager: 'Direct manager',
  HR: 'HR',
  Admin: 'Admin',
  SpecificUser: 'Specific person',
}
