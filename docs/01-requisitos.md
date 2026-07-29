# GC Domus — Documento de Requisitos (v1)

> Etapa 1 de 7 do processo. Status: **aguardando aprovação**.
> Última atualização: 2026-07-26

---

## 1. Visão do produto

Plataforma web de **desafios semanais** sobre as lições estudadas no Grupo de Crescimento (GC) Domus.

A cada semana o administrador publica uma **rodada** contendo a lição da semana e um quiz de
múltipla escolha. Os participantes respondem individualmente, em casa, durante a janela da semana.
Acertos e velocidade geram pontos. As pontuações alimentam um **ranking semanal** e um **ranking da
temporada**, e ao fim da temporada os **3 primeiros** são premiados.

**Objetivo de negócio:** aumentar o engajamento no estudo da lição, tornando-o divertido e
mensurável.

**Métrica de sucesso:** % de participantes ativos que respondem à rodada da semana
(meta inicial: > 70%).

---

## 2. Escopo e escala

| Item | Definição |
| --- | --- |
| Participantes previstos | 10 a 30 |
| GCs atendidos | 1 (GC Domus) |
| Administradores | 1 (o próprio dono do sistema) |
| Fuso de referência | `America/Sao_Paulo` (persistência sempre em UTC) |
| Idioma da interface | pt-BR |
| Plataforma alvo | Web responsivo, **mobile-first** (celular é o dispositivo principal) |

### Fora do escopo da v1

- Múltiplos GCs / multi-tenant
- Sorteio de brindes (a premiação é por mérito: top 3)
- Recálculo de pontuação por correção de gabarito
- Modo treino / responder rodadas antigas
- Peso ou dificuldade por pergunta
- Penalidade por resposta errada
- Ranking por faixa etária
- Notificações automáticas (e-mail, push, WhatsApp)
- Medalhas, conquistas, níveis e títulos
- Ranking por GC e disputas entre GCs
- Tempo real (ranking ao vivo)
- Upload de arquivos pelo navegador (mídia é referenciada por URL)

> Itens fora de escopo estão registrados no backlog de evolução (seção 9), não descartados.

---

## 3. Papéis

| Papel | Descrição |
| --- | --- |
| **Visitante** | Não autenticado. Só acessa login e cadastro. |
| **Cadastrado sem sala** | Autenticado, mas fora de qualquer sala. Só vê o convite para entrar em uma sala e o próprio perfil. |
| **Participante** | Membro de uma sala. Responde rodadas, vê sua pontuação, histórico e rankings **da sua sala**. |
| **Administrador** | Tudo do participante, mais: gerencia temporadas, rodadas, lições, perguntas e vê estatísticas **da sua sala**. |

Decisão: o administrador **também pode participar**, e sua pontuação é sinalizada como tal (ele
conhece o gabarito). Ver RN-22.

---

## 4. Conceitos do domínio (glossário)

| Termo | Significado |
| --- | --- |
| **Temporada** (`Season`) | Período de competição (ex.: um trimestre). Agrupa rodadas e define o ranking premiado. |
| **Rodada** (`Round`) | Desafio de uma semana: uma lição + um conjunto de perguntas + uma janela de disponibilidade. |
| **Lição** (`Lesson`) | Conteúdo estudado na semana (título, referência bíblica, texto, link). Pertence a uma rodada. |
| **Pergunta** (`Question`) | Enunciado de múltipla escolha, opcionalmente com imagem ou áudio. |
| **Alternativa** (`AnswerOption`) | Opção de resposta de uma pergunta. Exatamente uma é a correta. |
| **Tentativa** (`Attempt`) | A participação de um participante em uma rodada. Única e irrepetível. |
| **Resposta** (`AttemptAnswer`) | A escolha do participante para uma pergunta, com o tempo gasto e os pontos obtidos. |
| **Janela** | Intervalo `[OpensAt, ClosesAt]` em que a rodada aceita respostas. |
| **Gabarito** | Conjunto de alternativas corretas + explicações. Só é revelado após o fechamento da rodada. |

---

## 5. Regras de negócio

### 5.1 Temporada

- **RN-01** — Uma temporada tem nome, data de início e data de fim.
- **RN-02** — Existe no máximo **uma temporada ativa** por vez. Toda rodada pertence a uma temporada.
- **RN-03** — O ranking premiado é o da temporada. O ranking histórico (todas as temporadas) é
  apenas informativo e nunca é zerado.
