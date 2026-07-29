# GC Domus — Modelo de Domínio (v1)

> Etapa 3 de 7. Nomes de código em inglês (decisão 38).

---

## 1. Agregados

| Agregado | Raiz | Filhos | Responsabilidade |
| --- | --- | --- | --- |
| **Room** | `Room` | — | A sala do GC: nome e código de convite. Dona de todo o conteúdo |
| **RoomMembership** | `RoomMembership` | — | Filiação de um participante a uma sala |
| **Season** | `Season` | `SeasonPodiumEntry` | Período de competição e pódio congelado, **dentro de uma sala** |
| **Round** | `Round` | `Lesson` (owned), `Question`, `AnswerOption` | Conteúdo da semana, janela e parâmetros de pontuação |
| **Attempt** | `Attempt` | `AttemptAnswer` | Participação de uma pessoa em uma rodada: tempo e pontos |
| **Participant** | `Participant` | — | Identidade pública e preferências. Existe **sem** sala |
| **AuditLog** | `AuditLogEntry` | — | Rastro das ações administrativas |

**Fronteiras:** `Attempt` referencia `Round` e `Participant` **por id**, nunca por navegação entre
agregados. A pontuação é calculada dentro de `Attempt`, recebendo os parâmetros da rodada como
valor (`RoundScoringSettings`), o que mantém o histórico imutável (RN-28).

---

## 2. Diagrama

```
Room ──1:N── Season ──1:N── Round ──1:1(owned)── Lesson
  │             │              │
  │             │              └──1:N── Question ──1:N── AnswerOption
  │             │
  │             └──1:N── SeasonPodiumEntry ─────┐
  │                                             │ (referência por id)
  └──1:N── RoomMembership ──── ParticipantId ───┤
                                                │
Participant ──1:N── Attempt ──1:N── AttemptAnswer
                       │                 │
                       │ RoundId         │ QuestionId / SelectedOptionId
                       └─────────────────┘  (referências por id)

AuditLogEntry
```

`Participant` **não** aponta para `Room`: a filiação é uma entidade própria (`RoomMembership`), o
que deixa a mesma conta pertencer a várias salas quando isso for necessário (RN-44) sem alterar o
cadastro.

---

## 3. Enumerações

```csharp
enum SeasonStatus      { Draft, Active, Finished }  // Draft: criada, ainda não e a corrente
enum RoundStatus       { Draft, Published }          // persistido
enum RoundAvailability { Scheduled, Open, Closed }    // derivado do relógio (RN-07)
enum QuestionMediaType { None, Image, Audio }
enum AttemptStatus     { InProgress, Completed }
enum AnswerOutcome     { Pending, Correct, Incorrect, Blank, TimedOut }
enum ParticipantRole   { Participant, Admin }
```

---

## 4. Value objects

### `RoundScoringSettings`

| Campo | Padrão | Regra |
| --- | --- | --- |
| `PointsPerCorrectAnswer` | 10 | 1 a 100 |
| `MaxSpeedBonus` | 5 | 0 a 50 |
| `QuestionTimeLimitSeconds` | 45 | 10 a 300 |

Imutável. Copiado para dentro da `Attempt` no início da tentativa (RN-28).

### `Lesson` (owned)

`Title`, `ScriptureReference`, `Content` (markdown), `ExternalUrl?`.
Pode estar vazio em rascunho; obrigatório para publicar (RN-08).

---

## 5. Regras de pontuação — `ScoringPolicy`

Único lugar do sistema que sabe calcular pontos. Estático, puro, sem dependências.

```csharp
public static class ScoringPolicy
{
    public const int NetworkGraceSeconds = 3;                       // RN-17

    public static bool IsWithinTimeLimit(long elapsedMs, int limitSeconds) =>
        elapsedMs <= (limitSeconds + NetworkGraceSeconds) * 1000L;

    public static int BasePoints(bool isCorrect, RoundScoringSettings s) =>
        isCorrect ? s.PointsPerCorrectAnswer : 0;                   // RN-23, RN-24

    public static int SpeedBonus(bool isCorrect, long elapsedMs, RoundScoringSettings s)
    {
        if (!isCorrect || s.MaxSpeedBonus == 0) return 0;            // RN-26
        var limitMs = s.QuestionTimeLimitSeconds * 1000.0;
        var remaining = Math.Clamp(1.0 - elapsedMs / limitMs, 0.0, 1.0);
        return (int)Math.Round(s.MaxSpeedBonus * remaining, MidpointRounding.AwayFromZero);
    }

    public static int MaxPointsPerQuestion(RoundScoringSettings s) =>
        s.PointsPerCorrectAnswer + s.MaxSpeedBonus;                 // RN-27
}
```

