# GC Domus — Casos de Uso e Fluxos (v1)

> Etapa 2 de 7. Referências `RN-xx` / `RF-xx` apontam para [01-requisitos.md](01-requisitos.md).

---

## Índice

| ID | Caso de uso | Ator |
| --- | --- | --- |
| UC-01 | Cadastrar-se com código de convite | Visitante |
| UC-02 | Autenticar-se | Visitante |
| UC-03 | Ver painel inicial | Participante |
| UC-04 | Ler a lição da semana | Participante |
| UC-05 | Iniciar tentativa | Participante |
| UC-06 | Responder pergunta | Participante |
| UC-07 | Retomar tentativa interrompida | Participante |
| UC-08 | Ver resultado da tentativa | Participante |
| UC-09 | Revisar rodada encerrada com gabarito | Participante |
| UC-10 | Consultar rankings | Participante |
| UC-11 | Consultar histórico pessoal | Participante |
| UC-12 | Editar perfil / excluir conta | Participante |
| UC-13 | Compartilhar pontuação | Participante |
| UC-20 | Gerenciar temporadas | Administrador |
| UC-21 | Criar rodada em rascunho | Administrador |
| UC-22 | Cadastrar lição | Administrador |
| UC-23 | Gerenciar perguntas e alternativas | Administrador |
| UC-24 | Pré-visualizar e publicar rodada | Administrador |
| UC-25 | Duplicar rodada anterior | Administrador |
| UC-26 | Gerenciar código de convite | Administrador |
| UC-27 | Gerenciar participantes e papéis | Administrador |
| UC-28 | Consultar estatísticas | Administrador |
| UC-29 | Encerrar temporada e registrar pódio | Administrador |
| UC-30 | Exportar ranking em CSV | Administrador |
| UC-31 | Gerar senha temporária para um participante | Administrador |
| UC-31 | Gerar senha temporária para um participante | Administrador |

---

## UC-01 — Cadastrar-se com código de convite

**Ator:** Visitante · **Pré-condição:** possui um código de convite válido (RN-34)

**Fluxo principal**
1. Visitante acessa `/cadastro`.
2. Informa código de convite, nome de exibição, e-mail e senha.
3. Sistema valida o código contra o convite ativo.
4. Sistema cria a conta com papel `Participant` e `ShowInRanking = true`.
5. Sistema autentica e redireciona para o painel inicial.

**Exceções**
- **E1** Código inválido/expirado → "Código de convite inválido. Peça o código ao líder do GC." Nenhuma conta é criada.
- **E2** E-mail já cadastrado → oferece ir para o login.
- **E3** Nome de exibição já em uso → pede outro (nome é a identidade no ranking).
- **E4** Senha fraca (< 8 caracteres) → mensagem de validação.
- **E5** Muitas tentativas do mesmo IP → bloqueio temporário (RNF-07).

---

## UC-02 — Autenticar-se

**Fluxo principal**
1. Visitante acessa `/entrar` e informa e-mail + senha.
2. Sistema valida credenciais e emite **cookie de sessão `httpOnly`** de longa duação.
3. Redireciona para o painel inicial.

**Exceções**
- **E1** Credenciais inválidas → mensagem genérica ("E-mail ou senha inválidos"), sem revelar se o e-mail existe.
- **E3** Excesso de tentativas → bloqueio temporário.

---

## UC-03 — Ver painel inicial

**Ator:** Participante

**Fluxo principal**
1. Sistema identifica a temporada ativa e a rodada relevante.
2. Exibe o **cartão da rodada** conforme o estado derivado (RN-07):
   - **Agendada** → "Abre domingo às 13h" + contagem regressiva.
   - **Aberta, não iniciada** → botão "Responder o desafio" + tempo restante da janela.
   - **Aberta, em andamento** → botão "Continuar de onde parei" + nº de perguntas restantes.
   - **Aberta, concluída** → sua pontuação + "ranking sai quando a rodada encerrar" (RN-32).
   - **Encerrada** → sua pontuação + botão "Ver gabarito" + posição na semana.
3. Exibe pontuação e posição na temporada, streak de participação e link para a lição.

**Fluxos alternativos**
- **A1** Sem temporada ativa → estado vazio: "Nenhuma temporada em andamento."
- **A2** Sem rodada publicada na temporada → "A próxima rodada está sendo preparada."

---

## UC-04 — Ler a lição da semana

1. Participante abre a lição a partir do painel ou da lista de rodadas.
2. Sistema exibe título, referência bíblica, texto e link externo.

