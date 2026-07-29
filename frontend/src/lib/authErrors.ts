const messages: Record<string, string> = {
  google: 'Não foi possível concluir o login com o Google. Tente de novo.',
  'google-indisponivel': 'O login com o Google não está configurado neste ambiente.',
  'google-sem-email': 'A sua conta do Google não liberou o e-mail. Use e-mail e senha.',
  nome: 'O nome da sua conta do Google não serve como nome de exibição. Crie a conta com e-mail e senha.',
  'nome-em-uso': 'Já existe alguém com esse nome de exibição. Crie a conta com e-mail e senha e escolha outro.',
  conta: 'Não foi possível criar a sua conta a partir do Google. Tente com e-mail e senha.',
}

export function describeAuthError(code: string | null): string | null {
  if (!code) return null
  return messages[code] ?? 'Não foi possível concluir a autenticação. Tente de novo.'
}
