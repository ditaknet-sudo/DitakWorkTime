const TOKEN_KEY = 'attendance_token'

export type AuthUser = {
  id: string
  email: string
  displayName: string
  preferredLanguage: string
  themePreference: string
  employeeId?: string | null
  roles: string[]
}

function apiBase() {
  return import.meta.env.VITE_API_BASE || '/api'
}

export function getToken() {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string | null) {
  if (token) localStorage.setItem(TOKEN_KEY, token)
  else localStorage.removeItem(TOKEN_KEY)
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Content-Type', 'application/json')
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const res = await fetch(`${apiBase()}${path}`, { ...init, headers })
  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || res.statusText)
  }
  if (res.status === 204) return undefined as T
  const ct = res.headers.get('content-type') || ''
  if (ct.includes('application/json')) return res.json()
  return res.blob() as Promise<T>
}

export const api = {
  login: (email: string, password: string) =>
    request<{ token: string; user: AuthUser }>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),
  me: () => request<AuthUser>('/me'),
  preferences: (language?: string, theme?: string) =>
    request<AuthUser>('/me/preferences', {
      method: 'PUT',
      body: JSON.stringify({ language, theme }),
    }),
  today: () =>
    request<{
      employeeId: string
      isCheckedIn: boolean
      workedMinutesToday: number
      hasOpenShift: boolean
      lastEventAtUtc?: string
      lastEventType?: number
    }>('/attendance/me/today'),
  checkIn: (source: 'Web' | 'Qr' = 'Web') =>
    request('/attendance/check-in', {
      method: 'POST',
      body: JSON.stringify({ source, idempotencyKey: crypto.randomUUID() }),
    }),
  checkOut: (source: 'Web' | 'Qr' = 'Web') =>
    request('/attendance/check-out', {
      method: 'POST',
      body: JSON.stringify({ source, idempotencyKey: crypto.randomUUID() }),
    }),
  presence: () =>
    request<
      Array<{
        employeeId: string
        fullName: string
        department?: string
        officiallyCheckedIn: boolean
        seenOnNetwork: boolean
        lastSeenAtUtc?: string
        clientIp?: string
      }>
    >('/attendance/presence'),
  heartbeat: () =>
    request('/devices/heartbeat', {
      method: 'POST',
      body: JSON.stringify({}),
    }),
  monthly: (employeeId: string, year: number, month: number) =>
    request<{
      employeeId: string
      fullName: string
      year: number
      month: number
      totalMinutes: number
      days: Array<{ workDate: string; workedMinutes: number; hasOpenShift: boolean; workedHoursDisplay: string }>
    }>(`/reports/employees/${employeeId}/monthly?year=${year}&month=${month}`),
  exportUrl: (employeeId: string, year: number, month: number, format: 'xlsx' | 'pdf') =>
    `${apiBase()}/reports/export?employeeId=${employeeId}&year=${year}&month=${month}&format=${format}`,
}

export function formatHours(minutes: number) {
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return `${h}:${m.toString().padStart(2, '0')}`
}
