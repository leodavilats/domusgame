# GC Domus — Desafios semanais

Plataforma de desafios semanais sobre as lições do Grupo de Crescimento Domus.
A cada semana o administrador publica uma rodada (lição + quiz); os participantes respondem
individualmente, ganham pontos por acerto e por rapidez, e disputam o ranking da temporada.

- **Backend:** ASP.NET Core 10 (Minimal API) · EF Core · PostgreSQL · ASP.NET Core Identity
- **Frontend:** React 19 + TypeScript + Vite + Tailwind 4 (PWA instalável, mobile-first)
- **Deploy:** um único container — a API serve a SPA compilada

A documentação do projeto está em [`docs/`](docs/): requisitos, casos de uso, modelo de domínio,
modelo de banco, arquitetura e backlog.

---

## Como rodar

### Opção 1 — Docker (mais simples)

```bash
docker compose up --build
```

Acesse **http://localhost:5080**.

Credenciais criadas pelo seed (definidas no `docker-compose.yml`):

| Item | Valor |
| --- | --- |
| Administrador | `admin@domus.local` / `Domus@2026` |
| Código de convite | `DOMUS2026` |
| Participantes de demonstração | `demo1@domus.local` … `demo6@domus.local` / `Demo@123` |

O seed de demonstração (`Seed__Demo=true`) cria uma temporada com três rodadas — uma encerrada
(com gabarito e ranking), uma aberta e uma agendada — para você ver o app funcionando de imediato.

### Opção 2 — Desenvolvimento local

Pré-requisitos: **.NET SDK 10**, **Node 22**, e um PostgreSQL (pode ser só o do compose).

```bash
# 1. banco
docker compose up db -d

# 2. API (porta 5080)
cd backend
dotnet run --project src/Domus.Api

# 3. front-end (porta 5173, com proxy de /api para 5080)
cd frontend
npm install
npm run dev
```

Abra **http://localhost:5173**.

---

## Testes

```bash
cd backend
dotnet test tests/Domus.Domain.Tests   # 72 testes de regras puras: pontuação, tempo, invariantes
dotnet test tests/Domus.Api.Tests      # 16 testes de integração; exige Docker (Testcontainers)
```

E no front-end:

```bash
cd frontend
npm test        # vitest + jsdom
```

Sem o SDK instalado, dá para rodar o back-end em container:

```powershell
docker run --rm -v "${PWD}:/src" -v domus-nuget:/root/.nuget/packages `
  -v "//var/run/docker.sock:/var/run/docker.sock" --add-host=host.docker.internal:host-gateway `
  -e TESTCONTAINERS_RYUK_DISABLED=true -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal `
  -w /src/backend mcr.microsoft.com/dotnet/sdk:10.0 bash -lc "dotnet test"
```

As duas variáveis `TESTCONTAINERS_*` só são necessárias nesse cenário (Docker dentro de Docker).
Em uma máquina com o SDK, ou no CI, `dotnet test` basta.

Os testes cobrem, principalmente, o que dói se quebrar:

- tabela de pontuação e tolerância de rede;
- tempo esgotado, ordem das perguntas e idempotência do envio;
- tentativa única sob requisições concorrentes;
- **vazamento de gabarito** em rodada aberta;
- validação de publicação, semana duplicada e janelas sobrepostas;
- unicidade da temporada ativa.

---

## Banco de dados e migrations

A migration inicial (`InitialCreate`) já está versionada em
`backend/src/Domus.Infrastructure/Migrations/` e é aplicada no start quando
`Database__ApplyMigrationsOnStartup=true`. Ela cria também o índice parcial da temporada ativa e as
*check constraints* de janela e pontuação.

Para novas mudanças de esquema:

```bash
cd backend
dotnet tool install --global dotnet-ef      # apenas na primeira vez
dotnet ef migrations add NomeDaMudanca \
  --project src/Domus.Infrastructure \
  --startup-project src/Domus.Infrastructure \
  --output-dir Migrations
