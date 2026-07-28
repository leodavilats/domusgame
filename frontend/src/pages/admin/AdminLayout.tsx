import { NavLink, Outlet } from 'react-router-dom'

const tabs = [
  { to: '/admin', label: 'Visao geral', end: true },
  { to: '/admin/rodadas', label: 'Rodadas', end: false },
  { to: '/admin/temporadas', label: 'Temporadas', end: false },
  { to: '/admin/participantes', label: 'Pessoas', end: false },
]

export function AdminLayout() {
  return (
    <div className="space-y-4">
      <nav className="-mx-1 flex gap-1 overflow-x-auto pb-1">
        {tabs.map((tab) => (
          <NavLink
            key={tab.to}
            to={tab.to}
            end={tab.end}
            className={({ isActive }) =>
              `shrink-0 rounded-xl px-3 py-2 text-sm font-semibold ${
                isActive ? 'bg-night-900 text-white' : 'bg-white text-slate-600 border border-slate-200'
              }`
            }
          >
            {tab.label}
          </NavLink>
        ))}
      </nav>

      <Outlet />
    </div>
  )
}
