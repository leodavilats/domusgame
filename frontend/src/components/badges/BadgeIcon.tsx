import type { ReactNode } from 'react'

export interface BadgeInfo {
  nome: string
  versiculo: string
  descricao: string
  cor: string
}

export const BADGE_CATALOG: Record<string, BadgeInfo> = {
  SarcaArdente: {
    nome: 'Sarça Ardente',
    versiculo: 'Êxodo 3:2',
    descricao: 'Completou a primeira rodada desde que entrou na sala.',
    cor: '#5c8b7f',
  },
  TabuasDaLei: {
    nome: 'Tábuas da Lei',
    versiculo: 'Êxodo 31:18',
    descricao: 'Acertou 100% das perguntas de uma rodada.',
    cor: '#b6913c',
  },
  ColunaDeFogo: {
    nome: 'Coluna de Fogo',
    versiculo: 'Êxodo 13:21',
    descricao: 'Terminou uma rodada perfeita usando menos de 25% do tempo disponível.',
    cor: '#e0bb6a',
  },
  AncoraDaEsperanca: {
    nome: 'Âncora da Esperança',
    versiculo: 'Hebreus 6:19',
    descricao: 'Acertou tudo usando quase todo o tempo — firmeza sem pressa.',
    cor: '#94897a',
  },
  CestoDeMana: {
    nome: 'Cesto de Maná',
    versiculo: 'Êxodo 16:4',
    descricao: 'Participou de 4 rodadas seguidas sem falhar.',
    cor: '#e0bb6a',
  },
  JaquimEBoaz: {
    nome: 'Jaquim e Boaz',
    versiculo: '1 Reis 7:21',
    descricao: 'Participou de todas as rodadas de uma temporada, sem exceção.',
    cor: '#b6913c',
  },
  HarpaDeDavi: {
    nome: 'Harpa de Davi',
    versiculo: '1 Samuel 16:23',
    descricao: 'Ficou entre os 3 primeiros no ranking de uma rodada.',
    cor: '#b6913c',
  },
  CoroaDaVida: {
    nome: 'Coroa da Vida',
    versiculo: 'Tiago 1:12',
    descricao: 'Terminou em 1º lugar no ranking final de uma temporada.',
    cor: '#e0bb6a',
  },
  LampadaAcesa10: {
    nome: 'Lâmpada Acesa (10)',
    versiculo: 'Mateus 25:4',
    descricao: 'Participou de 10 rodadas na sala.',
    cor: '#5c8b7f',
  },
  LampadaAcesa25: {
    nome: 'Lâmpada Acesa (25)',
    versiculo: 'Mateus 25:4',
    descricao: 'Participou de 25 rodadas na sala.',
    cor: '#5c8b7f',
  },
  LampadaAcesa50: {
    nome: 'Lâmpada Acesa (50)',
    versiculo: 'Mateus 25:4',
    descricao: 'Participou de 50 rodadas na sala.',
    cor: '#5c8b7f',
  },
  PedraAngular: {
    nome: 'Pedra Angular',
    versiculo: 'Salmos 118:22',
    descricao: 'Participou desde a primeira rodada aberta na sala.',
    cor: '#94897a',
  },
}

export const BADGE_ORDER = [
  'SarcaArdente',
  'TabuasDaLei',
  'ColunaDeFogo',
  'AncoraDaEsperanca',
  'CestoDeMana',
  'JaquimEBoaz',
  'HarpaDeDavi',
  'CoroaDaVida',
  'LampadaAcesa10',
  'LampadaAcesa25',
  'LampadaAcesa50',
  'PedraAngular',
]

// Definicoes compartilhadas do selo (anel de cera) — renderizar uma unica vez por pagina.
export function BadgeDefs() {
  return (
    <svg width="0" height="0" style={{ position: 'absolute' }} aria-hidden="true">
      <defs>
        <g id="badge-shell">
          <circle
            cx="60"
            cy="60"
            r="54"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.4"
            strokeDasharray="1.2 3.6"
            strokeLinecap="round"
            opacity="0.75"
          />
          <circle cx="60" cy="60" r="47" fill="none" stroke="currentColor" strokeWidth="1.1" opacity="0.55" />
          <circle cx="60" cy="60" r="47" fill="url(#badge-face-fill)" stroke="currentColor" strokeWidth="1.4" />
        </g>
        <radialGradient id="badge-face-fill" cx="35%" cy="30%" r="75%">
          <stop offset="0%" stopColor="currentColor" stopOpacity="0.16" />
          <stop offset="100%" stopColor="currentColor" stopOpacity="0.03" />
        </radialGradient>
      </defs>
    </svg>
  )
}