**Regra:** a lição é visível assim que a rodada abre, e permanece acessível para sempre. A lição de
rodada em rascunho ou agendada **não** é exibida (RN-09).

---

## UC-05 — Iniciar tentativa

**Pré-condições:** rodada **aberta** (RN-13) e nenhuma tentativa existente do participante (RN-14)

**Fluxo principal**
1. Participante clica em "Responder o desafio".
2. Sistema exibe a **tela de regras**: tentativa única, N perguntas, X segundos por pergunta, sem voltar, precisa de conexão estável.
3. Participante confirma.
4. Sistema cria a `Attempt` (`StartedAt = now`), com a ordem das alternativas embaralhada por semente derivada da tentativa (RN-16).
5. Sistema entrega a **primeira pergunta** e registra `ServedAt` no servidor (RN-17).

**Exceções**
- **E1** Rodada fechada entre o carregamento e o clique → "Esta rodada foi encerrada."
- **E2** Tentativa já existe → redireciona para retomada (UC-07) ou resultado (UC-08).
- **E3** Duas requisições simultâneas → a restrição única do banco falha na segunda; o sistema trata o conflito e retorna a tentativa existente (RNF-04, RNF-05).

---

## UC-06 — Responder pergunta

**Pré-condições:** tentativa em andamento, rodada aberta, pergunta entregue e ainda não respondida

**Fluxo principal**
1. Participante seleciona uma alternativa e confirma.
2. Sistema calcula `elapsed = now − ServedAt` (relógio do servidor, RNF-03).
3. Se `elapsed ≤ timeLimit + 3s` → compara com o gabarito, calcula base (RN-23) e bônus (RN-26), persiste a `AttemptAnswer`.
4. Se excedeu → registra `TimedOut`, 0 ponto (RN-18).
5. Sistema responde **sem revelar se acertou** (RN-21) e entrega a próxima pergunta com novo `ServedAt`.
6. Ao responder a última, a tentativa é concluída (`CompletedAt`, RN-20) e o participante vai ao resultado (UC-08).

**Fluxos alternativos**
- **A1** Cronômetro zera no cliente → envia resposta em branco automaticamente.
- **A2** Participante não envia nada e volta depois → a pergunta é marcada como esgotada na próxima interação (RN-18).
- **A3** Reenvio da mesma pergunta (duplo clique / retry de rede) → operação idempotente: retorna o mesmo resultado, sem repontuar (RNF-05).

**Exceções**
- **E1** Alternativa não pertence à pergunta → 400, nada é gravado.
- **E2** Pergunta fora de ordem (tentando pular ou voltar) → 409 (RN-15).
- **E3** Rodada fechou durante a tentativa → tentativa é finalizada com o que já foi pontuado (RN-20).

---

## UC-07 — Retomar tentativa interrompida

1. Participante volta ao painel e vê "Continuar de onde parei".
2. Sistema localiza a primeira pergunta sem resposta.
3. Se a pergunta anterior estava entregue e expirou, é fechada como esgotada.
4. Sistema entrega a pergunta atual com novo `ServedAt` e segue em UC-06.

**Exceção:** rodada já fechada → não é possível retomar; vai ao resultado parcial (RN-19, RN-20).

---

## UC-08 — Ver resultado da tentativa

1. Sistema exibe: pontos totais, acertos/total, tempo total, pontuação máxima possível.
2. Se a rodada **ainda está aberta** → sem gabarito, sem ranking, com aviso "o gabarito e o ranking saem no encerramento" (RN-21, RN-32).
3. Se a rodada **está encerrada** → mostra posição na semana e acesso à revisão (UC-09).
4. Oferece o compartilhamento (UC-13).

---

## UC-09 — Revisar rodada encerrada com gabarito

**Pré-condição:** `now > ClosesAt` (RN-21)

1. Sistema lista cada pergunta com: enunciado, mídia, alternativa correta, alternativa escolhida, explicação, pontos obtidos e tempo gasto.
2. Perguntas esgotadas aparecem sinalizadas como "tempo esgotado".

**Exceção:** rodada aberta → 403; a interface nem oferece o caminho.

---

## UC-10 — Consultar rankings

