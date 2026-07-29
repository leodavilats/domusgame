# GC Domus — Arquitetura (v1)

> Etapa 5 de 7.

---

## 1. Princípio norteador

> **Um desenvolvedor, 30 usuários, uma rodada por semana.**
> A arquitetura precisa ser *correta nas regras* e *barata em cerimônia*.

Aplicamos, portanto, uma versão enxuta e honesta das práticas pedidas:

| Prática pedida | Como aplicamos | Por que não a versão completa |
| --- | --- | --- |
| Clean Architecture | 3 projetos: `Domain` (puro) → `Infrastructure` → `Api`. Dependências apontam para dentro. | 5–7 projetos com interfaces espelhadas triplicariam o custo de cada mudança |
| SOLID | Domínio rico com invariantes; `ScoringPolicy` isolada; handlers com uma responsabilidade | — |
| CQRS | Separação **lógica**: escrita passa pelo domínio; leitura usa projeções e SQL direto | Sem MediatR, sem *event sourcing*, sem banco de leitura separado |
| Vertical Slice | `Features/<Assunto>/<Operação>.cs` com endpoint + handler + DTOs juntos | Camadas horizontais espalham cada mudança em 6 arquivos |
| Repository | `DbContext` usado diretamente nos handlers (já é Unit of Work + Repository) | Uma camada de repositórios genéricos só esconderia o EF Core |

**Nada de:** microserviços, mensageria, Redis, agendador, Kubernetes, gateway.
Se algum desses virar necessário, será por um requisito novo — não por antecipação.

---

## 2. Visão de implantação

```
              ┌──────────────────────────────────────────────┐
   Celular ───►│  Container único (ASP.NET Core 10)          │
   (PWA)      │                                              │
              │  /api/*   → Minimal API (features)           │
              │  /*       → wwwroot: React (build do Vite)   │
              └───────────────────┬──────────────────────────┘
                                  │ Npgsql / EF Core
                       ┌──────────▼──────────┐
                       │ PostgreSQL gerenciado│
                       └──────────────────────┘
```

**Decisão-chave (33):** o front-end React é compilado pelo Vite e servido como arquivos estáticos
pelo próprio ASP.NET. Consequências:

- **um** deploy, **um** container, **uma** origem → cookie de sessão `SameSite=Lax` sem CORS, sem
  token em `localStorage`, sem *refresh token*;
- Next.js seria um segundo runtime a manter e implantar para um app 100% autenticado, sem SEO e sem
  necessidade de SSR — custo sem retorno aqui.

Em desenvolvimento, o Vite roda em `5173` com proxy de `/api` para `5080` (mesma semântica de
mesma origem, sem CORS).

---

## 3. Estrutura de pastas

```
domusgame/
├─ docs/                            # 01..06 (este processo)
├─ backend/
│  ├─ Domus.sln
│  ├─ Directory.Build.props         # net10.0, nullable, warnings-as-errors
│  ├─ src/
│  │  ├─ Domus.Domain/              # ZERO dependências externas
│  │  │  ├─ Common/                 # Entity, DomainException, Guard
│  │  │  ├─ Seasons/                # Season, SeasonPodiumEntry, SeasonStatus
│  │  │  ├─ Rounds/                 # Round, Lesson, Question, AnswerOption, RoundScoringSettings
│  │  │  ├─ Attempts/               # Attempt, AttemptAnswer, ScoringPolicy, OptionShuffler
│  │  │  ├─ Participants/           # Participant, ParticipantRole
│  │  │  ├─ Rooms/                  # Room, RoomMembership
│  │  │  └─ Settings/               # AuditLogEntry
│  │  ├─ Domus.Infrastructure/
│  │  │  ├─ Persistence/            # DomusDbContext + Configurations/
│  │  │  ├─ Migrations/
│  │  │  ├─ Identity/               # AppUser, ClaimsFactory, DI do Identity
│  │  │  └─ Seed/                   # DatabaseSeeder (idempotente)
│  │  └─ Domus.Api/
│  │     ├─ Program.cs
│  │     ├─ Common/                 # ErrorHandling, CurrentUser, Results, Validation
│  │     ├─ Features/
│  │     │  ├─ Auth/                # Register, Login, Logout, Me, Google
│  │     │  ├─ Rooms/               # GetMyRoom, JoinRoom
│  │     │  ├─ Dashboard/           # GetDashboard
│  │     │  ├─ Rounds/              # ListRounds, GetRound, GetReview
│  │     │  ├─ Attempts/            # StartAttempt, GetAttemptState, SubmitAnswer, GetResult
│  │     │  ├─ Rankings/            # GetRoundRanking, GetSeasonRanking
│  │     │  ├─ Profile/             # UpdateProfile, DeleteAccount
│  │     │  └─ Admin/               # Seasons/ Rounds/ Questions/ Participants/ Invite/ Stats/
│  │     └─ wwwroot/                # build do front-end (gerado, não versionado)
│  └─ tests/
│     ├─ Domus.Domain.Tests/        # pontuação, invariantes, máquina de estados
│     └─ Domus.Api.Tests/           # integração: Testcontainers + WebApplicationFactory
├─ frontend/
│  ├─ src/
│  │  ├─ api/                       # client tipado + hooks
│  │  ├─ auth/                      # sessão e rotas protegidas
│  │  ├─ components/                # UI compartilhada
│  │  ├─ pages/                     # telas do participante
│  │  ├─ pages/admin/               # telas do administrador
│  │  └─ lib/                       # formatação de data/tempo, share, markdown
│  └─ public/                       # manifest do PWA e ícones
├─ .github/workflows/ci.yml
├─ docker-compose.yml               # app + postgres (desenvolvimento)
├─ Dockerfile                       # build do front + build do back → imagem final
└─ README.md
```