- **RN-04** — Ao encerrar uma temporada, os **3 primeiros colocados** são registrados como
  premiados (congelados no histórico, imunes a mudanças futuras).

### 5.2 Rodada e janela de disponibilidade

- **RN-05** — Uma rodada tem: temporada, número da semana, título, lição, perguntas, `OpensAt`,
  `ClosesAt` e parâmetros de pontuação.
- **RN-06** — Padrão sugerido de janela: **abre domingo às 13h00**, **fecha sábado às 23h59**
  (horário de Brasília). Os valores são editáveis por rodada.
- **RN-07** — A rodada tem apenas dois estados persistidos: `Draft` (rascunho) e `Published`
  (publicada). A disponibilidade é **derivada do relógio**:
  - `now < OpensAt` → **Agendada** (visível na agenda, não responde)
  - `OpensAt ≤ now ≤ ClosesAt` → **Aberta**
  - `now > ClosesAt` → **Encerrada**

  > Decisão de arquitetura: liberação e encerramento automáticos **não exigem agendador**
  > (cron/worker). Isso elimina uma peça de infraestrutura e uma classe inteira de bugs.
- **RN-08** — Uma rodada só pode ser publicada se: tiver lição preenchida, tiver **pelo menos uma
  pergunta**, cada pergunta tiver **2 a 5 alternativas** com **exatamente uma correta**, e
  `OpensAt < ClosesAt`.
- **RN-09** — Rodada em `Draft` é invisível para participantes.
- **RN-10** — A rodada é editável enquanto **não abriu**: sempre em `Draft` e também em
  `Published` enquanto `now < OpensAt` (estado *Agendada*). **A partir da abertura ela é
  imutável** — há respostas e pontuação em jogo, e mudar enunciado, gabarito ou parâmetros no
  meio do caminho tornaria as tentativas incomparáveis entre si.
  (Consequência aceita: um erro de gabarito percebido **depois** da abertura permanece; não há
  recálculo na v1.)
- **RN-11** — Não pode haver duas rodadas com o mesmo número de semana na mesma temporada.
- **RN-12** — Rodadas publicadas de uma mesma temporada **não podem ter janelas sobrepostas**
  (garante "uma rodada aberta por vez").

### 5.3 Participação

- **RN-13** — Só é possível responder a rodada **aberta**. Rodadas encerradas ficam disponíveis
  apenas para leitura (lição + gabarito + a própria pontuação).
- **RN-14** — **Tentativa única**: um participante pode iniciar no máximo uma tentativa por rodada.
  Garantido por restrição de unicidade no banco, não por validação de aplicação.
- **RN-15** — As perguntas são apresentadas **uma por vez**, na ordem definida pelo administrador,
  **sem possibilidade de voltar** ou alterar uma resposta já enviada.
- **RN-16** — A **ordem das alternativas é embaralhada por tentativa** (semente derivada da
  tentativa, para ser estável se a página recarregar).
- **RN-17** — Cada pergunta tem um **tempo limite** (padrão: **45 segundos**, configurável por
  rodada). O cronômetro começa quando o servidor entrega a pergunta e é medido **exclusivamente no
  servidor**. Há uma tolerância de rede de **3 segundos**.
- **RN-18** — Resposta enviada após o tempo limite + tolerância é registrada como **tempo
  esgotado**: vale 0 ponto. Se o participante nada enviar, o cliente envia resposta em branco ao
  zerar o cronômetro; se nem isso acontecer, a pergunta é marcada como esgotada na próxima
  interação.
- **RN-19** — Uma tentativa interrompida (fechou o navegador, caiu a conexão) pode ser **retomada
  enquanto a rodada estiver aberta**, seguindo da primeira pergunta ainda não respondida. A
  pergunta que estava na tela no momento da interrupção provavelmente expirará — o participante é
  avisado disso **antes de iniciar** a tentativa.
- **RN-20** — Uma tentativa é considerada **concluída** quando todas as perguntas foram respondidas
  ou esgotadas, ou quando a rodada fecha. Tentativas não concluídas pontuam apenas o que foi
  efetivamente respondido.
- **RN-21** — O **gabarito, as explicações e as respostas dos outros participantes** só são
  visíveis após `ClosesAt`. Nenhum endpoint retorna `IsCorrect` de alternativas antes disso.
- **RN-22** — **Todo membro da sala aparece no ranking.** Não existe opt-out: o ranking de um grupo
  de 30 pessoas que já conversam entre si não fica mais confortável com ausências, fica confuso —
  quem sumiu da lista continua pontuando e a soma deixa de fechar para quem olha.
  *(Regra revisada: a versão anterior tinha `ShowInRanking` por participante, editável no perfil.)*

