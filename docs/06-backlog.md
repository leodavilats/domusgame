# GC Domus — Backlog de Implementação (v1)

> Etapa 6 de 7. Tarefas pequenas, em ordem de execução. Cada tarefa é entregável e verificável.

---

## Legenda

`[ ]` pendente · `[x]` concluída · **DoD** = definição de pronto

## Situação atual

| Épico | Situação |
| --- | --- |
| 0 — Fundação | ✅ estrutura, solution, Dockerfile, compose e CI criados |
| 1 — Domínio | ✅ entidades, `ScoringPolicy`, `Attempt` e testes escritos |
| 2 — Persistência | ✅ contexto, configurações, Identity, seed e migration `InitialCreate` aplicada em banco limpo |
| 3 — Auth e perfil | ✅ cadastro com convite, login, Google opcional, perfil e exclusão |
| 4 — Participação | ✅ painel, tentativa, respostas, resultado, revisão, rankings |
| 5 — Administração | ✅ temporadas, rodadas, lição, perguntas, publicação, estatísticas, convite, CSV |
| 6–8 — Front-end | ✅ todas as telas; `npm run build` limpo |
| 9 — Fechamento | ✅ README, CLAUDE.md, migration e verificação ponta a ponta em container |

**Verificado com a aplicação em execução** (`docker compose up`):
build limpo, migration aplicada em banco vazio, seed idempotente, cadastro com convite, quiz com
cronômetro do servidor, ausência de gabarito no payload da rodada aberta, bloqueio de gabarito e
ranking durante a semana, liberação após o encerramento, área administrativa negada a participante,
rankings semanal e de temporada, estatísticas por rodada.

**Pendência conhecida:** a suíte automatizada (`dotnet test`) ainda não foi executada — exige o
.NET SDK 10 na máquina ou uma execução em container.

---

## Épico 0 — Fundação

| # | Tarefa | DoD |
| --- | --- | --- |
| 0.1 | Estrutura do repositório, `.gitignore`, `.editorconfig`, `README` | `git init` feito, árvore de pastas criada |
| 0.2 | `Domus.sln` + 3 projetos + 2 de teste + `Directory.Build.props` | `dotnet build` limpo, sem warnings |
| 0.3 | `docker-compose.yml` (app + postgres) e `Dockerfile` multi-stage | `docker compose up` sobe API e banco |
| 0.4 | CI no GitHub Actions (build back, testes, build front) | pipeline verde |

## Épico 1 — Domínio (sem banco, 100% testável)

| # | Tarefa | DoD |
| --- | --- | --- |
| 1.1 | `Common`: `Entity`, `Guard`, `DomainValidationException`, `DomainRuleException`, `NotFoundException` | — |
| 1.2 | `Participant` + invariantes I-P1..I-P3 | testes de nome e anonimização |
| 1.3 | `Season` + `SeasonPodiumEntry` + `Finish` | testes I-S1..I-S4 |
| 1.4 | `RoundScoringSettings` (VO) + faixas válidas | testes de limite |
| 1.5 | `Lesson` (owned) + `Question` + `AnswerOption` + I-Q1..I-Q5 | testes de alternativas (2–5, exatamente 1 correta) |
| 1.6 | `Round`: mutações só em `Draft`, ordem contígua, `ValidateForPublish`, `Publish` | testes I-R1..I-R7 |
| 1.7 | `Round.AvailabilityAt(now)` | testes Scheduled/Open/Closed nas bordas |
| 1.8 | `ScoringPolicy` | **tabela de verdade completa** da seção 5 do doc 03 |
| 1.9 | `OptionShuffler` determinístico | mesma tentativa → mesma ordem; tentativas diferentes → ordens diferentes |
| 1.10 | `Attempt.Start` / `ServeCurrentQuestion` / `Submit` / `ExpirePendingIfNeeded` / `Complete` | testes I-A1..I-A9 e da máquina de estados |
| 1.11 | `GcSettings` + `AuditLogEntry` | testes de rotação de convite |
| 1.12 | Teste de arquitetura: domínio sem EF/ASP.NET/Identity | falha se alguém adicionar a referência |

