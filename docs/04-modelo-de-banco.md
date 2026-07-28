# GC Domus — Modelo de Banco de Dados (v1)

> Etapa 4 de 7. PostgreSQL 16+, EF Core com migrations. Todo instante em **UTC**
> (`timestamptz`), apresentação em `America/Sao_Paulo` (RNF-09).

---

## 1. Diagrama

```
             ┌──────────────┐
             │   Seasons    │
             └──────┬───────┘
                    │ 1:N
             ┌──────▼───────┐        ┌───────────────┐
             │    Rounds    │───1:N──►│  Questions   │
             │ (+ Lesson,   │        └───────┬───────┘
             │  + Scoring)  │                │ 1:N
             └──────┬───────┘        ┌───────▼───────┐
                    │                │ AnswerOptions │
                    │ 1:N            └───────────────┘
             ┌──────▼───────┐
             │   Attempts   │───1:N──► AttemptAnswers
             └──────┬───────┘
                    │ N:1
             ┌──────▼───────┐  1:1   ┌──────────────┐
             │ Participants │◄──────►│   AspNetUsers│  (Identity: credenciais)
             └──────────────┘  PK    └──────────────┘

  SeasonPodiumEntries → Seasons        GcSettings (1 linha)     AuditLogs
```

`Lesson` e `RoundScoringSettings` são **owned types** — colunas dentro de `Rounds`, sem tabela
extra e sem join.

---

## 2. Tabelas

### `Seasons`

| Coluna | Tipo | Nulo | Notas |
| --- | --- | --- | --- |
| `Id` | `uuid` | não | PK |
| `Name` | `varchar(80)` | não | |
| `StartsOn` | `date` | não | |
| `EndsOn` | `date` | não | `> StartsOn` (I-S2) |
| `Status` | `int` | não | 0 = Draft, 1 = Active, 2 = Finished |
| `FinishedAt` | `timestamptz` | sim | |
| `CreatedAt` | `timestamptz` | não | |

Índices: `UX_Seasons_SingleActive` — único **parcial** sobre `Status` com filtro `WHERE "Status" = 1`
→ garante **uma única temporada ativa** (RN-02) no banco, não só na aplicação.

> Consequência prática: ativar outra temporada exige **duas gravações** (desativa a anterior,
> depois ativa a nova) dentro da mesma transação — o índice não tolera duas ativas nem por um
> instante. É o que `ActivateAsync` faz.

### `SeasonPodiumEntries`

| Coluna | Tipo | Nulo | Notas |
| --- | --- | --- | --- |
| `Id` | `uuid` | não | PK |
| `SeasonId` | `uuid` | não | FK → `Seasons` · **cascade** |
| `Position` | `int` | não | 1, 2 ou 3 |
| `ParticipantId` | `uuid` | não | sem FK: é snapshot histórico |
| `DisplayName` | `varchar(40)` | não | congelado (RN-04) |
| `TotalPoints` | `int` | não | |
| `TotalTimeMs` | `bigint` | não | |

Índice único: `(SeasonId, Position)`.

### `Rounds`

| Coluna | Tipo | Nulo | Notas |
| --- | --- | --- | --- |
| `Id` | `uuid` | não | PK |
| `SeasonId` | `uuid` | não | FK → `Seasons` · **restrict** |
| `WeekNumber` | `int` | não | ≥ 1 |
| `Title` | `varchar(120)` | não | |
| `OpensAt` | `timestamptz` | não | |
| `ClosesAt` | `timestamptz` | não | `> OpensAt` |
| `Status` | `int` | não | 0 = Draft, 1 = Published |
| `PublishedAt` | `timestamptz` | sim | |
| `CreatedAt` | `timestamptz` | não | |
| `Lesson_Title` | `varchar(160)` | não | owned; `''` em rascunho |
| `Lesson_ScriptureReference` | `varchar(160)` | não | owned |
| `Lesson_Content` | `text` | não | owned, markdown |
| `Lesson_ExternalUrl` | `varchar(500)` | sim | owned |
| `Scoring_PointsPerCorrectAnswer` | `int` | não | owned, default 10 |
| `Scoring_MaxSpeedBonus` | `int` | não | owned, default 5 |
| `Scoring_QuestionTimeLimitSeconds` | `int` | não | owned, default 45 |

Índices:
- único `(SeasonId, WeekNumber)` → RN-11
- `(SeasonId, Status, OpensAt)` → busca da rodada corrente
- `(Status, OpensAt, ClosesAt)` → cálculo de disponibilidade

Checks: `CK_Rounds_Window` (`ClosesAt > OpensAt`), `CK_Rounds_Scoring`
(`PointsPerCorrectAnswer BETWEEN 1 AND 100`, `MaxSpeedBonus BETWEEN 0 AND 50`,
`QuestionTimeLimitSeconds BETWEEN 10 AND 300`).

> Sobreposição de janelas (RN-12) é validada na aplicação — um índice de exclusão com `tstzrange`
> seria possível, mas exige SQL manual e a regra é barata de checar com 30 usuários.

### `Questions`

