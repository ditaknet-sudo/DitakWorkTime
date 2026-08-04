import { createContext, useContext } from 'react'
import type { AuthUser } from './api'

export type ThemeMode = 'system' | 'light' | 'dark'

export type AuthState = {
  user: AuthUser | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
  setLanguage: (lang: string) => Promise<void>
  setTheme: (theme: ThemeMode) => Promise<void>
  refresh: () => Promise<void>
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('AuthProvider missing')
  return context
}
