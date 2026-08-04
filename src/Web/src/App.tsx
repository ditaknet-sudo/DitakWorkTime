import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { api, formatHours, getInitials, getPrimaryRole, getRoleBadgeClass, getToken, type EmployeeListItem } from './api'
import { AuthProvider } from './auth'
import { useAuth } from './auth-context'
import './App.css'

/* ─── Role helpers ─── */
const PRIVILEGED = ['Admin', 'Manager', 'Director', 'Accountant']
const PRESENCE_ROLES = ['Admin', 'Manager', 'Director']
function isPrivileged(roles: string[]) { return roles.some(r => PRIVILEGED.includes(r)) }
function canViewPresence(roles: string[]) { return roles.some(r => PRESENCE_ROLES.includes(r)) }
function isOverviewOnly(roles: string[]) {
  return roles.some(role => role === 'Director' || role === 'Accountant') &&
    !roles.some(role => role === 'Admin' || role === 'Manager')
}

/* ─── Sidebar Navigation ─── */
function Sidebar() {
  const { t, i18n } = useTranslation()
  const { user, logout, setLanguage, setTheme } = useAuth()
  const location = useLocation()
  const active = (path: string) => location.pathname === path ? 'active' : ''
  const roles = user?.roles ?? []
  const primaryRole = getPrimaryRole(roles)
  const canSeePresence = canViewPresence(roles)
  const canSeeReports  = true

  return (
    <aside className="sidebar">
      <div className="sidebar-brand">
        <div className="brand-name">⏱ Ditak WorkTime</div>
        <div className="brand-sub">Monitoring</div>
      </div>
      <nav className="sidebar-nav">
        {!isOverviewOnly(roles) && (
          <Link to="/" className={active('/')}><span className="nav-icon">🏠</span>{t('dashboard')}</Link>
        )}
        {isOverviewOnly(roles) && (
          <Link to="/" className={active('/')}><span className="nav-icon">📊</span>{t('overview')}</Link>
        )}
        {canSeePresence && (
          <Link to="/presence" className={active('/presence')}><span className="nav-icon">👥</span>{t('presence')}</Link>
        )}
        {canSeeReports && (
          <Link to="/reports" className={active('/reports')}><span className="nav-icon">📋</span>{t('reports')}</Link>
        )}
        {!isPrivileged(roles) && (
          <Link to="/qr" className={active('/qr')}><span className="nav-icon">📷</span>{t('qr')}</Link>
        )}
      </nav>
      <div className="sidebar-footer">
        <div className="sidebar-user">
          <div className="avatar">{getInitials(user?.displayName ?? 'U')}</div>
          <div className="sidebar-user-info">
            <div className="user-name">{user?.displayName}</div>
            <div className="user-email">{user?.email}</div>
          </div>
        </div>
        <div style={{ marginBottom: '0.5rem' }}>
          <span className={`badge role-badge ${getRoleBadgeClass(primaryRole)}`}>{primaryRole}</span>
        </div>
        <div className="sidebar-actions controls">
          <select aria-label={t('language')} value={i18n.language}
            onChange={(e) => void setLanguage(e.target.value)}>
            <option value="en">EN</option>
            <option value="hy">ՀՅ</option>
            <option value="ru">RU</option>
          </select>
          <select aria-label={t('theme')}
            value={user?.themePreference || localStorage.getItem('theme') || 'system'}
            onChange={(e) => void setTheme(e.target.value as 'system' | 'light' | 'dark')}>
            <option value="system">{t('themeSystem')}</option>
            <option value="light">{t('themeLight')}</option>
            <option value="dark">{t('themeDark')}</option>
          </select>
          <button type="button" className="ghost" onClick={logout}>{t('logout')}</button>
        </div>
      </div>
    </aside>
  )
}