## Épico 2 — Persistência

| # | Tarefa | DoD |
| --- | --- | --- |
| 2.1 | `DomusDbContext` + configurações (owned types, índices, checks) | modelo compila e `dotnet ef dbcontext info` responde |
| 2.2 | Identity (`AppUser`, chave compartilhada com `Participant`, claims factory) | login emite claim `role` |
| 2.3 | Migration `InitialCreate` + índice único parcial da temporada ativa | `database update` cria o esquema |
| 2.4 | Seed idempotente: `GcSettings`, admin inicial | subir duas vezes não duplica nada |
| 2.5 | Seed de demonstração (temporada + 3 rodadas + 6 participantes + tentativas) | rankings e estatísticas com dados reais |

## Épico 3 — API: autenticação e perfil

| # | Tarefa | DoD |
| --- | --- | --- |
| 3.1 | Middleware de erros → `ProblemDetails`; `CurrentUser`; JSON camelCase + enums string | erro de domínio devolve o status correto |
| 3.2 | `POST /auth/register` com código de convite | conta criada; código inválido = 400 |
| 3.3 | `POST /auth/login`, `/logout`, `GET /auth/me` | cookie de 60 dias |
| 3.4 | Google (registrado só se configurado) | app sobe sem credenciais do Google |
| 3.5 | Rate limiting em `/auth/*` | 6ª tentativa em 1 min = 429 |
| 3.6 | `PUT /profile`, `DELETE /profile` | anonimização preserva tentativas |

## Épico 4 — API: participação (o coração)

| # | Tarefa | DoD |
| --- | --- | --- |
| 4.1 | `GET /dashboard` | os 5 estados do cartão da rodada (UC-03) |
| 4.2 | `POST /rounds/{id}/attempts` | tentativa única; 2ª chamada devolve a existente |
| 4.3 | `GET /rounds/{id}/attempts/current` | retomada correta após interrupção |
| 4.4 | `POST /attempts/{id}/answers` | pontuação do servidor; idempotente; fora de ordem = 409 |
| 4.5 | `GET /attempts/{id}/result` | sem gabarito com rodada aberta |
| 4.6 | `GET /rounds`, `GET /rounds/{id}` | histórico e lição |
| 4.7 | `GET /rounds/{id}/review` | 403 com rodada aberta; gabarito completo depois |
| 4.8 | `GET /rankings/round/{id}`, `/rankings/season` | desempate por tempo; `ShowInRanking` respeitado |
| 4.9 | **Teste anti-vazamento** de todas as rotas de rodada aberta | nenhuma resposta contém a alternativa correta |
| 4.10 | Teste de concorrência: 2 `POST /attempts` simultâneos | uma tentativa no banco, sem 500 |

## Épico 5 — API: administração

| # | Tarefa | DoD |
| --- | --- | --- |
| 5.1 | Autorização de admin em todo o grupo `/admin` | participante recebe 403 |
| 5.2 | CRUD de temporadas + ativar + encerrar com pódio | só uma ativa (índice parcial testado) |
| 5.3 | CRUD de rodadas (rascunho) + parâmetros de pontuação | edição de publicada = 409 |
| 5.4 | Lição | markdown salvo |
| 5.5 | CRUD de perguntas/alternativas + mover | ordem sempre contígua |
| 5.6 | `validate` + `publish` | lista de problemas legível; semana duplicada e janela sobreposta bloqueadas |
| 5.7 | `duplicate` | rascunho novo com as perguntas copiadas |
| 5.8 | Estatísticas da rodada e da temporada | participação, médias, perguntas mais difíceis, quem falta |
| 5.9 | Participantes + papéis | não é possível remover o último admin |
| 5.10 | Convite (ver/rotacionar) + auditoria | log gravado |
| 5.11 | Exportação CSV | abre corretamente em planilha (BOM UTF-8) |

