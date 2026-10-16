export type Role = 'Admin' | 'HR' | 'Manager' | 'Staff'
export type RequestType = 'Leave' | 'Claim' | 'Purchase' | 'General'
export type RequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Cancelled'
export type StepStatus = 'Waiting' | 'Pending' | 'Approved' | 'Rejected' | 'Skipped'
export type ApproverKind = 'DirectManager' | 'HR' | 'Admin' | 'SpecificUser'

export interface Me {
  id: number
  username: string
  fullName: string
  employeeNo: string
  role: Role
  department: string | null
  position: string | null
  isManager: boolean
  companyName: string | null
}

export interface UserRow {
  id: number
  employeeNo: string
  username: string
  fullName: string
  email: string | null
  phone: string | null
  role: Role
  departmentId: number | null
  department: string | null
  position: string | null
  managerId: number | null
  manager: string | null
  joinDate: string | null
  isActive: boolean
  lastLoginAt: string | null
}

export interface Department { id: number; name: string; headcount: number }

export interface LeaveType { id: number; name: string; defaultDays: number; requiresAttachment: boolean; isActive: boolean }

export interface LeaveBalance {
  leaveTypeId: number
  leaveType: string
  entitlement: number
  used: number
  pending: number
  remaining: number
}

export interface RequestRow {
  id: number
  refNo: string
  type: RequestType
  title: string
  status: RequestStatus
  createdAt: string
  completedAt: string | null
  requester: string
  leaveType: string | null
  startDate: string | null
  endDate: string | null
  days: number | null
  amount: number | null
  currentStep: string | null
}

export interface RequestDetail {
  id: number
  refNo: string
  type: RequestType
  title: string
  description: string | null
  status: RequestStatus
  createdAt: string
  completedAt: string | null
  requester: { id: number; fullName: string; employeeNo: string }
  leaveType: string | null
  startDate: string | null
  endDate: string | null
  halfDay: boolean
  days: number | null
  amount: number | null
  steps: {
    id: number
    stepOrder: number
    approverKind: ApproverKind
    status: StepStatus
    actedAt: string | null
    comment: string | null
    approver: string | null
    actedBy: string | null
  }[]
  attachments: { id: number; fileName: string; size: number; uploadedAt: string }[]
  canAct: boolean
  canCancel: boolean
}

export interface AttendanceRow {
  id: number
  userId: number
  employeeNo: string
  name: string
  date: string
  clockIn: string
  clockOut: string | null
  hours: number | null
  late: boolean
  note: string | null
}

export interface DocumentRow {
  id: number
  fileName: string
  category: string | null
  size: number
  uploadedAt: string
  uploadedBy: string
}

export interface AuditRow {
  id: number
  timestamp: string
  username: string
  action: string
  entityType: string
  entityId: string | null
  details: string | null
}

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message)
  }
}

let onUnauthorized: () => void = () => {}
export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const init: RequestInit = { method, credentials: 'same-origin', headers: {} }
  if (body instanceof FormData) {
    init.body = body
  } else if (body !== undefined) {
    init.body = JSON.stringify(body)
    init.headers = { 'Content-Type': 'application/json' }
  }

  const res = await fetch(url, init)
  if (res.status === 401 && !url.endsWith('/auth/login')) onUnauthorized()
  if (!res.ok) {
    let message = res.status === 403 ? 'You do not have access to this.' : `Request failed (${res.status}).`
    try {
      const data = await res.json()
      if (data?.message) message = data.message
      else if (data?.title) message = data.title
    } catch {
      // response had no JSON body
    }
    throw new ApiError(res.status, message)
  }
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(url: string) => request<T>('GET', url),
  post: <T = void>(url: string, body?: unknown) => request<T>('POST', url, body ?? {}),
  put: <T = void>(url: string, body?: unknown) => request<T>('PUT', url, body ?? {}),
  del: <T = void>(url: string) => request<T>('DELETE', url),
}
