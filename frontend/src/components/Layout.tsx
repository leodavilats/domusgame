import { useEffect } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useSession } from '../auth/SessionContext'
import { CalendarIcon, HomeIcon, TrophyIcon, UserIcon } from './Icons'
import { LogoMark } from './Logo'
import { Avatar } from './ui'

const items = [
  { to: '/', label: 'Início', Icon: HomeIcon, end: true },
  { to: '/ranking', label: 'Ranking', Icon: TrophyIcon, end: false },
  { to: '/historico', label: 'Histórico', Icon: CalendarIcon, end: false },
  { to: '/perfil', label: 'Perfil', Icon: UserIcon, end: false },
]

function useScrollToTopOnNavigate(pathname: string) {
  useEffect(() => {
    window.scrollTo({ top: 0, behavior: 'instant' })
  }, [pathname])
}

export function Layout() {
  const { me } = useSession()
  const location = useLocation()

  useScrollToTopOnNavigate(location.pathname)

  const immersive = location.pathname.includes('/quiz')

  const wide = location.pathname.startsWith('/admin')

  return (
    <div className={`mx-auto flex min-h-dvh w-full flex-col bg-slate-100 ${wide ? 'max-w-5xl' : 'max-w-2xl'}`}>
      {!immersive && (
        <header className="sticky top-0 z-10 border-b border-slate-200 bg-white/95 backdrop-blur">
          <div className="flex items-center justify-between px-4 py-3">
            <div className="flex items-center gap-3">
              <LogoMark size={38} />
              <div>
                <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
                  Desafio semanal
                </p>
                <p className="text-base font-bold text-slate-900">{me?.room?.name ?? 'GC Domus'}</p>
              </div>
            </div>

            <div className="flex items-center gap-3">
              {me?.isAdmin && (
                <NavLink to="/admin" className="text-sm font-semibold text-brand-600">
                  Admin
                </NavLink>
              )}
              {me && <Avatar name={me.displayName} url={me.avatarUrl} size={36} />}
            </div>
          </div>
        </header>
      )}

      <main className={`flex-1 px-4 ${immersive ? 'py-4' : 'pb-24 pt-4'}`}>
        <Outlet />
      </main>

      {!immersive && (
        <nav
          className={`fixed bottom-0 left-1/2 z-10 w-full -translate-x-1/2 border-t border-slate-200 bg-white pb-[env(safe-area-inset-bottom)] ${
            wide ? 'max-w-5xl' : 'max-w-2xl'
          }`}
        >
          <ul className="grid grid-cols-4">
            {items.map(({ to, label, Icon, end }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  end={end}
                  className={({ isActive }) =>
                    `flex min-h-14 flex-col items-center justify-center gap-0.5 text-xs font-medium ${
                      isActive ? 'text-brand-600' : 'text-slate-500'
                    }`
                  }
                >
                  <Icon />
                  {label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      )}
    </div>
  )
}
