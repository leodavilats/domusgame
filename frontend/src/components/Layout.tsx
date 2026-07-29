import { useEffect } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useSession } from '../auth/SessionContext'
import { CalendarIcon, HomeIcon, ShieldIcon, TrophyIcon } from './Icons'
import { LogoMark } from './Logo'
import { Avatar } from './ui'

const items = [
  { to: '/', label: 'Início', Icon: HomeIcon, end: true },
  { to: '/ranking', label: 'Ranking', Icon: TrophyIcon, end: false },
  { to: '/historico', label: 'Histórico', Icon: CalendarIcon, end: false },
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
    <div className="flex min-h-dvh flex-col">
      {!immersive && (
        <header className="sticky top-0 z-20 border-b border-slate-200/80 bg-surface/85 backdrop-blur-md">
          <div className={`mx-auto flex items-center gap-3 px-4 py-2.5 ${wide ? 'max-w-6xl' : 'max-w-2xl'}`}>
            <Link to="/" className="flex min-w-0 items-center gap-2.5 rounded-xl">
              <LogoMark size={36} />
              <span className="min-w-0">
                <span className="block truncate text-[15px] font-bold leading-tight text-slate-900">
                  {me?.room?.name ?? 'GC Domus'}
                </span>
                <span className="block text-[11px] font-medium uppercase tracking-wide text-slate-500">
                  Desafio semanal
                </span>
              </span>
            </Link>

            <nav aria-label="Seções" className="ml-auto hidden items-center gap-1 md:flex">
              {items.map(({ to, label, end }) => (
                <NavLink
                  key={to}
                  to={to}
                  end={end}
                  className={({ isActive }) =>
                    `rounded-xl px-3 py-2 text-sm font-semibold transition ${
                      isActive ? 'bg-slate-100 text-slate-900' : 'text-slate-500 hover:bg-slate-50 hover:text-slate-700'
                    }`
                  }
                >
                  {label}
                </NavLink>
              ))}
            </nav>

            <div className="ml-auto flex items-center gap-1 md:ml-2">
              {me?.isAdmin && (
                <NavLink
                  to="/admin"
                  className={({ isActive }) =>
                    `inline-flex min-h-10 items-center gap-1.5 rounded-xl px-2.5 text-sm font-semibold transition ${
                      isActive ? 'bg-night-900 text-white' : 'text-slate-500 hover:bg-slate-100 hover:text-slate-700'
                    }`
                  }
                >
                  <ShieldIcon className="h-5 w-5" />
                  <span className="hidden sm:inline">Admin</span>
                </NavLink>
              )}

              {me && (
                <Link
                  to="/perfil"
                  aria-label={`Meu perfil (${me.displayName})`}
                  title="Meu perfil"
                  className="rounded-full p-0.5 transition hover:bg-slate-100"
                >
                  <Avatar name={me.displayName} url={me.avatarUrl} size={36} ring />
                </Link>
              )}
            </div>
          </div>
        </header>
      )}

      <main
        className={`mx-auto w-full flex-1 px-4 ${wide ? 'max-w-6xl' : 'max-w-2xl'} ${
          immersive ? 'py-3' : 'pb-28 pt-5 md:pb-10'
        }`}
      >
        <Outlet />
      </main>

      {!immersive && (
        <nav
          aria-label="Navegação principal"
          className="fixed bottom-0 left-0 z-20 w-full border-t border-slate-200/80 bg-surface/95 pb-[env(safe-area-inset-bottom)] backdrop-blur-md md:hidden"
        >
          <ul className="mx-auto grid max-w-2xl grid-cols-3">
            {items.map(({ to, label, Icon, end }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  end={end}
                  className={({ isActive }) =>
                    `group flex min-h-14 flex-col items-center justify-center gap-1 text-[11px] font-semibold transition ${
                      isActive ? 'text-brand-700' : 'text-slate-500'
                    }`
                  }
                >
                  {({ isActive }) => (
                    <>
                      <span
                        className={`flex h-8 w-14 items-center justify-center rounded-full transition ${
                          isActive ? 'bg-brand-50' : 'group-active:bg-slate-100'
                        }`}
                      >
                        <Icon className="h-[22px] w-[22px]" />
                      </span>
                      {label}
                    </>
                  )}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      )}
    </div>
  )
}