const ICON_PATHS: Record<string, ReactNode> = {
  SarcaArdente: (
    <>
      <path
        d="M60 78c-10 0-18-8-18-16 0-6 4-10 8-10-2-6 2-11 8-11 4 0 7 2 8 6 5 0 9 4 9 9 0 5-3 8-6 9 3 1 5 4 5 8 0 6-7 5-14 5z"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.6"
        strokeLinejoin="round"
      />
      <path d="M60 30c-3 6-6 9-6 14 0 4 3 7 6 7s6-3 6-7c0-5-3-8-6-14z" fill="currentColor" />
      <path d="M50 40c-2 4-4 6-4 9 0 3 2 5 4 5s4-2 4-5c0-3-2-5-4-9z" fill="currentColor" opacity="0.7" />
      <path d="M70 40c2 4 4 6 4 9 0 3-2 5-4 5s-4-2-4-5c0-3 2-5 4-9z" fill="currentColor" opacity="0.7" />
    </>
  ),
  TabuasDaLei: (
    <>
      <path d="M42 40c0-3 2-5 5-5h4v42h-9z" fill="none" stroke="currentColor" strokeWidth="2.6" strokeLinejoin="round" />
      <path d="M78 40c0-3-2-5-5-5h-4v42h9z" fill="none" stroke="currentColor" strokeWidth="2.6" strokeLinejoin="round" />
      <path d="M51 35a9 9 0 0 1 18 0" fill="none" stroke="currentColor" strokeWidth="2.6" />
      <path
        d="M50 46h6M50 53h6M50 60h6M64 46h6M64 53h6M64 60h6"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        opacity="0.8"
      />
    </>
  ),
  ColunaDeFogo: (
    <>
      <path d="M60 32c-3 5-5 8-5 12 0 3 2 5 5 5s5-2 5-5c0-4-2-7-5-12z" fill="currentColor" />
      <path
        d="M50 78c0-13 4-18 4-24 0-4 3-6 6-6s6 2 6 6c0 6 4 11 4 24 0 6-4 10-10 10s-10-4-10-10z"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.6"
        strokeLinejoin="round"
      />
      <path d="M54 56c1 6-1 10-1 16M66 56c-1 6 1 10 1 16" stroke="currentColor" strokeWidth="1.6" opacity="0.6" />
    </>
  ),
  AncoraDaEsperanca: (
    <>
      <circle cx="60" cy="42" r="6" fill="none" stroke="currentColor" strokeWidth="2.6" />
      <path d="M60 48v34" stroke="currentColor" strokeWidth="2.6" />
      <path d="M48 56h24" stroke="currentColor" strokeWidth="2.6" />
      <path
        d="M60 82c-9 0-16-6-17-15M60 82c9 0 16-6 17-15"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.6"
        strokeLinecap="round"
      />
    </>
  ),
  CestoDeMana: (
    <>
      <path
        d="M40 58h40l-5 20a4 4 0 0 1-4 3H49a4 4 0 0 1-4-3z"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.6"
        strokeLinejoin="round"
      />
      <path d="M42 58l4-6h28l4 6M46 64h28M44 71h32" stroke="currentColor" strokeWidth="1.6" opacity="0.7" />
      <circle cx="50" cy="50" r="2.6" fill="currentColor" />
      <circle cx="60" cy="46" r="2.6" fill="currentColor" />
      <circle cx="70" cy="50" r="2.6" fill="currentColor" />
      <circle cx="60" cy="52" r="2.6" fill="currentColor" />
    </>
  ),
  JaquimEBoaz: (
    <>
      <path d="M38 44h44v6H38z" fill="currentColor" opacity="0.85" />
      <rect x="40" y="50" width="8" height="30" fill="none" stroke="currentColor" strokeWidth="2.4" />
      <rect x="72" y="50" width="8" height="30" fill="none" stroke="currentColor" strokeWidth="2.4" />
      <path d="M40 80h40" stroke="currentColor" strokeWidth="2.4" />
    </>
  ),
  HarpaDeDavi: (
    <>
      <path d="M42 78V52c0-10 8-18 18-18" fill="none" stroke="currentColor" strokeWidth="2.8" strokeLinecap="round" />
      <path
        d="M78 78V60c0-14-10-22-18-22"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.8"
        strokeLinecap="round"
        opacity="0.55"
      />
      <path d="M46 44v34M52 40v38M58 38v40M64 40v38M70 44v34" stroke="currentColor" strokeWidth="1.4" opacity="0.75" />
    </>
  ),
  CoroaDaVida: (
    <>
      <path
        d="M36 52l7 22h34l7-22-13 10-11-16-11 16z"
        fill="currentColor"
        stroke="currentColor"
        strokeWidth="1.4"
        strokeLinejoin="round"
      />
      <path d="M40 78h40" stroke="currentColor" strokeWidth="2.8" strokeLinecap="round" />
      <circle cx="60" cy="44" r="2.6" fill="currentColor" />
    </>
  ),
  LampadaAcesa10: (
    <>
      <path
        d="M40 66c0-9 9-16 20-16s20 7 20 16-9 12-20 12-20-3-20-12z"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.6"
        strokeLinejoin="round"
      />
      <path d="M78 62c5-1 9 1 9 4s-4 5-9 4" fill="none" stroke="currentColor" strokeWidth="2.2" />
      <path d="M60 50v-6" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" />
      <path d="M60 38c-2 3-4 5-4 8 0 2 2 4 4 4s4-2 4-4c0-3-2-5-4-8z" fill="currentColor" />
    </>
  ),
  PedraAngular: (
    <>
      <path
        d="M60 34l-24 14v10l24 14 24-14V48z"
        fill="none"
        stroke="currentColor"
        strokeWidth="2.4"
        strokeLinejoin="round"
      />
      <path
        d="M36 58v18l24 14 24-14V58l-24 14z"
        fill="currentColor"
        opacity="0.18"
        stroke="currentColor"
        strokeWidth="2.4"
        strokeLinejoin="round"
      />
      <path d="M60 62v24" stroke="currentColor" strokeWidth="1.6" opacity="0.5" />
    </>
  ),
}

ICON_PATHS.LampadaAcesa25 = ICON_PATHS.LampadaAcesa10
ICON_PATHS.LampadaAcesa50 = ICON_PATHS.LampadaAcesa10

export function BadgeIcon({ code, className }: { code: string; className?: string }) {
  const info = BADGE_CATALOG[code]
  const path = ICON_PATHS[code]
  if (!info || !path) return null

  return (
    <svg viewBox="0 0 120 120" className={className} style={{ color: info.cor }} role="img" aria-label={info.nome}>
      <use href="#badge-shell" />
      {path}
    </svg>
  )
}
