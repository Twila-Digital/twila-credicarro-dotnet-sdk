<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/Twila-Digital/twila-credicarro-dotnet-sdk/production/assets/logo-dark.svg">
    <img src="https://raw.githubusercontent.com/Twila-Digital/twila-credicarro-dotnet-sdk/production/assets/logo-light.svg" alt="Credicarro" width="180" style="max-width: 100%;">
  </picture>
</p>

<p align="center">
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/github/license/Twila-Digital/twila-credicarro-dotnet-sdk"></a>
  <a href="https://github.com/Twila-Digital/twila-credicarro-dotnet-sdk/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/Twila-Digital/twila-credicarro-dotnet-sdk/actions/workflows/ci.yml/badge.svg"></a>
  <a href="https://github.com/Twila-Digital/twila-credicarro-dotnet-sdk/actions/workflows/quality.yml"><img alt="Quality" src="https://github.com/Twila-Digital/twila-credicarro-dotnet-sdk/actions/workflows/quality.yml/badge.svg"></a>
  <a href="https://github.com/Twila-Digital/twila-credicarro-dotnet-sdk/security/code-scanning"><img alt="Security" src="https://github.com/Twila-Digital/twila-credicarro-dotnet-sdk/actions/workflows/security.yml/badge.svg"></a>
  <a href="https://codecov.io/gh/Twila-Digital/twila-credicarro-dotnet-sdk"><img alt="Coverage" src="https://codecov.io/gh/Twila-Digital/twila-credicarro-dotnet-sdk/branch/production/graph/badge.svg"></a>
  <img alt="Target Frameworks" src="https://img.shields.io/badge/.NET-netstandard2.0%20%7C%20net8.0-512BD4">
</p>

# Credicarro