/* ─── Mobile Top Bar ─── */
function MobileTopBar() {
  const { t } = useTranslation()
  const { logout } = useAuth()
  return (
    <header className="topbar-mobile">
      <span className="brand-mobile">⏱ WorkTime</span>
      <button type="button" className="ghost" onClick={logout}>{t('logout')}</button>
    </header>
  )
}

/* ─── Mobile Bottom Nav ─── */
function MobileBottomNav() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const location = useLocation()
  const active = (path: string) => location.pathname === path ? 'active' : ''
  const roles = user?.roles ?? []
  return (
    <nav className="bottom-nav">
      <Link to="/" className={active('/')}><span className="nav-icon">🏠</span>{t('dashboard')}</Link>
      {canViewPresence(roles) && (
        <Link to="/presence" className={active('/presence')}><span className="nav-icon">👥</span>{t('presence')}</Link>
      )}
      <Link to="/reports" className={active('/reports')}><span className="nav-icon">📋</span>{t('reports')}</Link>
      {!isPrivileged(roles) && (
        <Link to="/qr" className={active('/qr')}><span className="nav-icon">📷</span>QR</Link>
      )}
    </nav>
  )
}

/* ─── Shell Layout ─── */
function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="shell">
      <Sidebar />
      <MobileTopBar />
      <main className="page-enter">{children}</main>
      <MobileBottomNav />
    </div>
  )
}

/* ─── Login Page ─── */
function LoginPage() {
  const { t } = useTranslation()
  const { login, user } = useAuth()
  const nav = useNavigate()
  const [email, setEmail] = useState('admin@company.local')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  if (user) return <Navigate to="/" replace />

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError('')
    setBusy(true)
    try {
      await login(email, password)
      nav('/')
    } catch {
      setError(t('loginFailed'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="login-wrap">
      <form className="panel login" onSubmit={onSubmit}>
        <div className="login-header">
          <div className="app-icon">⏱</div>
          <h1>{t('appName')}</h1>
          <p>{t('loginSubtitle')}</p>
        </div>
        <label>
          {t('email')}
          <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" required autoComplete="username" />
        </label>
        <label>
          {t('password')}
          <input value={password} onChange={(e) => setPassword(e.target.value)} type="password" required autoComplete="current-password" placeholder="••••••••" />
        </label>
        {error && <div className="error">⚠ {error}</div>}
        <button type="submit" disabled={busy}>{busy ? t('loading') : t('signIn')}</button>
      </form>
    </div>
  )
}

/* ─── Dashboard / Overview ─── */
function DashboardPage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const [status, setStatus] = useState<Awaited<ReturnType<typeof api.today>> | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const roles = user?.roles ?? []
  const overviewOnly = isOverviewOnly(roles)

  const load = useCallback(async () => {
    try {
      setStatus(await api.today())
      setError('')
    } catch {
      setError(t('noEmployee'))
    }
  }, [t])

  useEffect(() => {
    if (!overviewOnly) {
      void load()
      const id = window.setInterval(() => { void api.heartbeat().catch(() => undefined) }, 60_000)
      void api.heartbeat().catch(() => undefined)
      return () => window.clearInterval(id)
    }
  }, [load, overviewOnly])

  const act = async (kind: 'in' | 'out') => {
    setBusy(true)
    setError('')
    try {
      if (kind === 'in') await api.checkIn('Web')
      else await api.checkOut('Web')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : t('requestFailed'))
    } finally {
      setBusy(false)
    }
  }

  // Read-only leadership roles see an overview instead of personal controls.
  if (overviewOnly) {
    return (
      <Shell>
        <div className="panel">
          <h2>👋 {t('welcome')}, {user?.displayName}</h2>
          <p className="muted" style={{ marginTop: '0.5rem' }}>{t('overviewHint')}</p>
          <div style={{ marginTop: '1rem', display: 'flex', gap: '0.75rem' }}>
            {canViewPresence(roles) && <Link to="/presence"><button type="button">👥 {t('presence')}</button></Link>}
            <Link to="/reports"><button type="button" className="ghost-dark">📋 {t('reports')}</button></Link>
          </div>
        </div>
      </Shell>
    )
  }

  return (
    <Shell>
      <section className="panel">
        <h2 className="section-title">🏠 {t('dashboard')}</h2>
        <p className="muted">{user?.displayName}</p>
        {error && <div className="error">⚠ {error}</div>}
        {status && (
          <>
            <div className="status-grid">
              <div className={`stat-card ${status.isCheckedIn ? 'checked-in' : 'checked-out'}`}>
                <div className="label">{t('status')}</div>
                <div className="value">{status.isCheckedIn ? '🟢' : '🔴'}</div>
                <div className="sub">{status.isCheckedIn ? t('statusIn') : t('statusOut')}</div>
              </div>
              <div className="stat-card">
                <div className="label">{t('workedToday')}</div>
                <div className="value">{formatHours(status.workedMinutesToday)}</div>
                <div className="sub">{t('hoursMin')}</div>
              </div>
              <div className="stat-card">
                <div className="label">{t('openShift')}</div>
                <div className="value">{status.hasOpenShift ? '⚠️' : '✅'}</div>
                <div className="sub">{status.hasOpenShift ? t('openShiftYes') : t('openShiftNo')}</div>
              </div>
            </div>
            <div className="check-actions">
              <button
                className={`btn-checkin ${status.isCheckedIn ? 'active' : ''}`}
                disabled={busy || status.isCheckedIn}
                onClick={() => void act('in')}>
                ▶ {t('checkIn')}
              </button>
              <button
                className="btn-checkout"
                disabled={busy || !status.isCheckedIn}
                onClick={() => void act('out')}>
                ■ {t('checkOut')}
              </button>
            </div>
          </>
        )}
      </section>
    </Shell>
  )
}

