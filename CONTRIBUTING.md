# Contribuindo

## Pré-requisitos

- .NET SDK conforme o [`global.json`](global.json)
- (Opcional) Credenciais de staging para rodar os samples/testes de integração manual

## Build e testes

```bash
dotnet restore
dotnet build
dotnet test tests/Credicarro.UnitTests -f net8.0
```

Os contract tests (`tests/Credicarro.ContractTests`) precisam de rede: baixam o `openapi.yaml` público da documentação (`https://documentacao.credicarro.com.br/openapi.yaml`, porque o swagger de staging exige autenticação) e conferem os tipos de fio do SDK contra ele. Para usar outra URL ou um arquivo local, defina `CREDICARRO_CONTRACT_TESTS_OPENAPI_URL`. Rode com `dotnet test tests/Credicarro.ContractTests`; no CI, veja o workflow `contract-tests.yml`.

## Instalando a partir do código-fonte

Pra testar mudanças locais antes de um release, use um `ProjectReference` direto:

```xml
<ItemGroup>
  <ProjectReference Include="../caminho/para/twila-credicarro-dotnet-sdk/src/Credicarro/Credicarro.csproj" />
</ItemGroup>
```

Ou empacote localmente e consuma via feed de arquivo:

```bash
dotnet pack src/Credicarro/Credicarro.csproj -c Release -o ./artifacts
dotnet add package Twila.Credicarro --source ./artifacts
```

## Abrindo um PR

1. Crie uma branch a partir de `production`
2. Adicione testes para qualquer mudança de comportamento
3. Rode `dotnet build` e `dotnet test tests/Credicarro.UnitTests` localmente antes de abrir o PR
4. Abra o PR contra `production` — o CI roda build + testes automaticamente

## Release (publicação no NuGet.org)

`release.yml` publica via [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) — sem API key de longa duração armazenada no repositório. Antes do primeiro release, alguém com acesso à conta/organização do nuget.org precisa configurar:

1. Em **nuget.org → seu usuário → Trusted Publishing**, adicionar uma política:
   - **Repository owner:** `Twila-Digital`
   - **Repository:** `twila-credicarro-dotnet-sdk`
   - **Workflow file:** `release.yml` (só o nome do arquivo, sem o caminho `.github/workflows/`)
   - **Environment:** `production`
2. No repositório do GitHub, criar o [environment](https://docs.github.com/actions/deployment/targeting-different-environments/using-environments-for-deployment) `production` (Settings → Environments) e adicionar o secret `NUGET_USER` com o nome de usuário (perfil) do nuget.org — **não o e-mail**.

Com isso configurado, `git push --tags` numa tag `v*` (ex.: `v1.0.0`) dispara build → contract tests → pack → publicação automaticamente.

## Reportando problemas

Abra uma [issue](https://github.com/Twila-Digital/twila-credicarro-dotnet-sdk/issues) com passos para reproduzir, versão do pacote/TFM e o comportamento esperado vs. observado. Nunca inclua `ClientId`/`ClientSecret` reais no relato.