---

## 4. Regra de dependência

```
Domus.Domain          (nada)
   ▲
Domus.Infrastructure  (EF Core, Npgsql, Identity)
   ▲
Domus.Api             (ASP.NET Core)  ──► serve wwwroot (React)
```

`Domus.Domain` não referencia EF Core, ASP.NET nem Identity. Isso é verificado por um teste de
arquitetura simples (assembly do domínio sem referências proibidas).

---

## 5. Decisões técnicas

| Tema | Decisão | Racional |
| --- | --- | --- |
| Runtime | **.NET 10 (LTS)** | LTS atual; o runtime 10 já está na máquina de desenvolvimento |
| API | **Minimal API** com grupos por feature | Menos cerimônia que controllers para ~45 endpoints |
| Validação | `Guard` no domínio + validação de DTO no handler | Um validador de biblioteca a menos |
| Tempo | `TimeProvider` (BCL) injetado; domínio recebe `DateTimeOffset now` | Testável sem abstração própria (`FakeTimeProvider`) |
| Identidade | ASP.NET Core Identity + cookie `httpOnly`, `SameSite=Lax`, 60 dias | Login persistente no celular, sem token exposto a XSS |
| Papéis | `Participants.Role` → claim no login (sem tabelas de role) | Duas tabelas e um join a menos |
| Login social | **Google** (`AddGoogle`), opcional por configuração | Elimina a senha esquecida, que era o suporte manual mais frequente. Sem `ClientId`/`ClientSecret` o esquema não é registrado e o botão avisa em vez de estourar |
| Vínculo do Google | E-mail já cadastrado → `AddLoginAsync` na conta existente | Evita duas contas com o mesmo e-mail, que é o modo clássico de o participante "perder" o histórico |
| Sala | `Room` + `RoomMembership`, conteúdo pendurado em `Season.RoomId` | Conta e pertencimento separados: dá para ter várias salas sem tocar no cadastro nem no login (RN-41 a RN-46) |
| Salas por pessoa | Uma na v1; o modelo suporta várias | `RoomMemberships` já é N:N; a interface assume a primeira filiação (RN-44) |
| Erros | Middleware único → `ProblemDetails` (400/401/403/404/409/500) | Contrato de erro consistente |
| Serialização | `System.Text.Json`, camelCase, enums como string | Contrato legível no front |
| Logs | `ILogger` + console estruturado | Suficiente para a escala |
| Rate limit | `AddRateLimiter` nativo em `/auth/*` e `submit-answer` | RNF-07 sem dependência externa |
| Front-end | React 19 + TypeScript + Vite + Tailwind 4 + React Router | Stack mínima; **sem** biblioteca de estado ou de data-fetching |
| Estado remoto | hooks próprios (`useApi`, `useMutation`) sobre `fetch`, com cache em memória *stale-while-revalidate* | ~150 linhas resolvem o que TanStack Query resolveria — uma dependência a menos. Sem o cache, cada troca de aba mostrava spinner |
| Carregamento | Área administrativa em `lazy()`, fora do pacote principal | Uma pessoa usa o admin; as outras 30 não precisam baixá-lo |
| Cache HTTP | `/assets/*` (nomes com hash) `immutable, max-age=1 ano`; `index.html` sempre revalidado | Sem isso o navegador paga uma ida e volta antes de renderizar, em toda visita |
| PWA | `manifest.webmanifest` + ícones, **sem service worker** | Instalável; o quiz exige rede de propósito (RNF-03) |
| Cache de estáticos | `/assets/*` (nomes com hash) → `immutable, max-age=1 ano`; `index.html` → `no-cache` | Segunda visita não paga ida e volta na rede; o HTML sempre aponta para os assets do deploy atual |
| Code splitting | Área administrativa em chunks próprios (`lazy`) | Quem nunca abre o admin não baixa o admin |
| Estado remoto | `useApi` com cache *stale-while-revalidate* por rota | Trocar de aba deixa de mostrar spinner; a revalidação acontece por trás |
| Testes | xUnit puro (`Assert`); Testcontainers na integração | Foco nas regras que doem (RNF-11). Sem biblioteca de asserção: FluentAssertions 8 mudou para licença comercial e o ganho de legibilidade não paga a dependência |