1. Participante escolhe **Semana** ou **Temporada**.
2. **Semana**: só de rodadas encerradas; ordena por pontos ↓, tempo total ↑ (RN-30).
3. **Temporada**: soma dos pontos das rodadas encerradas; desempate por tempo acumulado (RN-31).
4. Cada linha mostra posição, nome de exibição, foto, pontos e tempo. A linha do próprio usuário é destacada e sempre visível, mesmo fora do top exibido.
5. Quem optou por não aparecer é omitido da lista, mas vê sua própria posição real (RN-22).

---

## UC-11 — Consultar histórico pessoal

1. Sistema lista as rodadas da temporada (e temporadas anteriores) com: semana, título, pontos, acertos, tempo, posição.
2. Rodadas não respondidas aparecem com 0 e "não participou" (RN-33).
3. Cada linha leva à revisão (UC-09), se encerrada.

---

## UC-12 — Editar perfil / excluir conta

1. Participante altera nome de exibição, URL da foto e a preferência de aparecer no ranking.
2. Para excluir a conta, confirma digitando o nome de exibição.
3. Sistema anonimiza: `DisplayName = "Participante removido"`, e-mail e credenciais apagados, tentativas preservadas sem vínculo pessoal (RN-38).

---

## UC-13 — Compartilhar pontuação

1. Participante toca em "Compartilhar".
2. Sistema monta o texto (ex.: *"Fiz 128 pontos no desafio da semana 3 do GC Domus! 🔥"*) + link.
3. Usa o compartilhamento nativo do dispositivo; sem suporte, copia para a área de transferência.

---

## UC-20 — Gerenciar temporadas

1. Admin cria a temporada (nome, início, fim) e define qual é a ativa.
2. Sistema garante no máximo uma ativa (RN-02).
3. Admin pode renomear ou ajustar datas enquanto a temporada não estiver encerrada.

**Exceção:** ativar uma temporada nova desativa a anterior, com confirmação explícita.

---

## UC-21 — Criar rodada em rascunho

1. Admin informa número da semana, título, `OpensAt`, `ClosesAt`, pontos por acerto, bônus máximo e tempo por pergunta.
2. Sistema pré-preenche a janela com o padrão (domingo 13h → sábado 23h59) e os parâmetros padrão (10 / 5 / 45s).
3. Rodada nasce em `Draft` (RN-09).

**Exceções**
- **E1** Semana repetida na temporada → 409 (RN-11).
- **E2** `ClosesAt ≤ OpensAt` → validação.

---

## UC-22 — Cadastrar lição

1. Admin preenche título, referência bíblica, conteúdo (markdown) e link opcional.
2. Sistema salva a lição vinculada à rodada (1:1).

---

## UC-23 — Gerenciar perguntas e alternativas

**Pré-condição:** rodada ainda não aberta — `Draft` ou `Published` agendada (RN-10)

1. Admin adiciona pergunta: enunciado, mídia opcional (tipo + URL), explicação.
2. Adiciona de 2 a 5 alternativas e marca **exatamente uma** como correta (RN-08).
3. Pode reordenar, editar e remover perguntas e alternativas.

**Exceções**
- **E1** Nenhuma ou mais de uma alternativa correta → validação impede salvar.
- **E2** Rodada já aberta → 409 ("rodada que já abriu não pode ser alterada").

---

## UC-24 — Pré-visualizar e publicar rodada

1. Admin abre a pré-visualização e vê exatamente o que o participante verá (sem destacar a correta).
2. Ao publicar, o sistema valida: lição preenchida, ≥ 1 pergunta, alternativas válidas, janela coerente, semana única, **sem sobreposição com outra rodada publicada** (RN-08, RN-11, RN-12).
3. Rodada passa a `Published`; a abertura é automática pelo relógio (RN-07).
4. Sistema registra a ação na auditoria (RNF-08).

**Exceção:** qualquer validação falha → publicação recusada com a lista de problemas.

---

## UC-25 — Duplicar rodada anterior

1. Admin escolhe "Duplicar" em uma rodada existente.
2. Sistema cria um `Draft` com as mesmas perguntas e alternativas, número da semana incrementado e janela deslocada em 7 dias.
3. A lição é copiada em branco (título sugerido), para ser reescrita.

---

## UC-26 — Gerenciar código de convite

1. Admin visualiza o código ativo e o total de cadastros feitos com ele.
2. Pode gerar um novo, invalidando o anterior (RN-35), com confirmação.

---

## UC-27 — Gerenciar participantes e papéis

1. Admin lista participantes com data de entrada, pontos na temporada e última participação.
2. Pode promover a `Admin` ou rebaixar a `Participant`.

**Exceção:** não é possível remover o próprio papel de administrador se for o último admin.