**Tabela de verdade (padrões 10 / 5 / 45s):**

| Situação | elapsed | base | bônus | total |
| --- | --- | --- | --- | --- |
| Correta imediata | 0 s | 10 | 5 | 15 |
| Correta | 5 s | 10 | 4 | 14 |
| Correta | 15 s | 10 | 3 | 13 |
| Correta | 30 s | 10 | 2 | 12 |
| Correta no limite | 45 s | 10 | 0 | 10 |
| Correta com tolerância de rede | 47 s | 10 | 0 | 10 |
| Errada | qualquer | 0 | 0 | 0 |
| Em branco | qualquer | 0 | 0 | 0 |
| Tempo esgotado | > 48 s | 0 | 0 | 0 |

---

## 6. Entidades e invariantes

### `Season`

```
Id, RoomId, Name, StartsOn: DateOnly, EndsOn: DateOnly, Status, FinishedAt?
Podium: List<SeasonPodiumEntry>
```

| Invariante | Regra |
| --- | --- |
| I-S0 | `RoomId` obrigatório: toda temporada nasce dentro de uma sala (RN-41) |
| I-S1 | `Name` obrigatório (≤ 80 caracteres) |
| I-S2 | `StartsOn < EndsOn` |
| I-S3 | Temporada `Finished` não aceita novas rodadas nem alteração de datas (RN-04) |
| I-S4 | `Finish(podium)` só pode ser chamado uma vez; grava até 3 posições com nome e pontos congelados |
| I-S5 | A temporada nasce em `Draft`; `Activate()` a torna corrente e `Deactivate()` a devolve para `Draft` quando outra assume |

> A unicidade da temporada ativa (RN-02) é **por sala** (RN-46): índice único parcial
> `(RoomId, Status) WHERE Status = 1` no banco + verificação no serviço de aplicação (não é
> invariante de um único agregado).

### `Round`

```
Id, SeasonId, WeekNumber, Title, OpensAt, ClosesAt (UTC),
Status, PublishedAt?, Scoring: RoundScoringSettings, Lesson (owned),
Questions: List<Question>
```

| Invariante | Regra |
| --- | --- |
| I-R1 | `WeekNumber ≥ 1`; `Title` obrigatório (≤ 120) |
| I-R2 | `OpensAt < ClosesAt` |
| I-R3 | Qualquer mutação (lição, perguntas, janela, parâmetros) exige `IsEditableAt(now)`: `Draft`, ou `Published` ainda não aberta (RN-10) |
| I-R4 | `Questions` sempre com `Order` contíguo iniciando em 1 |
| I-R5 | `Publish(now)` exige: lição completa, ≥ 1 pergunta, cada pergunta com 2–5 alternativas e exatamente 1 correta, `OpensAt < ClosesAt` (RN-08) |
| I-R6 | `AvailabilityAt(now)`: `now < OpensAt` → `Scheduled`; `≤ ClosesAt` → `Open`; senão `Closed`. `Draft` nunca é `Open` para participante (RN-07, RN-09) |
| I-R7 | `MaxPoints = Questions.Count × (PointsPerCorrectAnswer + MaxSpeedBonus)` (RN-27) |

Métodos: `UpdateDetails`, `UpdateWindow`, `UpdateScoring`, `SetLesson`, `AddQuestion`,
`UpdateQuestion`, `RemoveQuestion`, `MoveQuestion`, `ValidateForPublish() → IReadOnlyList<string>`,
`Publish(now)`, `AvailabilityAt(now)`, `IsAnswerRevealedAt(now)`, `IsEditableAt(now)`.

> Todo mutador recebe `DateTimeOffset now`: a permissão de editar depende do relógio, não só do
> status persistido. A exclusão da rodada é do serviço de aplicação, que combina `IsEditableAt(now)`
> com a ausência de tentativas — o agregado `Round` não conhece `Attempt`.

