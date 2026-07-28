import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { ApiError, api } from '../api/client'
import type { Me } from '../api/types'

interface SessionValue {
  me: Me | null
  loading: boolean
  setMe: (me: Me | null) => void
  refresh: () => Promise<void>
  logout: () => Promise<void>
}

const SessionContext = createContext<SessionValue | null>(null)

export function SessionProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    try {
      setMe(await api.get<Me>('/api/auth/me'))
    } catch (error) {
      if (error instanceof ApiError && error.isUnauthorized) setMe(null)
      else setMe(null)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  const logout = useCallback(async () => {
    try {
      await api.post('/api/auth/logout')
    } finally {
      setMe(null)
    }
  }, [])

  const value = useMemo<SessionValue>(
    () => ({ me, loading, setMe, refresh, logout }),
    [me, loading, refresh, logout],
  )

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
}

export function useSession(): SessionValue {
  const context = useContext(SessionContext)
  if (!context) throw new Error('useSession precisa estar dentro de SessionProvider.')
  return context
}
