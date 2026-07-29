interface SharePayload {
  title: string
  text: string
  url?: string
}

export async function share(payload: SharePayload): Promise<'shared' | 'copied' | 'failed'> {
  const url = payload.url ?? window.location.origin

  if (navigator.share) {
    try {
      await navigator.share({ title: payload.title, text: payload.text, url })
      return 'shared'
    } catch {
    }
  }

  try {
    await navigator.clipboard.writeText(`${payload.text} ${url}`)
    return 'copied'
  } catch {
    return 'failed'
  }
}

export function buildScoreMessage(roomName: string, weekNumber: number, points: number): string {
  return `Fiz ${points} pontos no desafio da semana ${weekNumber} do ${roomName}!`
}