### 5.4 Pontuação

- **RN-23** — Resposta **correta** vale `PointsPerCorrectAnswer` (padrão: **10 pontos**).
- **RN-24** — Resposta **errada, em branco ou com tempo esgotado** vale **0**. Não há pontuação
  negativa.
- **RN-25** — Toda pergunta vale o mesmo. Não há peso nem dificuldade.
- **RN-26** — **Bônus de velocidade**: aplicado somente a respostas corretas, proporcional ao tempo
  restante:

  ```
  bonus = round( MaxSpeedBonus × (1 − elapsed / timeLimit) )     // limitado a [0, MaxSpeedBonus]
  ```

  Padrão: `MaxSpeedBonus = 5`, `timeLimit = 45s`. Exemplos com os padrões:

  | Tempo gasto | Base | Bônus | Total |
  | --- | --- | --- | --- |
  | 5 s | 10 | 4 | 14 |
  | 15 s | 10 | 3 | 13 |
  | 30 s | 10 | 2 | 12 |
  | 45 s | 10 | 0 | 10 |
  | errada / esgotada | 0 | 0 | 0 |

- **RN-27** — Pontuação máxima da rodada = `nº de perguntas × (PointsPerCorrectAnswer + MaxSpeedBonus)`.
- **RN-28** — A pontuação de uma resposta é calculada **no servidor, no momento do envio**, e
  persistida (base, bônus, tempo gasto). Pontuação nunca é recalculada em leitura — o histórico é
  imutável mesmo que os parâmetros da rodada mudem no futuro.
- **RN-29** — O **tempo total** da tentativa é a soma dos tempos gastos por pergunta (respostas
  esgotadas contam o tempo limite cheio).

### 5.5 Ranking

- **RN-30** — **Ranking semanal**: participantes de uma rodada, ordenados por
  (1) maior pontuação, (2) menor tempo total. Empate absoluto compartilha a posição.
- **RN-31** — **Ranking da temporada**: soma das pontuações das rodadas da temporada, ordenado por
  (1) maior pontuação total, (2) menor tempo total acumulado.
