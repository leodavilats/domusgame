# Contexto do projeto — GC Domus

Plataforma de desafios semanais das lições do GC Domus. Público de 10 a 30 pessoas, um único GC,
um desenvolvedor. **Simplicidade é requisito**, não preguiça: ver `docs/05-arquitetura.md` §1.

## Onde as coisas estão

| Preciso mexer em... | Vá para |
| --- | --- |
| Regra de negócio (pontuação, tempo, invariante) | `backend/src/Domus.Domain/` |
| Esquema, Identity, seed | `backend/src/Domus.Infrastructure/` |
| Endpoint / DTO | `backend/src/Domus.Api/Features/<Assunto>/` |
| Leitura compartilhada (ranking, rodada corrente) | `backend/src/Domus.Api/Common/DomusQueries.cs` |
| Tela | `frontend/src/pages/` (admin em `pages/admin/`) |
| Contrato do front | `frontend/src/api/types.ts` (espelha `Common/Contracts.cs`) |

## Convenções

- **Código e domínio em inglês; interface em pt-BR.** Comentários em português, sem acentos nos
  arquivos `.cs` (evita ruído de encoding em diffs).
- **Vertical slice**: endpoint, handler e DTOs no mesmo arquivo da feature. Sem MediatR.
- **Sem repositórios genéricos**: `DomusDbContext` direto nos handlers.
- `TreatWarningsAsErrors=true`. Nullable habilitado.
- Domínio **não** referencia EF Core, ASP.NET nem Identity — há teste de arquitetura garantindo.
- Testes com xUnit puro (`Assert`), sem biblioteca de asserção. Front-end com vitest + jsdom.
- **Nunca use `useCallback` com dependências vazias sobre um closure que lê estado de formulário.**
  Já quebrou todos os formulários do app uma vez: `run` congelou a primeira renderização e cada
  envio mandava os campos vazios. Se precisar de identidade estável, guarde a função numa ref
  (ver `useMutation` em `frontend/src/api/hooks.ts`) e não silencie `react-hooks/exhaustive-deps`.

## Invariantes que não se negociam

1. Nenhum DTO de rodada aberta pode conter a alternativa correta (`RNF-02`).
2. Tempo sempre do servidor (`RNF-03`).
3. Tentativa única garantida por índice no banco (`RNF-04`).
4. Envio de resposta idempotente por `(AttemptId, QuestionId)` (`RNF-05`).
5. Pontuação calculada e persistida no envio, com parâmetros congelados na tentativa (`RN-28`).
6. Rodada é imutável **a partir da abertura** (`RN-10`). Antes disso — rascunho ou publicada
   ainda agendada — o admin pode editar e excluir; a exclusão exige zero participações.

Ao mexer em qualquer uma delas, o teste correspondente em `tests/` deve ser atualizado junto —
e provavelmente a regra em `docs/01-requisitos.md` também.

## Fluxo de trabalho combinado

Processo em 7 etapas (requisitos → casos de uso → domínio → banco → arquitetura → backlog →
implementação), documentado em `docs/`. Mudança de escopo entra primeiro no documento, depois no
código.
