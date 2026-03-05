# Shopee Integration

Aplicação desktop Windows para integração com a **Shopee Open Platform API**. Permite autenticar com a loja, listar produtos, atualizar estoque e preço, e listar pedidos usando a API v2 da Shopee (Sandbox e Production).

## O que a aplicação faz

- **Autenticação OAuth:** gera URL de autorização, captura o `code` via redirect em localhost e troca por access token; persiste access token, refresh token e data de expiração de forma segura.
- **Refresh automático:** antes de chamadas à API, verifica se o token expirou e renova automaticamente com o refresh token salvo.
- **Produtos:** lista itens da loja (get_item_list) e permite atualizar estoque e preço do item selecionado (update_stock, update_price).
- **Pedidos:** lista pedidos dos últimos 14 dias (get_order_list), com detalhes dos itens quando disponíveis.
- **Credenciais:** Partner ID, Shop ID, API Key e ambiente (Sandbox/Production) são salvos ao fechar o app e recarregados na abertura.

## Requisitos

- **Windows** (usa Windows Forms e Credential Manager).
- **.NET 8+** (projeto configurado para `net10.0-windows`).
- Conta na [Shopee Open Platform](https://open.shopee.com) e app com Partner ID, Shop ID e Partner Key (API Key).

## Como utilizar

### 1. Compilar e executar

```bash
dotnet build
dotnet run
```

Ou abrir a solução no Visual Studio e executar (F5).

### 2. Preencher credenciais

No topo da janela:

- **Partner ID** e **Shop ID:** números do seu app/loja no painel da Shopee.
- **API Key:** Partner Key do app (pode ser texto ou no formato `shpk` + hex, conforme o painel).
- **Environment:** **Sandbox** para testes ou **Production** para loja real.

Clique em **Conectar** para inicializar o serviço com esses dados.

### 3. Obter access token (primeira vez ou nova autorização)

**Opção A – Fluxo com captura automática do code (recomendado)**

1. Certifique-se de ter registrado no **Partner Center da Shopee** a redirect URI:
   - `http://127.0.0.1:8765/callback`
2. Clique em **Gerar Auth URL**. O app:
   - inicia um servidor local na porta 8765;
   - abre o navegador na página de login da Shopee;
   - aguarda o redirect com o `code` (até 5 minutos).
3. Faça login e autorize o app na Shopee. Ao ser redirecionado para `http://127.0.0.1:8765/callback?code=...`, o app recebe o `code`, troca por access token e salva tokens + expiração. Uma mensagem de sucesso será exibida.

**Opção B – Code manual**

1. Clique em **Gerar Auth URL** (pode usar qualquer redirect URI registrada no painel).
2. Após autorizar, copie o parâmetro `code` da URL de redirect e cole no campo **Auth Code**.
3. Clique em **Get Access Token**. O app troca o code por access token e salva access token, refresh token e expiração.

Os tokens ficam armazenados de forma segura (Windows Credential Manager ou arquivo cifrado no perfil do usuário). Em chamadas seguintes, o app usa o token salvo e renova automaticamente quando expirado.

### 4. Refresh manual (opcional)

Se quiser forçar a renovação do access token, use o botão **Refresh Token**. O app usa o refresh token salvo e persiste o novo access token e o novo refresh token.

### 5. Abas Produtos e Pedidos

- **Produtos:** **Listar Produtos** carrega os itens; selecione um item, altere Estoque/Preço na grid e clique em **Atualizar Estoque/Preço**.
- **Pedidos:** **Listar Pedidos** carrega os pedidos dos últimos 14 dias. A listagem de produtos pode vir com apenas `item_id` (nome/estoque/preço podem precisar de detalhe via API).

O status das operações aparece na barra de status na parte inferior da janela.

## Onde os dados são guardados

- **Credenciais (Partner ID, Shop ID, API Key, Environment, Auth Code):**  
  `%LocalAppData%\ShopeeIntegration\credentials.json` (salvo ao fechar o app).

- **Access token / Refresh token:**  
  Windows Credential Manager (alvos `ShopeeIntegration_AccessToken` e `ShopeeIntegration_RefreshToken`) ou, em fallback, arquivos cifrados no mesmo diretório (`access_token.dat`, `refresh_token.dat`).

- **Data de expiração do token:**  
  `%LocalAppData%\ShopeeIntegration\token_expires_at.dat`.

- **Log:**  
  `%LocalAppData%\ShopeeIntegration\shopee_integration.log` (requisições e respostas da API para diagnóstico).

## Redirect URI para captura do code

Para o fluxo “Gerar Auth URL” com captura automática do `code`, cadastre no painel da Shopee (Sandbox e/ou Production) a redirect URI exata:

- `http://127.0.0.1:8765/callback`

A Shopee exige correspondência exata da URI. A porta 8765 é fixa no código; se outra aplicação já usar essa porta, pode ser necessário alterar a constante no código.

## Melhorias possíveis

- **Detalhe de produto:** chamar `get_item_detail` por item ao listar produtos, para preencher nome, estoque e preço na grid quando a listagem retornar só `item_id` e metadados básicos.
- **Configurável redirect URI/porta:** permitir configurar a URL de callback (e a porta) em arquivo de configuração ou na tela, em vez de valor fixo no código.
- **Filtros na listagem:** filtros por status do item, busca por nome/SKU e paginação configurável na tela.
- **Pedidos:** abas ou painel para ver detalhes do pedido (itens, valores, status) e, se a API permitir, ações como marcar como enviado.
- **Tratamento de erros na UI:** exibir mensagens da API (ex.: “invalid sign”, “token expired”) de forma mais clara e sugerir ações (ex.: “Refaça o login” ou “Clique em Refresh Token”).
- **Múltiplas lojas:** suporte a mais de um Partner ID/Shop ID (perfis ou seleção de loja) e tokens por loja.
- **Logs e diagnóstico:** opção na interface para abrir a pasta de log ou visualizar as últimas linhas do log; máscara da API Key em todos os logs.
- **Testes automatizados:** testes unitários para assinatura (HMAC), parsing de resposta (ex.: `item` vs `items`) e fluxo de token (refresh automático).
- **Atualização em lote:** atualizar estoque ou preço de vários itens selecionados de uma vez, com fila e feedback de progresso.
- **Exportação:** exportar lista de produtos ou pedidos para CSV/Excel.

---

Projeto .NET (WinForms). Consulte a [documentação da Shopee Open Platform](https://open.shopee.com/documents) para detalhes dos endpoints e regras de negócio.