## Épico 6 — Front-end: base

| # | Tarefa | DoD |
| --- | --- | --- |
| 6.1 | Vite + React + TS + Tailwind 4 + proxy `/api` | `npm run build` limpo |
| 6.2 | Cliente de API, tratamento de erro, `useApi`/`useMutation` | erro de rede exibe mensagem amigável |
| 6.3 | Contexto de sessão + rotas protegidas + rota de admin | sem sessão → `/entrar` |
| 6.4 | Layout mobile-first, navegação inferior, tema | usável em 360 px |
| 6.5 | Telas de login e cadastro (com convite) | fluxo completo com o back |
| 6.6 | PWA (manifest + ícones) + formatação de data/hora em Brasília | instalável no celular |

## Épico 7 — Front-end: participante

| # | Tarefa | DoD |
| --- | --- | --- |
| 7.1 | Painel inicial com os 5 estados e contagem regressiva | reflete o back |
| 7.2 | Tela da lição (markdown) | legível no celular |
| 7.3 | Tela de regras antes de iniciar | avisos de tentativa única e tempo |
| 7.4 | **Tela do quiz**: cronômetro, uma pergunta por vez, mídia, auto-envio ao zerar | tempo sincronizado com `serverNow` |
| 7.5 | Tela de resultado | mensagem correta com rodada aberta |
| 7.6 | Revisão com gabarito | só após encerramento |
| 7.7 | Rankings (semana/temporada) com destaque do usuário | pódio visível |
| 7.8 | Histórico pessoal | inclui rodadas não respondidas |
| 7.9 | Perfil + exclusão de conta | confirmação por digitação do nome |
| 7.10 | Compartilhamento nativo | fallback para copiar |

## Épico 8 — Front-end: administração

| # | Tarefa | DoD |
| --- | --- | --- |
| 8.1 | Layout e navegação do admin | separado do participante |
| 8.2 | Temporadas (listar/criar/ativar/encerrar/CSV) | — |
| 8.3 | Rodadas (listar/criar/editar/excluir rascunho) | estados visíveis (rascunho/agendada/aberta/encerrada) |
| 8.4 | Editor da lição | markdown com pré-visualização |
| 8.5 | Editor de perguntas (alternativas, correta, mídia, explicação, reordenar) | validação em tempo real |
| 8.6 | Pré-visualização + publicação com lista de problemas | publica só quando válido |
| 8.7 | Estatísticas | participação, médias, perguntas difíceis, quem falta responder |
| 8.8 | Participantes e convite | rotação com confirmação |

## Épico 9 — Fechamento

| # | Tarefa | DoD |
| --- | --- | --- |
| 9.1 | Revisão dos 8 critérios de aceite da v1 (doc 01, seção 11) | todos verificados |
| 9.2 | README com instalação, execução, deploy e variáveis | um estranho consegue subir o projeto |
| 9.3 | `CLAUDE.md` com convenções do projeto | contexto para sessões futuras |
| 9.4 | Roteiro de deploy (Postgres gerenciado + container) | passo a passo testado localmente |

---

## Ordem de execução

```
Épico 0 → Épico 1 → Épico 2 → Épico 3 → Épico 4 → Épico 6 → Épico 7 → Épico 5 → Épico 8 → Épico 9
                                          ▲                              ▲
                                 primeiro fluxo ponta a ponta      administração completa
```

Racional: o **fluxo de participação** (4 + 6 + 7) é o coração do produto e o maior risco técnico
(tempo, pontuação, vazamento). A administração completa (5 + 8) vem depois porque, no limite, o
admin poderia cadastrar a primeira rodada via seed. Assim o valor aparece antes do CRUD.
