import { useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, Route, Routes, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { api, formatHours, getToken } from './api'
import { AuthProvider, useAuth } from './auth'
import './App.css'

function Shell({ children }: { children: React.ReactNode }) {
  const { t, i18n } = useTranslation()
  const { user, logout, setLanguage, setTheme } = useAuth()
  return (
    <div className="shell">
      <header className="topbar">
        <div className="brand">{t('appName')}</div>
        <nav>
          <Link to="/">{t('dashboard')}</Link>
          <Link to="/presence">{t('presence')}</Link>
          <Link to="/reports">{t('reports')}</Link>
          <Link to="/qr">{t('qr')}</Link>
        </nav>
        <div className="controls">
          <select
            aria-label={t('language')}
            value={i18n.language}
            onChange={(e) => void setLanguage(e.target.value)}
          >
            <option value="en">EN</option>
            <option value="hy">HY</option>
            <option value="ru">RU</option>
          </select>
          <select
            aria-label={t('theme')}
            value={user?.themePreference || localStorage.getItem('theme') || 'system'}
            onChange={(e) => void setTheme(e.target.value as 'system' | 'light' | 'dark')}
          >
            <option value="system">{t('themeSystem')}</option>
            <option value="light">{t('themeLight')}</option>
            <option value="dark">{t('themeDark')}</option>
          </select>
          <span className="user">{user?.displayName}</span>
          <button type="button" className="ghost" onClick={logout}>
            {t('logout')}
          </button>
        </div>
      </header>
      <main>{children}</main>
    </div>
  )
}

function LoginPage() {
  const { t } = useTranslation()
  const { login, user } = useAuth()
  const nav = useNavigate()
  const [email, setEmail] = useState('admin@company.local')
  const [password, setPassword] = useState('ChangeMe123!')
  const [error, setError] = useState('')

  if (user) return <Navigate to="/" replace />

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError('')
    try {
      await login(email, password)
      nav('/')
    } catch {
      setError(t('loginFailed'))
    }
  }

  return (
    <div className="login-wrap">
      <form className="panel login" onSubmit={onSubmit}>
        <h1>{t('appName')}</h1>
        <p>{t('login')}</p>
        <label>
          {t('email')}
          <input value={email} onChange={(e) => setEmail(e.target.value)} type="email" required />
        </label>
        <label>
          {t('password')}
          <input value={password} onChange={(e) => setPassword(e.target.value)} type="password" required />
        </label>
        {error && <div className="error">{error}</div>}
        <button type="submit">{t('signIn')}</button>
      </form>
    </div>
  )
}

function DashboardPage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const [status, setStatus] = useState<Awaited<ReturnType<typeof api.today>> | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const load = async () => {
    try {
      setStatus(await api.today())
      setError('')
    } catch {
      setError(t('noEmployee'))
    }
  }

  useEffect(() => {
    void load()
    const id = window.setInterval(() => {
      void api.heartbeat().catch(() => undefined)
    }, 60_000)
    void api.heartbeat().catch(() => undefined)
    return () => window.clearInterval(id)
  }, [])

  const act = async (kind: 'in' | 'out') => {
    setBusy(true)
    try {
      if (kind === 'in') await api.checkIn('Web')
      else await api.checkOut('Web')
      await load()
    } finally {
      setBusy(false)
    }
  }

  return (
    <Shell>
      <section className="panel">
        <h2>{t('dashboard')}</h2>
        <p className="muted">{user?.displayName}</p>
        {error && <div className="error">{error}</div>}
        {status && (
          <>
            <div className="status-grid">
              <div>
                <div className="label">{t('workedToday')}</div>
                <div className="value">{formatHours(status.workedMinutesToday)}</div>
              </div>
              <div>
                <div className="label">Status</div>
                <div className="value">{status.isCheckedIn ? t('statusIn') : t('statusOut')}</div>
              </div>
              <div>
                <div className="label">{t('openShift')}</div>
                <div className="value">{status.hasOpenShift ? 'Yes' : 'No'}</div>
              </div>
            </div>
            <div className="actions">
              <button disabled={busy || status.isCheckedIn} onClick={() => void act('in')}>
                {t('checkIn')}
              </button>
              <button disabled={busy || !status.isCheckedIn} className="secondary" onClick={() => void act('out')}>
                {t('checkOut')}
              </button>
            </div>
          </>
        )}
      </section>
    </Shell>
  )
}