> Semana única (RN-11) e janelas não sobrepostas (RN-12) são regras **entre** rodadas: validadas no
> serviço de aplicação com consulta ao banco + índice único em `(SeasonId, WeekNumber)`.

### `Question` / `AnswerOption`

```
Question:     Id, RoundId, Order, Text, MediaType, MediaUrl?, Explanation?, Options
AnswerOption: Id, QuestionId, Order, Text, IsCorrect
```

| Invariante | Regra |
| --- | --- |
| I-Q1 | `Text` obrigatório (≤ 500) |
| I-Q2 | `MediaType != None` ⇒ `MediaUrl` obrigatório e absoluto (http/https) |
| I-Q3 | `ReplaceOptions` exige 2 a 5 alternativas, texto não vazio, **exatamente uma** correta (RN-08) |
| I-Q4 | `Order` das alternativas contíguo iniciando em 1 |
| I-Q5 | `CorrectOption` é sempre resolvível em pergunta válida |

### `Participant`

```
Id (mesmo id do usuário de identidade), DisplayName, AvatarUrl?, ShowInRanking,
Role, JoinedAt, IsRemoved
```

| Invariante | Regra |
| --- | --- |
| I-P1 | `DisplayName` obrigatório, 2–40 caracteres, único (case-insensitive) |
| I-P2 | `Anonymize()` define `DisplayName = "Participante removido"`, limpa avatar, `IsRemoved = true`, `ShowInRanking = false` (RN-38) |
| I-P3 | Participante removido não pode ser promovido nem autenticar |

> `Participant` é domínio puro. As credenciais ficam em `AppUser` (ASP.NET Core Identity), na
> infraestrutura, compartilhando a mesma chave primária. O domínio nunca conhece Identity.

### `Attempt` — o agregado mais importante

```
Id, RoundId, ParticipantId, StartedAt, CompletedAt?, Status,
Scoring: RoundScoringSettings (cópia),  QuestionCount,
TotalPoints, CorrectCount, TotalTimeMs,
Answers: List<AttemptAnswer>
```

```
AttemptAnswer: Id, AttemptId, QuestionId, QuestionOrder, ServedAt, AnsweredAt?,
               SelectedOptionId?, Outcome, BasePoints, SpeedBonus, ElapsedMs
```

| Invariante | Regra |
| --- | --- |
| I-A1 | No máximo uma `Attempt` por `(RoundId, ParticipantId)` — índice único no banco (RN-14, RNF-04) |
| I-A2 | No máximo uma `AttemptAnswer` por `(AttemptId, QuestionId)` — índice único (RNF-05) |
| I-A3 | Perguntas são servidas **em ordem**; não é possível servir a pergunta N+1 sem resolver a N (RN-15) |
| I-A4 | Uma resposta já resolvida (`Outcome != Pending`) é imutável — reenvio é idempotente (RNF-05) |
| I-A5 | `TotalPoints`, `CorrectCount` e `TotalTimeMs` são somas derivadas, recalculadas a cada resolução |
| I-A6 | `ElapsedMs` vem sempre de `AnsweredAt − ServedAt` com relógio do servidor (RNF-03) |
| I-A7 | `TimedOut` ⇒ `ElapsedMs = QuestionTimeLimitSeconds × 1000` (custo cheio no desempate, RN-29) |
| I-A8 | `Complete()` quando todas as perguntas estão resolvidas **ou** a rodada fechou (RN-20) |
| I-A9 | Tentativa `Completed` não aceita novas respostas |

**Máquina de estados de uma resposta**

```
                 ServeCurrentQuestion(now)
        (vazio) ──────────────────────────► Pending  (ServedAt = now)
                                              │
       Submit(optionId, now) dentro do prazo  │  Submit(null) ou prazo estourado
        ┌─────────────────────────────────────┼──────────────────────────┐
        ▼                                     ▼                          ▼
    Correct / Incorrect                     Blank                     TimedOut
   (base + bônus / 0)                        (0)                        (0)
        └──────────────────── imutável a partir daqui (I-A4) ───────────────┘
```

**API do agregado**

```csharp
static Attempt Start(Round round, Guid participantId, DateTimeOffset now);
ServedQuestion? ServeCurrentQuestion(Round round, DateTimeOffset now);  // cria/retorna Pending
SubmitResult Submit(Round round, Guid questionId, Guid? selectedOptionId, DateTimeOffset now);
void ExpirePendingIfNeeded(DateTimeOffset now);                        // usado na retomada (RN-19)
void CompleteIfRoundClosed(Round round, DateTimeOffset now);            // RN-20
int NextQuestionOrder { get; }
bool IsFinished { get; }
```