---

## 6. Contrato da API

Todas as rotas sob `/api`. Autenticação por cookie. Erros em `ProblemDetails`.

### Autenticação

| Método | Rota | Descrição |
| --- | --- | --- |
| `POST` | `/api/auth/register` | `{ displayName, email, password }` — sem convite (RN-34) |
| `POST` | `/api/auth/login` | `{ email, password }` |
| `POST` | `/api/auth/logout` | |
| `GET` | `/api/auth/me` | sessão atual (com a sala, quando houver) ou 401 |
| `GET` | `/api/auth/google/start` | `?displayName=` → redireciona ao Google (UC-02) |
| `GET` | `/api/auth/google/callback` | vincula por e-mail ou cria a conta e autentica |

### Participante

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/api/rooms/mine` | minhas salas (0 ou 1 na v1) |
| `POST` | `/api/rooms/join` | `{ inviteCode }` — idempotente (UC-14, RN-43) |
| `GET` | `/api/dashboard` | rodada corrente + estado + pontuação + streak (UC-03). Sem sala: devolve tudo vazio, não erro |
| `GET` | `/api/rounds` | histórico de rodadas publicadas com meu resultado (UC-11) |
| `GET` | `/api/rounds/{id}` | lição + metadados + meu resumo (UC-04) |
| `GET` | `/api/rounds/{id}/review` | gabarito — **403 se aberta** (UC-09, RN-21) |
| `POST` | `/api/rounds/{id}/attempts` | inicia tentativa (idempotente) (UC-05) |
| `GET` | `/api/rounds/{id}/attempts/current` | estado + pergunta corrente (UC-07) |
| `POST` | `/api/attempts/{id}/answers` | `{ questionId, selectedOptionId? }` (UC-06) |
| `GET` | `/api/attempts/{id}/result` | resultado (sem gabarito se aberta) (UC-08) |
| `GET` | `/api/rankings/round/{roundId}` | ranking semanal (UC-10) |
| `GET` | `/api/rankings/season` | `?seasonId=` (default: ativa) (UC-10) |
| `PUT` | `/api/profile` | `{ displayName, avatarUrl, showInRanking }` (UC-12) |
| `POST` | `/api/profile/delete` | anonimiza a conta (UC-12) — `POST` porque a confirmação vai no corpo |

### Administrador (`/api/admin/*`, exige claim `role=Admin`)

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET`/`POST` | `/seasons` | listar / criar (UC-20) |
| `PUT` | `/seasons/{id}` | editar |
| `POST` | `/seasons/{id}/activate` | ativar (desativa a anterior) |
| `POST` | `/seasons/{id}/finish` | encerrar e congelar pódio (UC-29) |
| `GET` | `/seasons/{id}/export` | CSV do ranking (UC-30) |
| `GET`/`POST` | `/rounds` | listar / criar rascunho (UC-21) |
| `GET`/`PUT`/`DELETE` | `/rounds/{id}` | detalhe / editar / excluir — permitido enquanto a rodada não abriu e não tem participações (RN-10) |
| `PUT` | `/rounds/{id}/lesson` | lição (UC-22) |
| `POST` | `/rounds/{id}/questions` | criar pergunta + alternativas (UC-23) |
| `PUT`/`DELETE` | `/rounds/{id}/questions/{qid}` | editar / remover |
| `POST` | `/rounds/{id}/questions/{qid}/move` | `{ direction: "up" \| "down" }` |
| `GET` | `/rounds/{id}/validate` | problemas que impedem publicar (UC-24) |
| `POST` | `/rounds/{id}/publish` | publicar (UC-24) |
| `POST` | `/rounds/{id}/duplicate` | duplicar como rascunho (UC-25) |
| `GET` | `/rounds/{id}/stats` | estatísticas da rodada (UC-28) |
| `GET` | `/participants` | listar os membros da sala (UC-27) |
| `PUT` | `/participants/{id}/role` | promover/rebaixar |
| `GET`/`POST` | `/invite` | ver / rotacionar o código da sala (UC-26) |
| `GET` | `/stats/overview` | participação por semana (UC-28) |

Toda rota administrativa resolve primeiro **a sala do admin** e filtra por ela. Id de temporada ou
rodada de outra sala responde **404** (RN-45) — o único lugar onde essa decisão aparece é
`DomusQueries.RequireRoundInMyRoomAsync` / `AdminSeasonEndpoints.LoadAsync`, e é de propósito: um
único ponto para revisar quando surgir a segunda sala.

### Ferramentas de teste (`/api/admin/tools/*`, exige `DevTools__Enabled=true`)

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/tools/diagnostics` | estado do ambiente e contagem de registros — liberado mesmo com as ferramentas desligadas |
| `GET` | `/tools/audit` | últimas 30 ações auditadas — idem |
| `POST` | `/tools/demo-season` | temporada de teste com rodadas de um dia e variações de pergunta |
| `POST` | `/tools/rounds/{id}/open-now` · `/close-now` | desloca a janela da rodada |
| `POST` | `/tools/rounds/{id}/simulate` | `{ count }` participações fictícias |
| `DELETE` | `/tools/rounds/{id}/my-attempt` | apaga a própria tentativa |
| `POST` | `/tools/reset` | `{ scope, confirmation }` — `attempts` \| `content` \| `all`, com frase `LIMPAR` |

> `open-now` e `close-now` são o único caminho que chama `Round.OverrideWindowForTesting`, que
> ignora RN-10 de propósito. O nome é feio para aparecer em qualquer revisão, e o método é
> inalcançável sem as ferramentas ligadas.

### Contrato anti-vazamento (RNF-02)

Os DTOs de participante **não possuem** campo `isCorrect` durante a rodada aberta:

- `QuestionForAttemptDto { id, order, text, mediaType, mediaUrl, options: [{ id, text }], timeLimitSeconds, servedAt, serverNow }`
- `SubmitAnswerResultDto { accepted, timedOut, nextQuestionOrder?, attemptFinished }` — **sem** acerto
- `ReviewQuestionDto { ..., correctOptionId, selectedOptionId, explanation, points }` — só em rodada encerrada

Um teste de integração garante que a resposta de `/attempts/current` e `/answers` **não contém** a
string `isCorrect` nem o id da alternativa correta.

---

## 7. Fluxo de uma resposta (ponta a ponta)

```
Cliente                          API                          Domínio                 Banco
  │  POST /attempts/{id}/answers  │                               │                     │
  ├──────────────────────────────►│ carrega Attempt + Round        │                     │
  │                               ├───────────────────────────────────────────────────►  │
  │                               │ attempt.Submit(round, qId,     │                     │
  │                               │        optionId, now)  ───────►│ valida ordem (I-A3) │
  │                               │                               │ elapsed = now-ServedAt
  │                               │                               │ ScoringPolicy        │
  │                               │                               │ atualiza somas       │
  │                               │◄──────────────────────────────┤ SubmitResult         │
  │                               │ SaveChanges (índice único      │                     │
  │                               │  protege duplicidade)          ├────────────────────►│
  │◄──────────────────────────────┤ { accepted, nextQuestionOrder } │                    │
```

Pontos de segurança embutidos: tempo do servidor (RNF-03), gabarito nunca serializado (RNF-02),
idempotência por `(AttemptId, QuestionId)` (RNF-05), tentativa única por `(RoundId, ParticipantId)`
(RNF-04).

---

## 8. Configuração

| Chave | Padrão | Uso |
| --- | --- | --- |
| `ConnectionStrings__Postgres` | — | conexão |
| `Database__ApplyMigrationsOnStartup` | `true` em Dev | migrations automáticas |
| `Gc__Name` | `GC Domus` | nome exibido |
| `Gc__InviteCode` | gerado | código inicial |
| `Admin__Email` / `Admin__Password` / `Admin__DisplayName` | — | administrador inicial |
| `Seed__Demo` | `false` | dados de demonstração |
| `App__PublicUrl` | `http://localhost:5080` | links de compartilhamento |
| `App__TimeZone` | `America/Sao_Paulo` | apresentação |

---

## 9. Qualidade

- `Directory.Build.props`: `Nullable=enable`, `TreatWarningsAsErrors=true`, `ImplicitUsings=enable`.
- CI (GitHub Actions): `dotnet build` → `dotnet test` (domínio) → `npm ci` → `npm run build` →
  `tsc --noEmit`. Testes de integração rodam no CI com serviço Postgres.
- Testes obrigatórios (RNF-11): tabela de pontuação, tolerância de rede, tempo esgotado, ordem das
  perguntas, idempotência, tentativa única concorrente, disponibilidade por relógio, vazamento de
  gabarito, validação de publicação, desempate de ranking.
