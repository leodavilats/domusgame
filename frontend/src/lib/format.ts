const TIME_ZONE = 'America/Sao_Paulo'

const dateTimeFormatter = new Intl.DateTimeFormat('pt-BR', {
  timeZone: TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
})

const dateFormatter = new Intl.DateTimeFormat('pt-BR', {
  timeZone: TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

const weekdayFormatter = new Intl.DateTimeFormat('pt-BR', {
  timeZone: TIME_ZONE,
  weekday: 'long',
  hour: '2-digit',
  minute: '2-digit',
})

export function formatDateTime(value: string | Date): string {
  return dateTimeFormatter.format(new Date(value))
}

export function formatDate(value: string | Date): string {
  return dateFormatter.format(new Date(value))
}

export function formatWeekday(value: string | Date): string {
  return weekdayFormatter.format(new Date(value))
}

export function formatDuration(ms: number): string {
  const totalSeconds = Math.max(0, Math.round(ms / 1000))
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60

  if (minutes === 0) return `${seconds}s`
  return `${minutes}min ${seconds.toString().padStart(2, '0')}s`
}

export function formatCountdown(targetIso: string, nowMs: number): string {
  const remaining = new Date(targetIso).getTime() - nowMs
  if (remaining <= 0) return 'agora'

  const seconds = Math.floor(remaining / 1000)
  const days = Math.floor(seconds / 86400)
  const hours = Math.floor((seconds % 86400) / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)

  if (days > 0) return `${days}d ${hours}h`
  if (hours > 0) return `${hours}h ${minutes}min`
  if (minutes > 0) return `${minutes}min`
  return `${seconds}s`
}

export function toLocalInput(iso: string | Date): string {
  const date = new Date(iso)
  const pad = (value: number) => String(value).padStart(2, '0')

  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(
    date.getMinutes(),
  )}`
}

export function fromLocalInput(value: string): string {
  return new Date(value).toISOString()
}

export function suggestedWindow(): { opensAt: string; closesAt: string } {
  const opens = new Date()
  const daysUntilSunday = (7 - opens.getDay()) % 7 || 7

  opens.setDate(opens.getDate() + daysUntilSunday)
  opens.setHours(13, 0, 0, 0)

  const closes = new Date(opens)
  closes.setDate(closes.getDate() + 6)
  closes.setHours(23, 59, 0, 0)

  return { opensAt: toLocalInput(opens), closesAt: toLocalInput(closes) }
}

export function formatPercent(value: number): string {
  return `${Math.round(value * 100)}%`
}

export function pluralize(count: number, singular: string, plural: string): string {
  return `${count} ${count === 1 ? singular : plural}`
}
