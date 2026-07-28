interface SharePayload {
  title: string
  text: string
  url?: string
}

/** UC-13: usa o compartilhamento nativo do celular; sem suporte, copia para a area de transferencia. */
export async function share(payload: SharePayload): Promise<'shared' | 'copied' | 'failed'> {
  const url = payload.url ?? window.location.origin

  if (navigator.share) {
    try {
      await navigator.share({ title: payload.title, text: payload.text, url })
      return 'shared'
    } catch {
      // Usuário cancelou ou o navegador recusou: cai para a copia.
    }
  }

  try {
    await navigator.clipboard.writeText(`${payload.text} ${url}`)
    return 'copied'
  } catch {
    return 'failed'
  }
}

export function buildScoreMessage(gcName: string, weekNumber: number, points: number): string {
  return `Fiz ${points} pontos no desafio da semana ${weekNumber} do ${gcName}!`
}