SDK .NET oficial para a API de integração do [Credicarro](https://documentacao.credicarro.com.br): financiamento e refinanciamento de veículos, do cadastro do cliente à proposta, ao envio de documentos e aos webhooks.

> Uso restrito a server-side. O `ClientSecret` nunca deve ser embarcado em um app mobile, SPA ou qualquer código que rode no dispositivo do usuário final.

## Compatibilidade

Publica dois target frameworks: `netstandard2.0` e `net8.0` (build nativo).

| Runtime | Versões aceitas |
| --- | --- |
| .NET / .NET Core | 2.0 até a versão mais recente (5, 6, 7, 8, 9, 10) |
| .NET Framework | 4.6.2 ou superior |
| Xamarin / Mono / UWP | Qualquer versão compatível com .NET Standard 2.0 |

> .NET Core 1.0/1.1 **não são suportados**, porque só implementam .NET Standard 1.6.

## Instalação

```bash
dotnet add package Twila.Credicarro
```

## Quick start

```csharp
using Credicarro.Configuration;
using Credicarro.DependencyInjection;

services.AddCredicarro(options =>
{
    options.ClientId = "<client-id>";
    options.ClientSecret = "<client-secret>";
    options.Environment = CredicarroEnvironment.Staging; // padrão: Production
});
```

`AddCredicarro` registra o `IHttpClientFactory`, a autenticação (token em cache, renovado antes de expirar), a política de retry e circuit breaker (Polly.Core) e o `ICredicarroClient` como singleton. Guarde `ClientId` e `ClientSecret` em variáveis de ambiente ou em um gerenciador de segredos, nunca no código.

| Ambiente | URL |
| --- | --- |
| `Staging` | `https://api.staging.credicarro.com.br/integration/` |
| `Production` (padrão) | `https://api.credicarro.com.br/integration/` |

`BaseUrl` substitui a URL do ambiente e precisa ser HTTPS (`http` só é aceito para `localhost` e `127.0.0.1`, em testes). Opções inválidas lançam `CredicarroConfigurationException` na primeira vez que o client é resolvido.

## Fluxo da proposta

O cliente é identificado pelo **CPF** em todas as chamadas. O caminho feliz de um financiamento:

```csharp
var client = provider.GetRequiredService<ICredicarroClient>();

// 1. Cadastro completo do cliente (dados pessoais, endereço e ocupação/renda).
await client.Customers.UpsertAsync(new UpsertCustomerRequest(
    Cpf: "12345678909",
    FirstName: "Maria",
    LastName: "Souza",
    Email: "maria.souza@exemplo.com.br",
    PhoneNumber: "+55 (11) 99876-5432",
    DateOfBirth: new DateTimeOffset(1990, 5, 20, 0, 0, 0, TimeSpan.FromHours(-3)),
    Address: new CustomerAddress("Av. Paulista", "Bela Vista", "São Paulo", "SP", "01311000", Number: "1578"),
    ProfessionalData: new ProfessionalData(OccupationType.Employee, 6500.00m)));

// 2. Simulação: uma opção por prazo.
var opcoes = await client.Simulations.CreateFinancingAsync(new CreateFinancingSimulationRequest(
    Cpf: "12345678909",
    LicensePlate: "ABC1D23",
    VehicleAmount: 45000m,
    DownPayment: 9000m,
    State: "SP",
    IsZeroKm: false,
    StoreCnpj: "11.222.333/0001-81"));

// 3. Proposta a partir da opção escolhida (cada opção gera uma única proposta).
var propostaId = await client.Proposals.CreateAsync(new CreateProposalRequest("12345678909", opcoes[0].SimulationId));

// 4. Acompanhe a proposta e veja o que o seu sistema precisa enviar.
var proposta = await client.Proposals.GetAsync(propostaId);

foreach (var acao in proposta.GetPendingActions())
    Console.WriteLine($"{acao.Participant}: {acao.Kind} (etapa {acao.Step})");
```

Para refinanciamento use `GetRefinancingRangeAsync` e `CreateRefinancingAsync` (com `RequestedAmount` dentro da faixa da placa). O refinanciamento ainda pede a conta de recebimento (`SendDepositAccountAsync`).

### O que enviar: `GetPendingActions()`

`ProposalDetails.GetPendingActions()` olha cada participante (comprador, vendedor e loja) e lista só o que cabe ao integrador, isto é, quando a etapa ativa tem `ActionOwner == Integrator`:

| Etapa ativa | `PendingActionKind` | Como resolver |
| --- | --- | --- |
| `DocumentSubmission` (6) | `SendDocuments` | `Proposals.UploadDocumentAsync` para cada tipo em `DocumentTypes` (documentos `Pending`) |
| `DocumentCorrection` (7) | `CorrectDocuments` | Reenviar os tipos em `DocumentTypes` (documentos `Invalid`) com uma **nova** `Idempotency-Key`; o motivo está em `ProposalDocument.Reason` |
| `VehicleDetails` (8) | `SendVehicleDetails` | `Proposals.SendVehicleDetailsAsync` |
| `DepositAccount` (5) | `SendDepositAccount` | `Proposals.SendDepositAccountAsync` (refinanciamento) |

```csharp
foreach (var acao in proposta.GetPendingActions())
{
    switch (acao.Kind)
    {
        case PendingActionKind.SendVehicleDetails:
            await client.Proposals.SendVehicleDetailsAsync(
                propostaId, new VehicleDetails(Renavam: "12345678900", Chassis: "9BWZZZ377VT004251", LicensePlate: "ABC1D23", Color: "PRATA"));
            break;

        case PendingActionKind.SendDepositAccount:
            await client.Proposals.SendDepositAccountAsync(
                propostaId, new DepositAccount("12345678909", "001", "1234", "5", "123456", "7", DepositAccountType.Checking));
            break;
    }
}
```

O mesmo `ProposalParticipantState` (etapa ativa, etapas concluídas e documentos) vem na consulta e no webhook detalhado. Todo valor numérico tem o código bruto e o nome (`StatusRaw`/`StatusName`, `StepRaw`/`StepName`...); valores novos da API viram `Unknown` sem lançar exceção.

### Enviar documentos

Um arquivo por chamada, em Base64, até 10 MiB (PDF, JPG/JPEG ou PNG). `DocumentFile` valida nome (sem caminho, até 255 caracteres), extensão e tamanho antes de enviar:

```csharp
var arquivo = DocumentFile.FromFile("/tmp/cnh-comprador.pdf");     // ou FromBytes(bytes, "cnh.pdf") / FromStream(stream, "cnh.pdf")

await client.Proposals.UploadDocumentAsync(
    propostaId,
    new ProposalDocumentUpload(ProposalParticipant.Buyer, DocumentType.Cnh, arquivo),
    idempotencyKey: "doc-7f3c2a10-comprador-cnh");                  // uma chave por envio
```

A `Idempotency-Key` é **obrigatória** nesta rota. Gere uma chave **por envio**, guarde-a junto ao registro do documento e reaproveite-a ao repetir esse envio: com a mesma chave e o mesmo conteúdo, um envio bem-sucedido nas últimas 24 horas não é processado de novo. Use uma chave **nova** ao reenviar uma correção. Sem `idempotencyKey`, o SDK gera uma por chamada e a repete em todas as tentativas automáticas daquela chamada.

## Clientes por recurso

| Cliente | Métodos |
| --- | --- |
| `client.Customers` | `UpsertAsync` (`POST v1/customer/unified`), `CreateAsync`, `UpsertAddressAsync`, `UpsertProfessionalDataAsync` |
| `client.Simulations` | `GetFinancingRangeAsync`, `GetRefinancingRangeAsync`, `CreateFinancingAsync`, `CreateRefinancingAsync`, `GetAsync`, `ListAsync` |
| `client.Proposals` | `CreateAsync`, `GetAsync`, `ListAsync`, `SendVehicleDetailsAsync`, `SendDepositAccountAsync`, `UploadDocumentAsync` |
| `client.Webhooks` | `CreateAsync`, `ListAsync`, `UpdateAsync`, `DeleteAsync` |
| `client.V1.Simulations` | `GetFinancingRangeAsync(placa)`, `GetRefinancingRangeAsync(placa)`, `CreateFinancingAsync`, `CreateRefinancingAsync` (rotas v1, sem `cnpjLoja`) |
| `client.V1.Proposals` | `CreateAsync` (`POST v1/proposal`), `GetAsync` (`GET v1/proposal/{id}`, formato resumido) |
| `CredicarroWebhookEvent` | `Parse`, `VerifySignature`, `VerifyAuthorization` (estático, para o seu endpoint de webhook) |

Todos os métodos são assíncronos e aceitam `CancellationToken`. As rotas v2 (`Simulations` e `Proposals`) escolhem a loja pelo `cnpjLoja` e devolvem o retrato com etapas e documentos; as rotas v1 (`client.V1`) ficam para integrações antigas.

## Idempotência e retentativas

O SDK gera uma `Idempotency-Key` (UUID) **por chamada lógica** e a repete em **todas** as tentativas. As chamadas seguras são repetidas automaticamente em `408`, `429` (respeitando `Retry-After`), `500`, `502`, `503`, `504`, falha de rede e timeout de tentativa, com backoff exponencial e jitter. Outros `4xx` nunca são repetidos.

| Chamada | Chave | Repetida? |
| --- | --- | --- |
| `GET`, geração do token | n/a | Sim |
| `Customers.CreateAsync`, `UpsertAddressAsync`, `UpsertProfessionalDataAsync` | Automática (ou a sua, em `idempotencyKey`) | Sim |
| `Simulations.CreateFinancingAsync` e `CreateRefinancingAsync` (v2 e v1) | Automática | Sim |
| `Proposals.CreateAsync` (v2 e v1) | Automática | Sim |
| `Proposals.UploadDocumentAsync` | Obrigatória: a sua (`idempotencyKey`) ou uma gerada | Sim, com a mesma chave |
| `Proposals.SendVehicleDetailsAsync`, `SendDepositAccountAsync` | Sem chave (reenviar substitui) | Sim |
| `Webhooks.UpdateAsync`, `DeleteAsync` | Sem chave | Sim |
| **`Customers.UpsertAsync`** (`POST v1/customer/unified`) | **Nunca automática** | **Só se você passar `idempotencyKey`** |
| `Webhooks.CreateAsync` | Sem chave | **Não** (repetir cadastraria o webhook de novo) |

> **Atenção ao `Customers.UpsertAsync`.** A chave muda o comportamento da rota. **Sem** `idempotencyKey` (o padrão), ela cria o cliente ou atualiza o cadastro se o CPF já existir (upsert), e por isso uma falha de rede **não é repetida** pelo SDK. **Com** `idempotencyKey`, ela só cria: se o CPF já estiver cadastrado, responde `409` (`Cpf.CpfAlreadyAdded`), e o SDK repete a chamada com a mesma chave. Para atualizar um cliente existente, não passe a chave.

`500` é repetido por padrão (`RetryOn500 = true`), mas só nas chamadas seguras acima. Para desligar as chaves automáticas, use `Resilience.DisableAutomaticIdempotencyKey` (essas chamadas deixam de ser repetidas; o envio de documento continua levando chave, porque o header é obrigatório).

### Resiliência

```csharp
services.AddCredicarro(options =>
{
    options.ClientId = "<client-id>";
    options.ClientSecret = "<client-secret>";
    options.Resilience.TotalTimeout = TimeSpan.FromSeconds(30);                 // padrão
    options.Resilience.AttemptTimeout = TimeSpan.FromSeconds(10);               // padrão
    options.Resilience.DocumentUploadAttemptTimeout = TimeSpan.FromSeconds(60); // padrão; orçamento do upload = 60 s x MaxRetryAttempts (180 s)
    options.Resilience.MaxRetryAttempts = 3;                                    // padrão, contando a tentativa original
});
```

`Retry-After` (segundos ou data HTTP) é respeitado até o orçamento restante; se for maior, o SDK não espera e lança `CredicarroRateLimitException` na hora. Redirects não são seguidos (viram `CredicarroApiException`) e falha de rede depois das tentativas vira `CredicarroTimeoutException` com causa sem dados da requisição. O `ToString` de requisições e respostas mascara dados pessoais, mas a serialização (System.Text.Json) leva os valores reais, para você persistir em fila ou cache; `CredicarroOptions` (com o `ClientSecret`) recusa serializar.

Circuit breaker: abre com 50% de falhas em 30 s (mínimo de 10 chamadas) e fica aberto por 15 s. Timeout e circuit breaker aberto lançam `CredicarroTimeoutException`.

## Paginação

`Simulations.ListAsync`, `Proposals.ListAsync` e `Webhooks.ListAsync` retornam `PagedResult<T>` e não têm auto-paginação: você controla o avanço de página. `Page` começa em 1 e `PageSize` vai de 1 a 100 (validado no cliente, antes de chamar a API).

```csharp
var pagina = await client.Proposals.ListAsync(new ListRequest(Page: 1, PageSize: 20));

foreach (var proposta in pagina.Items)
    Console.WriteLine($"{proposta.Number} {proposta.Status}");

if (pagina.HasNext)
    pagina = await client.Proposals.ListAsync(new ListRequest(Page: pagina.Page + 1, PageSize: 20));
```

## Tratamento de erros

| Exceção | Quando |
| --- | --- |
| `CredicarroConfigurationException` | `CredicarroOptions` inválidas (ex.: `ClientId` ausente, `BaseUrl` sem HTTPS) |
| `CredicarroAuthenticationException` | Credenciais inválidas (`401`/`400` ao gerar o token) ou `401` persistente depois de renovar o token |
| `CredicarroValidationException` | `400`: dado inválido (veja `ErrorCode` e `Field`) |
| `CredicarroBusinessRuleException` | `422`: regra de negócio não atendida (ex.: `Store.Integration.Intermediation`) |
| `CredicarroRateLimitException` | `429`, com `RetryAfter` |
| `CredicarroApiException` | Qualquer outro erro de API (`403`, `404`, `409`, `5xx`) e base das três acima |
| `CredicarroTimeoutException` | Timeout ou circuit breaker aberto |
| `CredicarroWebhookSignatureException` | Assinatura de webhook inválida, expirada ou corpo ilegível |

`CredicarroApiException` expõe `StatusCode`, `ErrorCode` (o `tipo` do erro, o campo estável para o seu tratamento), `Title`, `Detail`, `Field`, `Errors` (lista com `Code`, `Message` e `Field`) e `RawBody`. O SDK lê também o formato padrão `type`/`title`/`detail` usado em falhas inesperadas, e um `401` sem corpo vira `CredicarroAuthenticationException`.

```csharp
try
{
    await client.Proposals.CreateAsync(new CreateProposalRequest(cpf, simulacaoId));
}
catch (CredicarroBusinessRuleException ex) when (ex.ErrorCode == "Proposal.SimulationAlreadyExpired")
{
    // Refaça a simulação.
}
catch (CredicarroApiException ex)
{
    logger.LogWarning("Erro da API | Status: {Status} | Codigo: {Codigo} | Campo: {Campo}", (int)ex.StatusCode, ex.ErrorCode, ex.Field);
}
```

A mensagem da exceção traz só o status, o `tipo` e o `titulo`. `Detail` e `RawBody` podem conter dados pessoais (CPF, e-mail): evite registrá-los em logs.

## Webhooks

Cadastre o webhook e guarde o segredo de assinatura, que **só é devolvido nesta resposta**:

```csharp
var webhook = await client.Webhooks.CreateAsync(new CreateWebhookRequest(
    WebhookType.Proposal,
    "https://parceiro.com.br/webhooks/credicarro",
    WebhookAuthenticationType.Basic,
    Credential: "<usuario:senha em Base64>"));

// webhook.WebhookId e webhook.SigningSecret
```

No seu endpoint, leia o **corpo bruto** (antes de desserializar) e valide a assinatura do header `X-CrediCarro-Signature`:

```csharp
using Credicarro.Webhooks;

// rawBody: string com o corpo exatamente como recebido
// 401 se a assinatura for inválida; 2xx na hora e processe de forma assíncrona.
if (!CredicarroWebhookEvent.VerifySignature(rawBody, signatureHeader, signingSecret))
    return Results.Unauthorized();

var evento = CredicarroWebhookEvent.Parse(rawBody, signatureHeader, signingSecret);

Console.WriteLine($"{evento.ProposalId}: {evento.Status} ({evento.EventReason})");
```

A assinatura é o HMAC-SHA256, em hexadecimal minúsculo, de `{t}.{corpo bruto}` com o `segredo_assinatura` (texto UTF-8) como chave, comparada em tempo constante. A tolerância de replay é de 5 minutos, nos dois sentidos (`Parse(rawBody, header, secret, tolerance)` aceita outro valor). Segredo vazio nunca valida. `ParseUnverified(rawBody)` (sem verificação) existe só para testes; `Parse` exige header e segredo, um `id_proposta` UUID válido, e recusa `t`/`v1` repetidos no header.

`ProposalWebhookEvent` aceita os dois formatos de payload: o simples (`id_proposta`, `enum_status` e `status` em inglês) e o detalhado de propostas criadas por `POST v2/proposal`, com `EventReason`, `Buyer`, `Store` e as mesmas etapas e documentos da consulta (`IsDetailed`). Os `Buyer`/`Store` do evento têm o mesmo tipo de `ProposalDetails`, então a mesma lógica de pendências vale para os dois.

Use também `CredicarroWebhookEvent.VerifyAuthorization(headerRecebido, headerEsperado)` para conferir o header `Authorization` (`Basic ...` ou `Bearer ...`) configurado no cadastro, e deduplique as entregas pelo header `Idempotency-Key`. O sample `Credicarro.Sample.AspNetCore` mostra um endpoint completo, que lê o corpo bruto com limite de 1 MiB.

## Samples

- `samples/Credicarro.Sample.Console`: .NET 8, DI standalone, fluxo cliente, simulação e proposta
- `samples/Credicarro.Sample.AspNetCore`: .NET 8, minimal API com consulta de proposta e endpoint de webhook (corpo bruto limitado a 1 MiB)
- `samples/Credicarro.Sample.NetFramework`: net472, `ServiceCollection` standalone

## Qualidade, segurança e cobertura

- **Build** (`ci.yml`): compila os dois TFMs em `ubuntu-latest` e `windows-latest`.
- **Test** (`ci.yml`): testes unitários em `net8.0` (ambos os OS) e `net472` (`windows-latest`, único onde .NET Framework roda de verdade).
- **Contract tests** (`contract-tests.yml`): conferem os tipos de fio contra o `openapi.yaml` público da documentação (o swagger de staging exige autenticação).
- **Quality** (`quality.yml`): análise estática via Codacy CLI, resultados publicados na aba **Security → Code scanning** do repositório.
- **Security** (`security.yml`): [CodeQL](https://codeql.github.com/) para C#, rodando a cada PR/push e semanalmente.
- **Coverage**: cobertura de testes coletada via `coverlet.collector` e publicada no [Codecov](https://codecov.io/gh/Twila-Digital/twila-credicarro-dotnet-sdk).

## Documentação completa

[documentacao.credicarro.com.br](https://documentacao.credicarro.com.br): referência de todos os endpoints, autenticação, erros, etapas e status, webhooks e mais.

## Contribuindo

Veja [CONTRIBUTING.md](CONTRIBUTING.md).

## Código de conduta

Este projeto segue o [Código de Conduta](CODE_OF_CONDUCT.md).

## Licença

[MIT](LICENSE)
