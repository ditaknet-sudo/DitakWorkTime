import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { api, getToken, setToken, type AuthUser } from './api'

type ThemeMode = 'system' | 'light' | 'dark'

type AuthState = {
  user: AuthUser | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
  setLanguage: (lang: string) => Promise<void>
  setTheme: (theme: ThemeMode) => Promise<void>
  refresh: () => Promise<void>
}

const AuthContext = createContext<AuthState | null>(null)

function applyTheme(theme: ThemeMode) {
  const root = document.documentElement
  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches
  const dark = theme === 'dark' || (theme === 'system' && prefersDark)
  root.dataset.theme = dark ? 'dark' : 'light'
  localStorage.setItem('theme', theme)
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const { i18n } = useTranslation()
  const [user, setUser] = useState<AuthUser | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = async () => {
    if (!getToken()) {
      setUser(null)
      setLoading(false)
      return
    }
    try {
      const me = await api.me()
      setUser(me)
      await i18n.changeLanguage(me.preferredLanguage || 'en')
      localStorage.setItem('lang', me.preferredLanguage || 'en')
      applyTheme((me.themePreference as ThemeMode) || 'system')
    } catch {
      setToken(null)
      setUser(null)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    const savedTheme = (localStorage.getItem('theme') as ThemeMode) || 'system'
    applyTheme(savedTheme)
    void refresh()
    const mq = window.matchMedia('(prefers-color-scheme: dark)')
    const onChange = () => {
      const theme = (localStorage.getItem('theme') as ThemeMode) || 'system'
      if (theme === 'system') applyTheme('system')
    }
    mq.addEventListener('change', onChange)
    return () => mq.removeEventListener('change', onChange)
  }, [])

  const value = useMemo<AuthState>(
    () => ({
      user,
      loading,
      refresh,
      login: async (email, password) => {
        const res = await api.login(email, password)
        setToken(res.token)
        setUser(res.user)
        await i18n.changeLanguage(res.user.preferredLanguage || 'en')
        applyTheme((res.user.themePreference as ThemeMode) || 'system')
      },
      logout: () => {
        setToken(null)
        setUser(null)
      },
      setLanguage: async (lang) => {
        localStorage.setItem('lang', lang)
        await i18n.changeLanguage(lang)
        if (getToken()) {
          const me = await api.preferences(lang, undefined)
          setUser(me)
        }
      },
      setTheme: async (theme) => {
        applyTheme(theme)
        if (getToken()) {
          const me = await api.preferences(undefined, theme)
          setUser(me)
        }
      },
    }),
    [user, loading, i18n],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('AuthProvider missing')
  return ctx
}
