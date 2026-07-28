import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useSession } from '../auth/SessionContext'
import { Avatar } from './ui'

const items = [
  { to: '/', label: 'Inicio', icon: '🏠', end: true },
  { to: '/ranking', label: 'Ranking', icon: '🏆', end: false },
  { to: '/historico', label: 'Historico', icon: '📅', end: false },
  { to: '/perfil', label: 'Perfil', icon: '👤', end: false },
]

/** Layout mobile-first: cabecalho enxuto e navegacao inferior ao alcance do polegar. */
export function Layout() {
  const { me } = useSession()
  const location = useLocation()

  // A tela do quiz ocupa a tela inteira: sem navegacao para nao tirar o foco.
  const immersive = location.pathname.includes('/quiz')

  return (
    <div className="mx-auto flex min-h-dvh w-full max-w-2xl flex-col bg-slate-100">
      {!immersive && (
        <header className="sticky top-0 z-10 border-b border-slate-200 bg-white/95 backdrop-blur">
          <div className="flex items-center justify-between px-4 py-3">
            <div>
              <p className="text-xs font-medium uppercase tracking-wide text-slate-500">Desafio semanal</p>
              <p className="text-base font-bold text-slate-900">{me?.gcName ?? 'GC Domus'}</p>
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
        <nav className="fixed bottom-0 left-1/2 z-10 w-full max-w-2xl -translate-x-1/2 border-t border-slate-200 bg-white pb-[env(safe-area-inset-bottom)]">
          <ul className="grid grid-cols-4">
            {items.map((item) => (
              <li key={item.to}>
                <NavLink
                  to={item.to}
                  end={item.end}
                  className={({ isActive }) =>
                    `flex min-h-14 flex-col items-center justify-center gap-0.5 text-xs font-medium ${
                      isActive ? 'text-brand-600' : 'text-slate-500'
                    }`
                  }
                >
                  <span aria-hidden="true" className="text-lg">
                    {item.icon}
                  </span>
                  {item.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      )}
    </div>
  )
}