- **RN-32** — O ranking semanal só é publicado **após o encerramento da rodada**. Durante a semana
  o participante vê apenas a própria pontuação. (Evita entregar de bandeja a informação de que "o
  gabarito rende 150 pontos" e reduz constrangimento.)
- **RN-33** — Quem não participou de uma rodada soma 0 naquela rodada — não é eliminado da
  temporada.

### 5.6 Cadastro e acesso

- **RN-34** — O cadastro é **aberto**: qualquer pessoa cria conta com e-mail e senha, ou com o
  Google. O código de convite não é mais um pré-requisito do cadastro (ver RN-41).
- **RN-35** — O administrador pode gerar um novo código da sala, invalidando o anterior. Quem já
  entrou **continua dentro**: o código controla a entrada, não a permanência.
- **RN-36** — O participante tem um **nome de exibição** obrigatório (é o que aparece no ranking).
  A **foto vem da conta do Google** e não é editável no app: sem upload e sem campo de URL, quem
  quiser trocar troca no Google. Quem entra com e-mail e senha fica com as iniciais.
- **RN-37** — Não coletamos data de nascimento, telefone nem endereço. O único dado pessoal
  obrigatório é nome de exibição + e-mail (identidade da conta).
- **RN-38** — O participante pode excluir sua conta. Suas tentativas são anonimizadas (o histórico
  agregado da rodada permanece; o nome é substituído por "Participante removido").
- **RN-39** — Não há recuperação de senha por e-mail (sem SMTP na v1). Quem perde a senha e havia
  entrado com o Google usa o Google; nos outros casos o acesso é recriado com outro e-mail. O
  administrador **não** gera senha para ninguém: senha de terceiro trafegando pelo grupo é pior do
  que a inconveniência que resolve.
- **RN-40** — Senha exige apenas **8 caracteres**. Nenhuma outra regra: exigir maiúscula ou
  dígito sem avisar produz erro que o participante não entende, e o público é um grupo de 30
  pessoas, não um alvo de ataque em massa.

### 5.7 Salas

- **RN-41** — Conteúdo (temporadas, rodadas, lições, ranking, pessoas) pertence a uma **sala**.
  Quem está cadastrado mas fora de qualquer sala vê a plataforma vazia e apenas o convite para
  entrar em uma.
- **RN-42** — Entrar na sala exige o **código de convite** dela, comparado sem diferenciar
  maiúsculas de minúsculas. Código errado não revela se a sala existe.
- **RN-43** — Entrar duas vezes na mesma sala não duplica a filiação (idempotente por
  `(RoomId, ParticipantId)`, garantido por índice único).
- **RN-44** — Na v1 o participante pertence a **uma** sala. O modelo (`RoomMemberships`) já suporta
  várias; a interface e as consultas assumem a primeira filiação por data de entrada.
- **RN-45** — Toda leitura e escrita de conteúdo é filtrada pela sala de quem pede. Rodada ou
  temporada de outra sala responde **404**, não 403 — quem não é da sala não deve nem saber que ela
  existe.
- **RN-46** — Uma temporada ativa **por sala** (não uma no sistema todo). O índice único que
  garantia isso passou a ser `(RoomId, Status)`.

---

## 6. Requisitos funcionais

### 6.1 Participante

| ID | Requisito |
| --- | --- |
| RF-01 | Cadastrar-se com e-mail + senha ou com a conta do Google, sem código de convite (RN-34). |
| RF-16 | Entrar em uma sala com o código de convite e ver a partir daí o conteúdo do GC (RN-41, RN-42). |
| RF-02 | Autenticar-se e manter sessão em sessões longas no celular. |
| RF-03 | Editar o perfil: **nome de exibição**. A foto vem do Google (RN-36) e o ranking não tem opt-out (RN-22). |
| RF-04 | Ver a home com: rodada da semana (estado e contagem regressiva), sua pontuação na temporada, sua posição no ranking e seu streak de participação. |
| RF-05 | Ler a lição da semana (título, referência bíblica, texto e link externo). |
| RF-06 | Iniciar a tentativa da rodada aberta, após tela de aviso das regras (tentativa única, tempo por pergunta, sem voltar). |
| RF-07 | Responder as perguntas uma a uma, com cronômetro visível e mídia (imagem/áudio) quando houver. |
| RF-08 | Retomar uma tentativa interrompida enquanto a rodada estiver aberta. |
| RF-09 | Ver o resultado da tentativa ao final: pontuação total, acertos, tempo — **sem gabarito** se a rodada ainda estiver aberta. |
| RF-10 | Após o encerramento: revisar pergunta a pergunta com gabarito, explicação e o que respondeu. |
| RF-11 | Ver o ranking da rodada encerrada e o ranking da temporada. |
| RF-12 | Ver seu histórico de rodadas (pontuação, acertos, posição). |
| RF-13 | Compartilhar a pontuação (texto + link) usando o compartilhamento nativo do celular. |
| RF-14 | Instalar a aplicação na tela inicial do celular (PWA). |
| RF-15 | Excluir a própria conta. |

### 6.2 Administrador

| ID | Requisito |
| --- | --- |
| RF-20 | Criar, editar e encerrar temporadas; definir a temporada ativa. |
| RF-21 | Criar rodada em rascunho: semana, título, janela, parâmetros de pontuação. |
| RF-22 | Cadastrar a lição da rodada (com editor de texto simples / markdown). |
| RF-23 | Cadastrar, editar, reordenar e remover perguntas do rascunho. |
| RF-24 | Cadastrar alternativas (2 a 5), marcar a correta e escrever a explicação. |
| RF-25 | Anexar imagem ou áudio a uma pergunta via URL. |
| RF-26 | Pré-visualizar a rodada como o participante a verá. |
| RF-27 | Publicar a rodada (com validação de RN-08, RN-11, RN-12) e ver o resultado do agendamento. |
| RF-28 | Duplicar uma rodada anterior como base para a próxima. |
| RF-29 | Gerenciar o código de convite da sala (ver e rotacionar). |
| RF-30 | Listar os participantes **da sala** e promover/rebaixar administradores. |
| RF-31 | Dashboard de estatísticas: taxa de participação por rodada, média e distribuição de pontos, perguntas com menor índice de acerto, tempo médio por pergunta, quem ainda não respondeu a rodada aberta, evolução da participação por semana. |
| RF-32 | Exportar o ranking da temporada em CSV (para a premiação). |

---

## 7. Requisitos não funcionais

| ID | Requisito |
| --- | --- |
| RNF-01 | **Mobile-first e responsivo.** Layout validado a partir de 360 px de largura. |
| RNF-02 | **Segurança do gabarito:** nenhuma resposta correta trafega para o cliente antes do encerramento da rodada. Verificado por teste automatizado. |
| RNF-03 | **Tempo autoritativo no servidor.** O relógio do cliente é apenas exibição. |
| RNF-04 | **Tentativa única garantida no banco** (índice único), resistente a duplo clique e requisições concorrentes. |
| RNF-05 | **Idempotência** no envio de resposta: reenviar a mesma resposta da mesma pergunta não duplica nem repontua. |
| RNF-06 | **Autorização** verificada no servidor em toda rota administrativa. |
| RNF-07 | **Rate limiting** em login, cadastro e envio de respostas. |
| RNF-08 | **Auditoria** das ações administrativas relevantes (publicar rodada, encerrar temporada, alterar papéis, trocar convite). |
| RNF-09 | **Persistência em UTC**, apresentação em `America/Sao_Paulo`, incluindo horário de verão. |
| RNF-10 | **Deploy simples:** um único container de aplicação + um Postgres gerenciado. |
| RNF-11 | **Testabilidade:** regras de pontuação, janela de tempo e tentativa única cobertas por testes automatizados. Domínio testável sem banco. |
| RNF-12 | **Desempenho:** com 30 participantes, qualquer tela deve responder em < 300 ms no servidor. Ranking calculado por consulta SQL, sem materialização. |
| RNF-13 | **Acessibilidade básica:** contraste adequado, navegação por teclado nas telas de quiz, alternativas clicáveis com área de toque ≥ 44 px. |
| RNF-14 | **LGPD:** política de privacidade simples, dado pessoal mínimo, exclusão de conta funcional. |
| RNF-15 | **Custo de operação alvo:** até US$ 10/mês (viável em camadas gratuitas). |

---

## 8. Decisões tomadas (perguntas 24 a 38)

| # | Tema | Decisão | Motivo |
| --- | --- | --- | --- |
| 24 | Cadastro | Cadastro aberto; o código de convite passou a ser a porta da **sala**, não do cadastro | Conta e pertencimento são coisas diferentes: separá-las abre caminho para vários GCs sem mudar o login |
| 25 | Login | **E-mail/senha** (ASP.NET Core Identity) **+ Google** | Sem infraestrutura de e-mail (magic link exigiria SMTP). O Google entrou depois, a pedido do dono do produto: elimina a senha esquecida, que era o suporte manual mais frequente |
| 26–27 | Faixa etária / menores | Não coletar data de nascimento; sem ranking por idade | Menos dado pessoal, menos risco LGPD, menos tela |
| 28 | Identidade pública | Nome de exibição obrigatório; foto vinda do Google; **sem** opt-out do ranking | Uma decisão a menos para o participante e um ranking que fecha a conta. Revisto depois da v1: o opt-out existia e foi retirado |
| 29 | Notificações | Nenhuma automática na v1; divulgação pelo grupo de WhatsApp + contagem regressiva na home | Push/e-mail/WhatsApp custam infraestrutura para 30 pessoas que já têm grupo |
| 30 | Compartilhamento | Web Share API com texto + link (card em imagem fica no backlog) | 90% do valor com 5% do esforço |
| 31 | PWA | Instalável, exige conexão para responder | Integridade do cronômetro |
| 32 | Ranking ao vivo | Não. Atualiza ao carregar a página | Nenhum ganho real nessa escala |
| 33 | Arquitetura | **.NET 9 API + React (Vite) + Tailwind, servidos pelo mesmo processo** | Um deploy, um container. Next.js adicionaria um segundo runtime sem ganho: o app é 100% autenticado, sem SEO nem SSR |
| 33b | Estilo de código | Domínio rico + *vertical slices*, sem CQRS/MediatR/event sourcing | Projeto de um dev; a cerimônia de Clean Architecture completa custaria mais do que entrega |
| 34 | Autenticação | Cookie `httpOnly`, `SameSite=Lax` (mesma origem) | Sem token no `localStorage`, sem refresh token para manter |
| 35 | Banco | Postgres gerenciado (camada gratuita) + EF Core com migrations | Zero manutenção de banco |
| 36 | Repositório | Monorepo Git (`/backend`, `/frontend`, `/docs`) + GitHub Actions (build + testes) | Rede de segurança desde o commit 1 |
| 37 | Testes | xUnit no domínio; integração nos pontos críticos (pontuação, janela, tentativa única) | Cobertura onde o erro dói |
| 38 | Idioma | Interface pt-BR; código, domínio e commits em inglês | Padrão do ecossistema, sem "Portunhol" no código |

### Simplificações deliberadas (e o que elas custam)

1. **Sem entidade `Gc`.** Nome do GC e código de convite ficam em uma configuração de linha única.
   *Custo futuro:* suportar vários GCs exigirá uma migração com nova tabela e chaves estrangeiras
   em participantes e rodadas. Aceitável com 30 usuários — eu recomendaria o contrário se fossem
   vários GCs desde já.
2. **Sem agendador.** Abertura e fechamento são derivados do relógio (RN-07).
   *Custo futuro:* quando entrarem notificações e medalhas, será necessário um job em background.
3. **Sem tabela de medalhas.** Streak é calculado por consulta.
   *Custo futuro:* nenhum relevante.
4. **Sem upload de mídia.** Imagens/áudios entram por URL.
   *Custo futuro:* o administrador depende de um serviço externo para hospedar mídia.

---

## 9. Backlog de evolução (pós-v1, priorizado)

| Prioridade | Item | Valor esperado |
| --- | --- | --- |
| Alta | Card de pontuação em imagem para compartilhar | Divulgação orgânica |
| Alta | Medalhas simples (primeira participação, 100% de acerto, 4 semanas seguidas, top 3 da semana) | Reconhecimento além do pódio |
| Alta | Streak com 1 "escudo" por temporada (perdoa uma falta) | Streak sem perdão faz desistir de vez |
| Média | Geração assistida de perguntas por IA a partir do texto da lição, com revisão obrigatória | Reduz drasticamente o trabalho semanal do administrador |
| Média | Notificação push (PWA) na abertura da rodada | Lembrete sem depender do grupo |
| Média | Modo treino (responder rodadas antigas sem pontuar) | Recupera quem faltou |
| Média | Retrospectiva da temporada ("sua temporada em números") | Pico de compartilhamento |
| Média | Correção de gabarito com recálculo auditável | Necessário se um erro publicado gerar conflito |
| Baixa | Bilhetes de sorteio (cada N pontos = 1 bilhete) | Inclui quem não briga pelo pódio |
| Baixa | Aposta de confiança (uma pergunta "dobro ou nada") | Decisão tática, mais diversão |
| Baixa | Perguntas relâmpago surpresa | Novidade no meio da semana |
| Baixa | Múltiplos GCs, ranking por GC e disputas entre GCs | Só quando houver um segundo GC |
| Baixa | Outros tipos de pergunta (V/F, múltipla resposta, completar versículo, ordenar) | Variedade |
| Baixa | Upload de mídia | Conveniência do administrador |

---

## 10. Riscos

| Risco | Impacto | Mitigação na v1 |
| --- | --- | --- |
| Combinação de respostas no grupo de WhatsApp | Ranking perde credibilidade | Gabarito só após o fechamento, alternativas embaralhadas por tentativa, tempo curto por pergunta, ranking semanal oculto durante a semana |
| Erro de gabarito publicado | Injustiça sem correção possível (RN-10) | Pré-visualização obrigatória antes de publicar + validação de publicação |
| Perda de conexão no meio do quiz | Participante perde pontos | Resposta persistida pergunta a pergunta; tentativa retomável (RN-19) |
| Participante esquece de responder | Queda na participação | Contagem regressiva na home; painel do admin lista quem falta; lembrete manual no grupo |
| Administrador competindo com acesso ao gabarito | Desconfiança | Sinalização do papel na lista de pessoas e nas estatísticas (RN-22) |
| Baixa adesão inicial | Projeto morre | v1 enxuta: se o ciclo semanal funcionar, gamificação avançada entra depois |

---

## 11. Critérios de aceite da v1 (pronto = feito)

1. O administrador cria uma temporada, uma rodada com lição e 8 perguntas, publica, e a rodada
   aparece automaticamente aos participantes às 13h de domingo.
2. Um participante se cadastra com o código de convite, responde as 8 perguntas com cronômetro por
   pergunta e recebe pontuação por acerto + velocidade calculada no servidor.
3. O mesmo participante não consegue responder a rodada uma segunda vez, nem por duplo clique, nem
   por chamada direta à API.
4. Nenhuma requisição feita durante a rodada aberta revela qual alternativa é a correta.
5. Após sábado 23h59, a rodada encerra sozinha, o gabarito é revelado e o ranking semanal é
   publicado.
6. O ranking da temporada soma corretamente as rodadas e desempata por menor tempo total.
7. Tudo utilizável em um celular de 360 px de largura.
8. A suíte de testes cobre pontuação, janela de tempo, tentativa única e vazamento de gabarito, e
   roda no CI.