```

O projeto tem uma `DesignTimeDbContextFactory`, então o `dotnet ef` não precisa subir a API — e a
connection string usada na geração é irrelevante (migrations vêm do modelo).

Se um dia o banco estiver vazio e não houver nenhuma migration, a aplicação cai para
`EnsureCreated` e registra um aviso — é uma rede de segurança, não o caminho normal.

---

## Configuração

Todas as chaves aceitam variáveis de ambiente no formato `Secao__Chave`.

| Chave | Padrão | Para que serve |
| --- | --- | --- |
| `ConnectionStrings__Postgres` | — | conexão com o banco, formato ADO.NET |
| `DATABASE_URL` | — | alternativa: URI `postgresql://usuario:senha@host:5432/banco` |
| `Database__ApplyMigrationsOnStartup` | `true` em Dev | aplica o esquema no start |
| `Gc__Name` | `GC Domus` | nome exibido no app |
| `Gc__InviteCode` | gerado | código de convite inicial |
| `Admin__Email` / `Admin__Password` / `Admin__DisplayName` | — | administrador de bootstrap (ver abaixo) |
| `Seed__Demo` | `false` | cria dados de demonstração |
| `App__PublicUrl` | `http://localhost:5080` | usado em links compartilhados |

### Administrador de bootstrap

`Admin__Password` é a **fonte da verdade** para essa conta. A cada start:

- se o e-mail ainda não existe, a conta é criada;
- se já existe e a senha configurada é diferente da armazenada, **a senha é sincronizada** e
  qualquer bloqueio por tentativas é limpo (registra um aviso no log);
- se o papel não for administrador, ele é restaurado.

Isso existe porque a alternativa — ignorar a variável depois do primeiro deploy — tranca o
administrador do lado de fora sem nenhuma pista do motivo. Para trocar a senha, altere a variável
e faça o redeploy.

Uma falha aqui **não derruba a aplicação**: é registrada no log e o serviço continua no ar.

### Conexão com o banco

**Não existe valor padrão em produção.** Sem `ConnectionStrings__Postgres` nem `DATABASE_URL`, a
aplicação falha no start com uma mensagem explícita — de propósito: um padrão `localhost` faria o
container tentar conectar a si mesmo e produzir um erro confuso de "Connection refused".

Formatos aceitos, nesta ordem de prioridade:

```
ConnectionStrings__Postgres = Host=ep-xxx.neon.tech;Port=5432;Database=domus;Username=u;Password=p;SSL Mode=Require
DATABASE_URL                = postgresql://usuario:senha@host:5432/banco?sslmode=require
```

`DATABASE_URL` é o formato que Railway, Render, Fly, Heroku e Neon expõem por padrão. Quando o
`sslmode` não vem na URI, usamos `Prefer`: criptografa se o servidor oferecer TLS e continua
funcionando em rede privada sem TLS (caso do Postgres interno de algumas plataformas).

No start, a aplicação registra no log o destino da conexão (`host:porta/banco`, nunca a senha) e
espera até ~40 s pelo banco antes de desistir — bancos gerenciados costumam estar acordando quando
o container sobe.

---

## Estrutura

```
backend/
  src/Domus.Domain/          regras de negócio puras (sem EF, sem ASP.NET)
  src/Domus.Infrastructure/  EF Core, Identity, seed
  src/Domus.Api/             Minimal API organizada por feature + wwwroot (SPA)
  tests/                     domínio e integração
frontend/                    React + Vite + Tailwind
docs/                        requisitos → casos de uso → domínio → banco → arquitetura → backlog
```

---

## Decisões que valem conhecer antes de mexer

1. **O tempo é do servidor.** O cronômetro do navegador é enfeite; `ServedAt` e `AnsweredAt` vêm
   do relógio da API. Nunca aceite tempo enviado pelo cliente.
2. **O gabarito não trafega em rodada aberta.** Os DTOs usados durante a tentativa simplesmente
   não têm campo de resposta correta — e há teste garantindo isso.
3. **Tentativa única é garantida no banco** (índice único em `RoundId + ParticipantId`), não por
   um `if` na aplicação.
4. **Rodada publicada não pode ser editada** e não há recálculo de gabarito na v1. Por isso a
   pré-visualização e a validação antes de publicar são importantes.
5. **Abertura e encerramento não usam agendador**: são derivados da comparação com o relógio.
6. **A pontuação é calculada e persistida no momento do envio**, com os parâmetros copiados para
   dentro da tentativa. Mudar a rodada depois não reescreve o histórico.