function PresencePage() {
  const { t } = useTranslation()
  const [rows, setRows] = useState<Awaited<ReturnType<typeof api.presence>>>([])

  useEffect(() => {
    const load = () => void api.presence().then(setRows).catch(() => undefined)
    load()
    const id = window.setInterval(load, 15_000)
    return () => window.clearInterval(id)
  }, [])

  return (
    <Shell>
      <section className="panel">
        <h2>{t('presence')}</h2>
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>{t('official')}</th>
              <th>{t('networkHint')}</th>
              <th>IP</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.employeeId}>
                <td>
                  {r.fullName}
                  {r.department ? <span className="muted"> · {r.department}</span> : null}
                </td>
                <td>
                  <span className={r.officiallyCheckedIn ? 'badge ok' : 'badge'}>
                    {r.officiallyCheckedIn ? t('statusIn') : t('statusOut')}
                  </span>
                </td>
                <td>
                  <span className={r.seenOnNetwork ? 'badge warn' : 'badge'}>
                    {r.seenOnNetwork ? t('networkHint') : t('offline')}
                  </span>
                </td>
                <td>{r.clientIp || '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </Shell>
  )
}

function ReportsPage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const now = new Date()
  const [year, setYear] = useState(now.getFullYear())
  const [month, setMonth] = useState(now.getMonth() + 1)
  const [report, setReport] = useState<Awaited<ReturnType<typeof api.monthly>> | null>(null)

  useEffect(() => {
    if (!user?.employeeId) return
    void api.monthly(user.employeeId, year, month).then(setReport).catch(() => setReport(null))
  }, [user?.employeeId, year, month])

  const download = async (format: 'xlsx' | 'pdf') => {
    if (!user?.employeeId) return
    const token = getToken()
    const res = await fetch(api.exportUrl(user.employeeId, year, month, format), {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })
    const blob = await res.blob()
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `attendance_${year}_${month.toString().padStart(2, '0')}.${format === 'pdf' ? 'pdf' : 'xlsx'}`
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <Shell>
      <section className="panel">
        <h2>{t('reports')}</h2>
        {!user?.employeeId && <div className="error">{t('noEmployee')}</div>}
        <div className="filters">
          <label>
            {t('year')}
            <input type="number" value={year} onChange={(e) => setYear(Number(e.target.value))} />
          </label>
          <label>
            {t('month')}
            <input type="number" min={1} max={12} value={month} onChange={(e) => setMonth(Number(e.target.value))} />
          </label>
          <button type="button" onClick={() => void download('xlsx')}>
            {t('exportExcel')}
          </button>
          <button type="button" className="secondary" onClick={() => void download('pdf')}>
            {t('exportPdf')}
          </button>
        </div>
        {report && (
          <>
            <p>
              {t('totalHours')}: <strong>{formatHours(report.totalMinutes)}</strong>
            </p>
            <table>
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Hours</th>
                  <th>{t('openShift')}</th>
                </tr>
              </thead>
              <tbody>
                {report.days.map((d) => (
                  <tr key={d.workDate}>
                    <td>{d.workDate}</td>
                    <td>{d.workedHoursDisplay}</td>
                    <td>{d.hasOpenShift ? 'Yes' : 'No'}</td>
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

function QrPage() {
  const { t } = useTranslation()
  const [msg, setMsg] = useState('')
  const [busy, setBusy] = useState(false)

  const act = async (kind: 'in' | 'out') => {
    setBusy(true)
    setMsg('')
    try {
      if (kind === 'in') await api.checkIn('Qr')
      else await api.checkOut('Qr')
      setMsg(kind === 'in' ? t('statusIn') : t('statusOut'))
    } catch {
      setMsg(t('noEmployee'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Shell>
      <section className="panel qr">
        <h2>{t('qr')}</h2>
        <p>{t('scanHint')}</p>
        <div className="actions">
          <button disabled={busy} onClick={() => void act('in')}>
            {t('checkIn')}
          </button>
          <button disabled={busy} className="secondary" onClick={() => void act('out')}>
            {t('checkOut')}
          </button>
        </div>
        {msg && <p className="value">{msg}</p>}
      </section>
    </Shell>
  )
}

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth()
  const { t } = useTranslation()
  if (loading) return <div className="login-wrap">{t('loading')}</div>
  if (!user) return <Navigate to="/login" replace />
  return <>{children}</>
}

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route
          path="/"
          element={
            <PrivateRoute>
              <DashboardPage />
            </PrivateRoute>
          }
        />
        <Route
          path="/presence"
          element={
            <PrivateRoute>
              <PresencePage />
            </PrivateRoute>
          }
        />
        <Route
          path="/reports"
          element={
            <PrivateRoute>
              <ReportsPage />
            </PrivateRoute>
          }
        />
        <Route
          path="/qr"
          element={
            <PrivateRoute>
              <QrPage />
            </PrivateRoute>
          }
        />
      </Routes>
    </AuthProvider>
  )
}