/* ─── Presence Board ─── */
function PresencePage() {
  const { t } = useTranslation()
  const [rows, setRows] = useState<Awaited<ReturnType<typeof api.presence>>>([])
  const [error, setError] = useState('')

  useEffect(() => {
    const load = () => void api.presence()
      .then(data => { setRows(data); setError('') })
      .catch(() => setError(t('requestFailed')))
    load()
    const id = window.setInterval(load, 15_000)
    return () => window.clearInterval(id)
  }, [t])

  const present = rows.filter(r => r.officiallyCheckedIn).length
  const onNet   = rows.filter(r => r.seenOnNetwork).length

  return (
    <Shell>
      <section className="panel">
        <h2 className="section-title">👥 {t('presence')}</h2>
        {error && <div className="error">⚠ {error}</div>}
        <div className="status-grid" style={{ marginBottom: '1rem' }}>
          <div className="stat-card checked-in">
            <div className="label">{t('presentNow')}</div>
            <div className="value">{present}</div>
            <div className="sub">{t('official')}</div>
          </div>
          <div className="stat-card">
            <div className="label">{t('seenOnNet')}</div>
            <div className="value">{onNet}</div>
            <div className="sub">{t('networkHint')}</div>
          </div>
          <div className="stat-card">
            <div className="label">{t('totalEmployees')}</div>
            <div className="value">{rows.length}</div>
          </div>
        </div>
        <table>
          <thead>
            <tr>
              <th>{t('name')}</th>
              <th>{t('official')}</th>
              <th>{t('networkHint')}</th>
              <th>IP</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.employeeId}>
                <td>
                  <strong>{r.fullName}</strong>
                  {r.department ? <span className="muted" style={{ fontSize: '0.82rem', marginLeft: '0.4rem' }}>· {r.department}</span> : null}
                </td>
                <td>
                  <span className={`badge ${r.officiallyCheckedIn ? 'ok' : 'muted'}`}>
                    {r.officiallyCheckedIn ? '✓ ' + t('statusIn') : t('statusOut')}
                  </span>
                </td>
                <td>
                  <span className={`badge ${r.seenOnNetwork ? 'warn' : 'muted'}`}>
                    {r.seenOnNetwork ? '📶 ' + t('networkHint') : t('offline')}
                  </span>
                </td>
                <td className="muted" style={{ fontFamily: 'monospace', fontSize: '0.82rem' }}>{r.clientIp || '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </Shell>
  )
}