| Coluna | Tipo | Nulo | Notas |
| --- | --- | --- | --- |
| `Id` | `uuid` | não | PK |
| `RoundId` | `uuid` | não | FK → `Rounds` · **cascade** |
| `Order` | `int` | não | contíguo, inicia em 1 |
| `Text` | `varchar(500)` | não | |
| `MediaType` | `int` | não | 0 = None, 1 = Image, 2 = Audio |
| `MediaUrl` | `varchar(500)` | sim | obrigatório se `MediaType != 0` |
| `Explanation` | `varchar(1000)` | sim | exibida só após o encerramento |

Índice único: `(RoundId, Order)`.

### `AnswerOptions`

| Coluna | Tipo | Nulo | Notas |
| --- | --- | --- | --- |
| `Id` | `uuid` | não | PK |
| `QuestionId` | `uuid` | não | FK → `Questions` · **cascade** |
| `Order` | `int` | não | contíguo, inicia em 1 |
| `Text` | `varchar(300)` | não | |
| `IsCorrect` | `boolean` | não | exatamente uma por pergunta (regra de domínio) |

Índice único: `(QuestionId, Order)`.

### `Participants`

| Coluna | Tipo | Nulo | Notas |
| --- | --- | --- | --- |
| `Id` | `uuid` | não | PK — **mesmo id** de `AspNetUsers.Id` |
| `DisplayName` | `varchar(40)` | não | |
| `NormalizedDisplayName` | `varchar(40)` | não | `UPPER(DisplayName)`, único (I-P1) |
| `AvatarUrl` | `varchar(500)` | sim | |
| `ShowInRanking` | `boolean` | não | default `true` |
| `Role` | `int` | não | 0 = Participant, 1 = Admin |
| `JoinedAt` | `timestamptz` | não | |
| `IsRemoved` | `boolean` | não | default `false` |

Índices: único `NormalizedDisplayName`; `(IsRemoved)`.

### `Attempts`

| Coluna | Tipo | Nulo | Notas |
| --- | --- | --- | --- |
| `Id` | `uuid` | não | PK |
| `RoundId` | `uuid` | não | FK → `Rounds` · **restrict** |
| `ParticipantId` | `uuid` | não | FK → `Participants` · **restrict** |
| `StartedAt` | `timestamptz` | não | |
| `CompletedAt` | `timestamptz` | sim | |
| `Status` | `int` | não | 0 = InProgress, 1 = Completed |
| `QuestionCount` | `int` | não | congelado no início |
| `TotalPoints` | `int` | não | derivado (I-A5) |
| `CorrectCount` | `int` | não | derivado |
| `TotalTimeMs` | `bigint` | não | derivado |
| `Scoring_PointsPerCorrectAnswer` | `int` | não | **cópia** dos parâmetros (RN-28) |
| `Scoring_MaxSpeedBonus` | `int` | não | cópia |
| `Scoring_QuestionTimeLimitSeconds` | `int` | não | cópia |

Índices:
- **único `(RoundId, ParticipantId)`** → RN-14 / RNF-04 (a garantia real da tentativa única)
- `(RoundId, TotalPoints DESC, TotalTimeMs ASC)` → ranking semanal
- `(ParticipantId, RoundId)` → histórico

### `AttemptAnswers`

| Coluna | Tipo | Nulo | Notas |
| --- | --- | --- | --- |
| `Id` | `uuid` | não | PK |
| `AttemptId` | `uuid` | não | FK → `Attempts` · **cascade** |
| `QuestionId` | `uuid` | não | FK → `Questions` · **restrict** |
| `QuestionOrder` | `int` | não | denormalizado para ordenar sem join |
| `ServedAt` | `timestamptz` | não | relógio do servidor (RNF-03) |
| `AnsweredAt` | `timestamptz` | sim | |
| `SelectedOptionId` | `uuid` | sim | FK → `AnswerOptions` · **restrict** |
| `Outcome` | `int` | não | 0 Pending, 1 Correct, 2 Incorrect, 3 Blank, 4 TimedOut |
| `BasePoints` | `int` | não | persistido (RN-28) |
| `SpeedBonus` | `int` | não | persistido |
| `ElapsedMs` | `bigint` | não | persistido |

Índices: único `(AttemptId, QuestionId)` → RNF-05; `(AttemptId, QuestionOrder)`;
`(QuestionId, Outcome)` → estatística de acerto por pergunta.

### `GcSettings`

| Coluna | Tipo | Nulo | Notas |
| --- | --- | --- | --- |
| `Id` | `int` | não | PK, sempre `1` (check `Id = 1`) |
| `GcName` | `varchar(80)` | não | default `'GC Domus'` |
| `InviteCode` | `varchar(20)` | não | |
| `NormalizedInviteCode` | `varchar(20)` | não | `UPPER`, usado na comparação |
| `InviteRotatedAt` | `timestamptz` | não | |

### `AuditLogs`