`SubmitResult` **não** informa acerto ou erro (RN-21): devolve apenas
`{ AnswerId, Outcome ∈ {Resolved, TimedOut}, NextQuestionOrder?, AttemptFinished }`.
O front-end só descobre acertos após o encerramento da rodada.

### `Room`

```
Id, Name, InviteCode, NormalizedInviteCode, InviteRotatedAt, CreatedAt
```

```csharp
static Room Create(string name, string inviteCode, DateTimeOffset now);
void Rename(string name);
void RotateInvite(string inviteCode, DateTimeOffset now);
bool MatchesInvite(string? candidate);
static string GenerateCode(int length = 8);
```

| Invariante | Regra |
| --- | --- |
| I-R1 | `Name` com até 80 caracteres, obrigatório |
| I-R2 | `InviteCode` com 6–20 caracteres, apenas letras e números, guardado em maiúsculas |
| I-R3 | `NormalizedInviteCode` é único no banco: dois GCs não compartilham código (RN-42) |
| I-R4 | `RotateInvite(code, now)` substitui o código e registra a data; ninguém é expulso (RN-35) |

> Substituiu `GcSettings`, que era uma linha única com nome do GC e convite. A migração `AddRooms`
> converte aquela linha na primeira sala e filia todos os participantes existentes — sem isso o GC
> publicado perderia o acesso ao próprio conteúdo.

### `RoomMembership`

```
Id, RoomId, ParticipantId, JoinedAt
```

| Invariante | Regra |
| --- | --- |
| I-M1 | `(RoomId, ParticipantId)` é único: entrar duas vezes não duplica (RN-43) |
| I-M2 | `Join(room, participantId, now)` é a única forma de criar a filiação |
| I-M3 | Excluir a sala ou o participante remove a filiação em cascata |

### `AuditLogEntry`

```
Id, OccurredAt, ActorId?, ActorName, Action, Details
```

Append-only. Ações registradas: `RoundPublished`, `SeasonActivated`, `SeasonFinished`,
`InviteRotated`, `RoleChanged`, `AccountDeleted` (RNF-08).

---

## 7. Embaralhamento determinístico das alternativas (RN-16)

Sem persistir ordem: a semente vem de `AttemptId + QuestionId`, então recarregar a página devolve
sempre a mesma ordem para aquela pessoa, e ordens diferentes para pessoas diferentes.

```csharp
public static IReadOnlyList<AnswerOption> ShuffleFor(Guid attemptId, Question question)
{
    var seed = attemptId.GetHashCodeStable() ^ question.Id.GetHashCodeStable();
    return question.Options.OrderBy(o => Stable(seed, o.Id)).ToList();
}
```

`Stable` é um hash determinístico (`XxHash`/SHA-256 truncado) — nunca `Guid.GetHashCode()`, que
varia entre processos.

---

## 8. Cálculos derivados (não persistidos)

| Cálculo | Definição |
| --- | --- |
| **Ranking semanal** | Tentativas de rodada encerrada, ordem: `TotalPoints ↓`, `TotalTimeMs ↑` (RN-30) |
| **Ranking da temporada** | `SUM(TotalPoints) ↓`, `SUM(TotalTimeMs) ↑` sobre rodadas encerradas (RN-31) |
| **Streak** | Nº de rodadas encerradas consecutivas, da mais recente para trás, com tentativa do participante |
| **Taxa de participação** | `tentativas na rodada / participantes ativos` |
| **Índice de acerto da pergunta** | `respostas Correct / respostas resolvidas` |
| **Pontuação máxima da rodada** | I-R7 |

Com 30 participantes, tudo isso é consulta SQL direta — sem tabela materializada (RNF-12).

---

## 9. Erros de domínio

| Exceção | Uso | HTTP |
| --- | --- | --- |
| `DomainValidationException` | invariante de entrada violada | 400 |
| `DomainRuleException` | operação inválida para o estado atual (rodada fechada, tentativa duplicada, pergunta fora de ordem) | 409 |
| `NotFoundException` | agregado inexistente | 404 |

O domínio nunca lança `Exception` genérica e nunca conhece HTTP.
