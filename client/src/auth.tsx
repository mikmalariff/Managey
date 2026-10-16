import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { api, setUnauthorizedHandler, type Me } from './api'

interface AuthState {
  me: Me | null
  loading: boolean
  login: (username: string, password: string) => Promise<void>
  logout: () => Promise<void>
  refresh: () => Promise<void>
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    try {
      setMe(await api.get<Me>('/api/auth/me'))
    } catch {
      setMe(null)
    }
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(() => setMe(null))
    refresh().finally(() => setLoading(false))
  }, [refresh])

  const login = async (username: string, password: string) => {
    setMe(await api.post<Me>('/api/auth/login', { username, password }))
  }

  const logout = async () => {
    await api.post('/api/auth/logout')
    setMe(null)
  }

  return <AuthContext.Provider value={{ me, loading, login, logout, refresh }}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}

/** Signed-in user; only call inside routes that require authentication. */
export function useMe() {
  const { me } = useAuth()
  if (!me) throw new Error('Not signed in')
  const isAdmin = me.role === 'Admin'
  const isHR = me.role === 'HR' || isAdmin
  return { me, isAdmin, isHR, isManager: me.isManager || isHR }
}