| Coluna | Tipo | Nulo |
| --- | --- | --- |
| `Id` | `uuid` | não |
| `OccurredAt` | `timestamptz` | não |
| `ActorId` | `uuid` | sim |
| `ActorName` | `varchar(60)` | não |
| `Action` | `varchar(60)` | não |
| `Details` | `varchar(1000)` | sim |

Índice: `(OccurredAt DESC)`.

### Tabelas do ASP.NET Core Identity

`AspNetUsers`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`.
**Não** usamos `AspNetRoles`/`AspNetUserRoles`: o papel vive em `Participants.Role` e é
transformado em claim no login (menos duas tabelas e um join, decisão 25).

`AspNetUsers.Id` = `Participants.Id`, com FK de `Participants` → `AspNetUsers`
(a identidade é criada primeiro).

---

## 3. Consultas principais

**Rodada corrente do participante**

```sql
SELECT r.*, a."Id" AS attempt_id, a."Status", a."TotalPoints"
FROM   "Rounds" r
LEFT   JOIN "Attempts" a ON a."RoundId" = r."Id" AND a."ParticipantId" = @me
WHERE  r."SeasonId" = @season AND r."Status" = 1
ORDER  BY (r."OpensAt" <= now() AND r."ClosesAt" >= now()) DESC,  -- aberta primeiro
          r."OpensAt" DESC
LIMIT  1;
```

**Ranking semanal** (rodada encerrada — RN-30/RN-32)

```sql
SELECT p."DisplayName", a."TotalPoints", a."TotalTimeMs", a."CorrectCount",
       RANK() OVER (ORDER BY a."TotalPoints" DESC, a."TotalTimeMs" ASC) AS position
FROM   "Attempts" a
JOIN   "Participants" p ON p."Id" = a."ParticipantId"
WHERE  a."RoundId" = @round
ORDER  BY position;
```

**Ranking da temporada** (RN-31/RN-33)

```sql
SELECT p."Id", p."DisplayName", p."ShowInRanking",
       COALESCE(SUM(a."TotalPoints"), 0) AS points,
       COALESCE(SUM(a."TotalTimeMs"), 0) AS time_ms,
       COUNT(a."Id")                     AS rounds_played,
       RANK() OVER (ORDER BY COALESCE(SUM(a."TotalPoints"),0) DESC,
                             COALESCE(SUM(a."TotalTimeMs"),0) ASC) AS position
FROM   "Participants" p
LEFT   JOIN "Attempts" a ON a."ParticipantId" = p."Id"
LEFT   JOIN "Rounds"   r ON r."Id" = a."RoundId"
                        AND r."SeasonId" = @season
                        AND r."Status" = 1 AND r."ClosesAt" < now()
WHERE  p."IsRemoved" = false
GROUP  BY p."Id"
ORDER  BY position;
```

**Perguntas mais difíceis** (UC-28)

```sql
SELECT q."Order", q."Text",
       COUNT(*) FILTER (WHERE aa."Outcome" = 1)::float / NULLIF(COUNT(*), 0) AS accuracy
FROM   "Questions" q
JOIN   "AttemptAnswers" aa ON aa."QuestionId" = q."Id" AND aa."Outcome" <> 0
WHERE  q."RoundId" = @round
GROUP  BY q."Id", q."Order", q."Text"
ORDER  BY accuracy ASC NULLS LAST;
```

---

## 4. Migrations e seed

- O esquema é definido pelo modelo (índices parciais e check constraints inclusos, via
  `HasFilter` e `HasCheckConstraint`).
- No start, com `ApplyMigrationsOnStartup=true`: se houver migrations, aplica `Migrate()`;
  **enquanto não houver nenhuma gerada**, cria o esquema com `EnsureCreated()` e registra um aviso
  no log. Isso permite rodar o projeto imediatamente sem abrir mão de migrations em produção.
- A migration inicial é gerada com um comando (ver README) e passa a ser o caminho oficial.
- **Seed idempotente** no start:
  1. `GcSettings` (linha 1) com código de convite de `Gc__InviteCode` ou gerado aleatoriamente;
  2. usuário administrador de `Admin__Email` / `Admin__Password` / `Admin__DisplayName`;
  3. em ambiente de desenvolvimento com `Seed__Demo=true`: uma temporada, 3 rodadas
     (encerrada, aberta, agendada), 8 perguntas cada e 6 participantes fictícios com tentativas —
     o suficiente para ver rankings, gabarito e estatísticas funcionando.

**Convenção de nomes:** identificadores em `PascalCase` (padrão do EF Core, sempre entre aspas nas
consultas). Optamos por **não** adicionar o pacote de snake_case: uma dependência a menos, e o SQL
manual do projeto é pequeno.

---

## 5. Retenção, backup e volume

Volume estimado por temporada (13 semanas, 30 participantes, 8 perguntas):
`13 × 30 = 390` tentativas e `≈ 3.120` respostas. Em cinco anos, menos de 100 mil linhas.
**Nenhuma estratégia de particionamento ou arquivamento é necessária.**

Backup: dump diário do Postgres gerenciado (recurso da própria plataforma). O dado é reconstituível
manualmente em último caso, exceto tentativas — que são o único dado realmente insubstituível.
