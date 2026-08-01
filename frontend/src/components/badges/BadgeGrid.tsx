import type { EarnedBadge } from '../../api/types'
import { BADGE_CATALOG, BADGE_ORDER, BadgeDefs, BadgeIcon } from './BadgeIcon'

export function BadgeGrid({ badges }: { badges: EarnedBadge[] }) {
  const earnedByCode = new Map(badges.map((badge) => [badge.code, badge]))

  return (
    <div className="grid grid-cols-3 gap-3 sm:grid-cols-4">
      <BadgeDefs />

      {BADGE_ORDER.map((code) => {
        const earned = earnedByCode.get(code)
        const info = BADGE_CATALOG[code]

        return (
          <div
            key={code}
            className={`flex flex-col items-center gap-1.5 rounded-xl p-2 text-center ${
              earned ? '' : 'grayscale opacity-35'
            }`}
            title={earned ? `${info.nome} — ${info.versiculo}` : `Bloqueado — ${info.descricao}`}
          >
            <BadgeIcon code={code} className="h-14 w-14" />
            <p className="text-[11px] font-semibold leading-tight text-slate-700">{info.nome}</p>
          </div>
        )
      })}
    </div>
  )
}
