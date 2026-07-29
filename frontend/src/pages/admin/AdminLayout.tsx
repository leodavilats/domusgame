import { Suspense } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { Spinner } from '../../components/ui'

const tabs = [
  { to: '/admin', label: 'Geral', end: true },
  { to: '/admin/temporadas', label: 'Temporadas', end: false },
  { to: '/admin/rodadas', label: 'Rodadas', end: false },
  { to: '/admin/participantes', label: 'Pessoas', end: false },
  { to: '/admin/ferramentas', label: 'Ferramentas', end: false },
]

export function AdminLayout() {
  return (
    <div className="space-y-5">
      <nav
        aria-label="Seções da administração"
        className="-mx-4 overflow-x-auto px-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
      >
        <div className="inline-flex min-w-full gap-1 rounded-2xl bg-slate-200/70 p-1">
          {tabs.map((tab) => (
            <NavLink
              key={tab.to}
              to={tab.to}
              end={tab.end}
              className={({ isActive }) =>
                `shrink-0 rounded-xl px-3.5 py-2 text-sm font-semibold transition ${
                  isActive
                    ? 'bg-surface text-slate-900 shadow-card'
                    : 'text-slate-600 hover:text-slate-900'
                }`
              }
            >
              {tab.label}
            </NavLink>
          ))}
        </div>
      </nav>

      <Suspense fallback={<Spinner />}>
        <Outlet />
      </Suspense>
    </div>
  )
}