/* ─── Reports Page (with employee selector for privileged roles) ─── */
function ReportsPage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const now = new Date()
  const [year, setYear] = useState(now.getFullYear())
  const [month, setMonth] = useState(now.getMonth() + 1)
  const [report, setReport] = useState<Awaited<ReturnType<typeof api.monthly>> | null>(null)
  const [employees, setEmployees] = useState<EmployeeListItem[]>([])
  const [selectedEmpId, setSelectedEmpId] = useState<string | null>(null)
  const [error, setError] = useState('')
  const roles = user?.roles ?? []
  const canSelectEmployee = isPrivileged(roles)

  // Load employee list if privileged
  useEffect(() => {
    if (!canSelectEmployee) {
      setEmployees([])
      setSelectedEmpId(null)
      return
    }

    void api.employees().then(list => {
      setEmployees(list)
      setSelectedEmpId(current => {
        if (current && list.some(employee => employee.id === current)) return current
        const own = list.find(employee => employee.id === user?.employeeId)
        return own?.id ?? list[0]?.id ?? null
      })
    }).catch(() => {
      setEmployees([])
      setSelectedEmpId(null)
    })
  }, [canSelectEmployee, user?.employeeId])

  const effectiveEmpId = canSelectEmployee ? selectedEmpId : user?.employeeId ?? null

  useEffect(() => {
    if (!effectiveEmpId) return
    void api.monthly(effectiveEmpId, year, month)
      .then(data => { setReport(data); setError('') })
      .catch(() => { setReport(null); setError(t('requestFailed')) })
  }, [effectiveEmpId, year, month, t])

  const download = async (format: 'xlsx' | 'pdf') => {
    if (!effectiveEmpId) return
    try {
      const token = getToken()
      const res = await fetch(api.exportUrl(effectiveEmpId, year, month, format), {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      })
      if (!res.ok) throw new Error(t('exportFailed'))
      const blob = await res.blob()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `worktime_${year}_${month.toString().padStart(2, '0')}.${format}`
      document.body.appendChild(a)
      a.click()
      a.remove()
      URL.revokeObjectURL(url)
      setError('')
    } catch {
      setError(t('exportFailed'))
    }
  }

  return (
    <Shell>
      <section className="panel">
        <h2 className="section-title">📋 {t('reports')}</h2>
        {!effectiveEmpId && !canSelectEmployee && <div className="error">⚠ {t('noEmployee')}</div>}
        {error && <div className="error">⚠ {error}</div>}

        <div className="filters">
          {canSelectEmployee && employees.length > 0 && (
            <label style={{ minWidth: '200px' }}>
              {t('employee')}
              <select value={selectedEmpId ?? ''} onChange={e => setSelectedEmpId(e.target.value)}>
                {employees.map(emp => (
                  <option key={emp.id} value={emp.id}>
                    {emp.fullName} {emp.department ? `(${emp.department})` : ''}
                  </option>
                ))}
              </select>
            </label>
          )}
          <label>
            {t('year')}
            <input type="number" value={year} min={2020} max={2099}
              onChange={(e) => setYear(Number(e.target.value))} style={{ width: '90px' }} />
          </label>
          <label>
            {t('month')}
            <input type="number" min={1} max={12} value={month}
              onChange={(e) => setMonth(Number(e.target.value))} style={{ width: '70px' }} />
          </label>
          <button type="button" onClick={() => void download('xlsx')}>📥 {t('exportExcel')}</button>
          <button type="button" className="ghost-dark" onClick={() => void download('pdf')}>📄 {t('exportPdf')}</button>
        </div>

        {report && (
          <>
            <div className="status-grid">
              <div className="stat-card">
                <div className="label">{t('totalHours')}</div>
                <div className="value">{formatHours(report.totalMinutes)}</div>
                <div className="sub">{report.fullName}</div>
              </div>
              <div className="stat-card">
                <div className="label">{t('workDays')}</div>
                <div className="value">{report.days.filter(d => d.workedMinutes > 0).length}</div>
                <div className="sub">{t('daysWithHours')}</div>
              </div>
            </div>
            <table>
              <thead>
                <tr>
                  <th>{t('date')}</th>
                  <th>{t('hours')}</th>
                  <th>{t('openShift')}</th>
                </tr>
              </thead>
              <tbody>
                {report.days.map((d) => (
                  <tr key={d.workDate}>
                    <td>{d.workDate}</td>
                    <td><strong>{d.workedHoursDisplay}</strong></td>
                    <td>
                      {d.hasOpenShift
                        ? <span className="badge warn">⚠ {t('openShiftYes')}</span>
                        : <span className="badge ok">✓</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </section>
    </Shell>
  )
}

/* ─── QR Page ─── */
function QrPage() {
  const { t } = useTranslation()
  const [msg, setMsg] = useState('')
  const [msgType, setMsgType] = useState<'ok' | 'error'>('ok')
  const [busy, setBusy] = useState(false)

  const act = async (kind: 'in' | 'out') => {
    setBusy(true)
    setMsg('')
    try {
      if (kind === 'in') await api.checkIn('Qr')
      else await api.checkOut('Qr')
      setMsg(kind === 'in' ? `✓ ${t('statusIn')}` : `✓ ${t('statusOut')}`)
      setMsgType('ok')
    } catch {
      setMsg(`⚠ ${t('noEmployee')}`)
      setMsgType('error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Shell>
      <section className="panel qr" style={{ maxWidth: 420, margin: '0 auto' }}>
        <h2 className="section-title">📷 {t('qr')}</h2>
        <p className="muted">{t('scanHint')}</p>
        <div className="check-actions" style={{ marginTop: '1.5rem' }}>
          <button className="btn-checkin" disabled={busy} onClick={() => void act('in')}>▶ {t('checkIn')}</button>
          <button className="btn-checkout" disabled={busy} onClick={() => void act('out')}>■ {t('checkOut')}</button>
        </div>
        {msg && <p className={msgType === 'ok' ? 'success' : 'error'} style={{ marginTop: '1rem', fontWeight: 600 }}>{msg}</p>}
      </section>
    </Shell>
  )
}

/* ─── Private Route ─── */
function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth()
  const { t } = useTranslation()
  if (loading) return <div className="login-wrap"><div className="muted">{t('loading')}</div></div>
  if (!user) return <Navigate to="/login" replace />
  return <>{children}</>
}

function PresenceRoute({ children }: { children: React.ReactNode }) {
  const { user } = useAuth()
  return canViewPresence(user?.roles ?? []) ? <>{children}</> : <Navigate to="/" replace />
}

function EmployeeRoute({ children }: { children: React.ReactNode }) {
  const { user } = useAuth()
  return !isPrivileged(user?.roles ?? []) ? <>{children}</> : <Navigate to="/" replace />
}

/* ─── App Root ─── */
export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<PrivateRoute><DashboardPage /></PrivateRoute>} />
        <Route path="/presence" element={<PrivateRoute><PresenceRoute><PresencePage /></PresenceRoute></PrivateRoute>} />
        <Route path="/reports" element={<PrivateRoute><ReportsPage /></PrivateRoute>} />
        <Route path="/qr" element={<PrivateRoute><EmployeeRoute><QrPage /></EmployeeRoute></PrivateRoute>} />
      </Routes>
    </AuthProvider>
  )
}