---

## UC-28 — Consultar estatísticas

1. Admin vê, por rodada encerrada: participação (respondentes / total), média e mediana de pontos, tempo médio por pergunta, ranking, e as perguntas ordenadas por menor índice de acerto.
2. Na rodada aberta: quem já respondeu e **quem ainda falta** (para lembrete no grupo).
3. Na temporada: evolução da participação por semana.

---

## UC-29 — Encerrar temporada e registrar pódio

1. Admin encerra a temporada.
2. Sistema calcula o ranking final e registra os 3 primeiros no histórico, de forma congelada (RN-04).
3. Temporada encerrada não recebe novas rodadas.

**Exceção:** existe rodada aberta ou agendada → alerta e confirmação explícita.

---

## UC-30 — Exportar ranking em CSV

1. Admin solicita a exportação do ranking da temporada.
2. Sistema gera CSV: posição, nome, pontos, tempo total, rodadas respondidas.

---

## UC-31 — Gerar senha temporária para um participante

**Ator:** Administrador · **Motivação:** não há recuperação por e-mail na v1 (RN-40)

**Fluxo principal**
1. Admin abre **Pessoas** e escolhe *Redefinir senha* na linha do participante.
2. Sistema pede confirmação, avisando que a senha atual deixará de valer.
3. Sistema gera uma senha pronunciável (ex.: `tamu-4729`), aplica na conta, limpa bloqueio por
   tentativas e registra a ação na auditoria.
4. Sistema exibe a senha **uma única vez**; o admin a repassa pelo grupo.

**Exceções**
- **E1** Conta removida → 409, não é possível redefinir.
- **E2** Participante tentando usar a rota → 403.

---

## UC-31 — Gerar senha temporária para um participante

**Ator:** Administrador · **Motivação:** sem serviço de e-mail, não existe "esqueci minha senha"

1. Admin abre **Pessoas** e escolhe *Redefinir senha* no participante.
2. Sistema confirma a ação (a senha atual deixa de valer).
3. Sistema gera uma senha pronunciável (ex.: `tamu-4729`), grava o hash, limpa bloqueio por
   tentativas e registra na auditoria.
4. A senha é exibida **uma única vez**, com botão de compartilhar.
5. O participante entra com ela e pode seguir usando ou trocá-la depois.

**Exceções**
- **E1** Conta removida → 409.
- **E2** Participante tentando usar a rota → 403.

> A senha não é armazenada em texto claro em lugar nenhum: se o admin fechar a tela sem
> anotar, o caminho é gerar outra.

---

## Fluxo crítico ponta a ponta (semana típica)

```
Sábado    Admin: cria rascunho → lição → 8 perguntas → pré-visualiza → publica (RN-08)
Domingo 13h  Rodada abre sozinha (relógio, RN-07)  →  divulgação manual no WhatsApp
Domingo–Sábado
          Participante: painel → regras → tentativa única
              ├─ pergunta 1..8, 45s cada, tempo medido no servidor (RN-17)
              ├─ correta: 10 pts + até 5 de bônus (RN-23, RN-26)
              └─ conexão caiu? retoma na primeira sem resposta (RN-19)
          Participante: vê a própria pontuação (sem gabarito, sem ranking — RN-21/32)
          Admin: acompanha quem falta responder (UC-28)
Sábado 23h59 Rodada encerra sozinha
          → gabarito liberado (UC-09)
          → ranking semanal publicado (UC-10)
          → pontos somados na temporada (RN-31)
Fim do trimestre
          Admin encerra a temporada → pódio congelado (RN-04) → CSV para a premiação (UC-30)
```

---

## Matriz de autorização

| Rota / operação | Visitante | Participante | Admin |
| --- | :---: | :---: | :---: |
| Cadastro, login | ✅ | — | — |
| Painel, lição, rodadas publicadas | ❌ | ✅ | ✅ |
| Iniciar tentativa / responder | ❌ | ✅ | ✅ |
| Gabarito de rodada **aberta** | ❌ | ❌ | ✅ (só na pré-visualização/admin) |
| Gabarito de rodada **encerrada** | ❌ | ✅ | ✅ |
| Rankings | ❌ | ✅ | ✅ |
| Rodadas em rascunho | ❌ | ❌ | ✅ |
| CRUD de temporada/rodada/lição/pergunta | ❌ | ❌ | ✅ |
| Convite, papéis, estatísticas, CSV | ❌ | ❌ | ✅ |
