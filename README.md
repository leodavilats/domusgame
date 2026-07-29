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
| Código da sala | `DOMUS2026` |
| Participantes de demonstração | `demo1@domus.local` … `demo6@domus.local` / `Demo@123` |

O cadastro é aberto: cria-se a conta com e-mail e senha (ou com o Google) e, com o **código da
sala**, entra-se na sala do GC — é ali que estão temporadas, rodadas, ranking e pessoas. Quem se
cadastra e não entra em nenhuma sala vê o painel vazio com o convite para entrar.

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
dotnet test tests/Domus.Domain.Tests   # 76 testes de regras puras: pontuação, tempo, invariantes
dotnet test tests/Domus.Api.Tests      # 32 testes de integração; exige Docker (Testcontainers)
```

E no front-end:

```bash
cd frontend
npm test        # 4 testes: vitest + jsdom
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
| `Gc__Name` | `GC Domus` | nome da **primeira sala**, exibido no cabeçalho |
| `Gc__InviteCode` | gerado | código da primeira sala; se vazio, é sorteado e aparece no log |
| `Admin__Email` / `Admin__Password` / `Admin__DisplayName` | — | administrador de bootstrap (ver abaixo) |
| `Authentication__Google__ClientId` / `__ClientSecret` | — | habilita o login com o Google (ver abaixo) |
| `Seed__Demo` | `false` | cria dados de demonstração |
| `DevTools__Enabled` | `false` | libera as ferramentas de teste do painel admin (ver abaixo) |
| `App__PublicUrl` | `http://localhost:5080` | usado em links compartilhados |

> `Gc__Name` e `Gc__InviteCode` só valem para **criar** a primeira sala. Depois disso, o código é
> gerenciado no painel (**Pessoas → Código da sala**) e a variável deixa de ter efeito sobre ele.

### Login com o Google

1. No [Google Cloud Console](https://console.cloud.google.com/apis/credentials), crie uma credencial
   **OAuth client ID** do tipo *Web application*.
2. Em *Authorized redirect URIs*, informe `https://SEU-DOMINIO/signin-google` (e
   `http://localhost:5080/signin-google` para desenvolvimento).
3. Configure `Authentication__Google__ClientId` e `Authentication__Google__ClientSecret`.

Sem essas duas variáveis o esquema **não é registrado**: o botão continua na tela, mas avisa que o
login com o Google não está disponível naquele ambiente em vez de estourar um erro. Se o e-mail da
conta do Google já existir no sistema, o login social é **vinculado** à conta existente — não nasce
uma segunda conta com o mesmo e-mail.

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

### Senha dos participantes

Regra única: **8 caracteres**. Sem exigência de maiúscula, dígito ou símbolo — a interface promete
isso e nada mais, e exigir em silêncio produz erro que o usuário não entende.

Não há recuperação por e-mail (não há serviço de e-mail na v1). Quem perde a senha entra com o
**Google** usando o mesmo e-mail: o login social é vinculado à conta que já existe, com o histórico
intacto. O administrador **não** gera senha para ninguém — senha de terceiro circulando no grupo é
pior do que a inconveniência que resolveria.

### Ferramentas de teste do painel admin

A aba **Ferramentas** do painel reúne atalhos para exercitar o fluxo sem esperar o relógio:

| Ferramenta | O que faz |
| --- | --- |
| Diagnóstico | ambiente, hora do servidor, hora do aparelho, migration aplicada e contagem de registros |
| Temporada de teste | uma temporada com três rodadas de **um dia** (encerrada, aberta, agendada), 5 perguntas fáceis cada, cobrindo sem mídia, com imagem, com áudio, 2 e 5 alternativas |
| Abrir agora / Encerrar agora | desloca a janela da rodada, para testar quiz e gabarito na hora |
| Refazer minha tentativa | apaga **só a sua** tentativa, já que ela é única por participante |
| Sair da sala | remove **só a sua** filiação, para ver o app como quem acabou de se cadastrar. A mensagem devolve o código para você voltar |
| Simular participações | cria participantes fictícios respondendo a rodada, com desempenhos variados |
| Limpar dados | três escopos: só participações, + rodadas e temporadas, ou tudo |
| Auditoria | últimas 30 ações administrativas registradas |

**Ficam desligadas por padrão.** Sem `DevTools__Enabled=true`, as ações respondem 403 — só o
diagnóstico e a auditoria continuam acessíveis, porque são leitura e explicam o estado do ambiente.
As ações destrutivas ainda exigem digitar `LIMPAR` no corpo da requisição, e administradores, a sala
e o seu código de convite nunca são apagados.

> **Atenção:** *Limpar dados* age no **banco todo**, não só na sala de quem chamou — é uma ferramenta
> de desenvolvimento, não uma operação de administração de sala. Quando existir uma segunda sala, ela
> precisa ser escopada antes de ser usada com dados reais.

O `docker-compose.yml` liga as ferramentas para desenvolvimento local. **Deixe desligado em
produção** — ative só quando for testar e desative em seguida.

A mídia usada pela temporada de teste (`/exemplo-imagem.svg` e `/exemplo-audio.wav`) é servida pelo
próprio app, para não depender de link externo que pode sair do ar.

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
7. **Conteúdo pertence a uma sala.** Conta e pertencimento são coisas separadas: o cadastro é aberto
   e a filiação vem do código da sala. Toda leitura e escrita é filtrada pela sala de quem pede, e
   id de outra sala responde 404 — se você adicionar um endpoint que recebe id de rodada ou
   temporada, ele precisa passar por `RequireRoundInMyRoomAsync` (ou equivalente).
