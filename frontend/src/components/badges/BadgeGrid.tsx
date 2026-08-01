import { useState } from 'react'
import type { EarnedBadge } from '../../api/types'
import { BADGE_CATALOG, BADGE_ORDER, BadgeDefs, BadgeIcon } from './BadgeIcon'
import { formatDate } from '../../lib/format'

export function BadgeGrid({ badges }: { badges: EarnedBadge[] }) {
  const earnedByCode = new Map(badges.map((badge) => [badge.code, badge]))
  const [selected, setSelected] = useState<string | null>(null)
  const selectedInfo = selected ? BADGE_CATALOG[selected] : null
  const selectedEarned = selected ? earnedByCode.get(selected) : undefined

  return (
    <div>
      <div className="grid grid-cols-3 gap-3 sm:grid-cols-4">
        <BadgeDefs />

        {BADGE_ORDER.map((code) => {
          const earned = earnedByCode.get(code)
          const info = BADGE_CATALOG[code]

          return (
            <button
              key={code}
              type="button"
              onClick={() => setSelected(code)}
              className={`flex flex-col items-center gap-1.5 rounded-xl p-2 text-center transition ${
                earned ? 'hover:bg-slate-50' : 'grayscale opacity-35 hover:opacity-60'
              }`}
            >
              <BadgeIcon code={code} className="h-14 w-14" />
              <p className="text-[11px] font-semibold leading-tight text-slate-700">{info.nome}</p>
            </button>
          )
        })}
      </div>

      {selectedInfo ? (
        <div
          role="button"
          tabIndex={0}
          onClick={() => setSelected(null)}
          onKeyDown={(event) => event.key === 'Escape' && setSelected(null)}
          className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 p-4 sm:items-center"
        >
          <div
            onClick={(event) => event.stopPropagation()}
            className="w-full max-w-sm rounded-2xl bg-white p-5 text-center shadow-raised"
          >
            <BadgeIcon code={selected as string} className="mx-auto h-20 w-20" />
            <p className="mt-2 text-base font-bold text-slate-900">{selectedInfo.nome}</p>
            <p className="text-xs font-semibold uppercase tracking-wide text-brand-600">{selectedInfo.versiculo}</p>
            <p className="mt-2 text-sm leading-relaxed text-slate-600">{selectedInfo.descricao}</p>

            {selectedEarned ? (
              <p className="mt-3 text-xs text-emerald-600">Conquistado em {formatDate(selectedEarned.earnedAt)}</p>
            ) : (
              <p className="mt-3 text-xs text-slate-400">Ainda não conquistado</p>
            )}

            <button
              type="button"
              onClick={() => setSelected(null)}
              className="mt-4 w-full rounded-xl bg-slate-100 py-2 text-sm font-semibold text-slate-700"
            >
              Fechar
            </button>
          </div>
        </div>
      ) : null}
    </div>
  )
}
