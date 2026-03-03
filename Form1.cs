using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShopeeIntegration 
{
    public partial class Form1 : Form
    {
        private ShopeeService _service;

        public Form1()
        {
            InitializeComponent();
        }

        private void SetStatus(string text)
        {
            toolStatus.Text = text;
            Application.DoEvents();
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (!long.TryParse(txtPartnerId.Text.Trim(), out var partnerId))
            {
                MessageBox.Show("Partner ID inválido.");
                return;
            }

            if (!long.TryParse(txtShopId.Text.Trim(), out var shopId))
            {
                MessageBox.Show("Shop ID inválido.");
                return;
            }

            var apiKey = txtApiKey.Text.Trim();
            var env = cbEnvironment.SelectedItem?.ToString() ?? "Sandbox";

            _service = new ShopeeService(partnerId, shopId, apiKey, env == "Production" ? ShopeeEnvironment.Production : ShopeeEnvironment.Sandbox);
            SetStatus("Serviço inicializado.");
        }

        private async void BtnListProducts_Click(object sender, EventArgs e)
        {
            if (_service == null)
            {
                MessageBox.Show("Conecte primeiro (clique em Conectar).");
                return;
            }

            try
            {
                SetStatus("Buscando produtos...");
                var products = await _service.GetProductsAsync();
                dgvProducts.DataSource = products;
                SetStatus($"Produtos carregados: {products.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar produtos: " + ex.Message);
                SetStatus("Erro ao carregar produtos.");
            }
        }

        private async void BtnUpdateSelected_Click(object sender, EventArgs e)
        {
            if (_service == null)
            {
                MessageBox.Show("Conecte primeiro.");
                return;
            }

            if (dgvProducts.SelectedRows.Count == 0)
            {
                MessageBox.Show("Selecione um produto na lista.");
                return;
            }

            var row = dgvProducts.SelectedRows[0].DataBoundItem as ProductModel;
            if (row == null)
            {
                MessageBox.Show("Falha ao obter produto selecionado.");
                return;
            }

            var newStock = row.Stock;
            var newPrice = row.Price;

            try
            {
                SetStatus("Atualizando estoque/preço...");
                var ok = await _service.UpdateStockPriceAsync(row.ItemId, newStock, newPrice);
                SetStatus(ok ? "Atualizado com sucesso." : "Falha na atualização.");
                if (ok) await BtnListProductsReload();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao atualizar: " + ex.Message);
                SetStatus("Erro ao atualizar.");
            }
        }

        private async Task BtnListProductsReload()
        {
            var products = await _service.GetProductsAsync();
            dgvProducts.DataSource = products;
        }

        private async void BtnListOrders_Click(object sender, EventArgs e)
        {
            if (_service == null)
            {
                MessageBox.Show("Conecte primeiro.");
                return;
            }

            try
            {
                SetStatus("Buscando pedidos...");
                var orders = await _service.GetOrdersAsync();
                dgvOrders.DataSource = orders;
                SetStatus($"Pedidos carregados: {orders.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar pedidos: " + ex.Message);
                SetStatus("Erro ao carregar pedidos.");
            }
        }

        private void BtnGetAuthUrl_Click(object sender, EventArgs e)
        {
            if (!long.TryParse(txtPartnerId.Text.Trim(), out var partnerId))
            {
                MessageBox.Show("Partner ID inválido.");
                return;
            }

            // Gera URL de autorização para o lojista. Ajuste redirect_uri conforme sua aplicação.
            var redirectUri = "https://shopee.com.br/"; // substituir pelo seu redirect URI registrado
            var url = ShopeeService.GetAuthorizationUrl(partnerId, redirectUri, cbEnvironment.SelectedItem?.ToString() == "Production");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
            SetStatus("Abra o navegador para autorizar a loja e copie o authorization_code.");
        }

        private async void BtnGetToken_Click(object sender, EventArgs e)
        {
            if (_service == null)
            {
                MessageBox.Show("Conecte primeiro.");
                return;
            }

            var code = txtAuthCode.Text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("Informe o authorization_code obtido após autorização.");
                return;
            }

            try
            {
                SetStatus("Trocando authorization_code por access_token...");
                var token = await _service.GetAccessTokenAsync(code);
                if (!string.IsNullOrEmpty(token))
                {
                    SetStatus("Token obtido e armazenado (seguro).");
                    MessageBox.Show("Access token obtido com sucesso.");
                }
                else
                {
                    SetStatus("Falha ao obter token.");
                    MessageBox.Show("Falha ao obter token. Verifique logs/erros.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao obter token: " + ex.Message);
                SetStatus("Erro ao obter token.");
            }
        }
    }

    public enum ShopeeEnvironment
    {
        Sandbox,
        Production
    }

    public class ProductModel
    {
        public long ItemId { get; set; }
        public string Name { get; set; } = "";
        public int Stock { get; set; }
        public decimal Price { get; set; }
    }

    public class OrderItemModel
    {
        public long ItemId { get; set; }
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class OrderModel
    {
        public string OrderId { get; set; } = "";
        public string BuyerName { get; set; } = "";
        public decimal TotalPrice { get; set; }
        public List<OrderItemModel> Items { get; set; } = new List<OrderItemModel>();
    }

    // Exceção customizada para erros da API Shopee
    public class ShopeeApiException : Exception
    {
        public int ErrorCode { get; }
        public string ShopeeMessage { get; }

        public ShopeeApiException(string message, int errorCode = -1, string shopeeMessage = null) : base(message)
        {
            ErrorCode = errorCode;
            ShopeeMessage = shopeeMessage;
        }
    }

    // Logger simples (arquivo)
    public static class SimpleLogger
    {
        private static readonly object _sync = new();
        private static readonly string _logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShopeeIntegration");
        private static readonly string _logFile = Path.Combine(_logDir, "shopee_integration.log");

        public static void Log(string message)
        {
            try
            {
                lock (_sync)
                {
                    Directory.CreateDirectory(_logDir);
                    var line = $"{DateTime.UtcNow:O} | {message}";
                    File.AppendAllLines(_logFile, new[] { line }, Encoding.UTF8);
                }
            }
            catch
            {
                // never throw from logger
            }
        }

        public static void LogRequest(string url, string body)
        {
            var safeUrl = MaskApiKeyInUrl(url);
            Log($"REQUEST -> {safeUrl} | BODY: {Truncate(body, 2000)}");
        }

        public static void LogResponse(string url, string response, int statusCode)
        {
            var safeUrl = MaskApiKeyInUrl(url);
            Log($"RESPONSE <- {safeUrl} | STATUS: {statusCode} | BODY: {Truncate(response, 2000)}");
        }

        private static string Truncate(string s, int max) => s?.Length > max ? s.Substring(0, max) + "..." : s;

        private static string MaskApiKeyInUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            // mask common api_key query param occurrences
            return url.Replace("api_key=", "api_key=[REDACTED]");
        }
    }

    public class ShopeeService
    {
        private readonly long _partnerId;
        private readonly long _shopId;
        private readonly string _apiKey;
        private readonly ShopeeEnvironment _env;
        private readonly HttpClient _http;

        // Hosts (ajuste se a documentação oficial indicar outro) partner.test.shopeemobile.com
        private const string SandboxHost = "https://open.sandbox.test-stable.shopee.com/authorize";
        private const string ProductionHost = "https://partner.shopeemobile.com";

        // Token persistence/file
        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShopeeIntegration");
        private static readonly string TokenFilePath = Path.Combine(AppFolder, "access_token.dat");
        private const string CredentialTarget = "ShopeeIntegration_AccessToken";

        // In-memory token
        private string _accessToken = null;
        private DateTime? _accessTokenExpiresAt = null;

        public ShopeeService(long partnerId, long shopId, string apiKey, ShopeeEnvironment env)
        {
            _partnerId = partnerId;
            _shopId = shopId;
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _env = env;
            _http = new HttpClient(new HttpClientHandler { UseCookies = false });

            // tenta carregar token seguro ao iniciar
            try
            {
                var token = LoadTokenSecure();
                if (!string.IsNullOrEmpty(token))
                {
                    _accessToken = token;
                    SimpleLogger.Log("Access token carregado do armazenamento seguro.");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Falha ao carregar token seguro: {ex.Message}");
            }
        }

        private string BaseHost => _env == ShopeeEnvironment.Production ? ProductionHost : SandboxHost;

        // ---------------------------
        // Authorization (OAuth) flow
        // ---------------------------
        public static string GetAuthorizationUrl(long partnerId, string redirectUri, bool production = false)
        {
            var host = production ? ProductionHost : SandboxHost;
            // Url de autorização varia conforme doc; este é template que pode precisar de ajustes.
            // Ajuste path/params de acordo com o que a documentação oficial indicar.
            var path = "/api/v2/shop/auth_partner";
            var url = $"{host}{path}?partner_id={partnerId}&redirect={Uri.EscapeDataString(redirectUri)}";
            return url;
        }

        public async Task<string> GetAccessTokenAsync(string authCode)
        {
            var path = "/api/v2/auth/token/get";
            var body = new
            {
                authorization_code = authCode,
                partner_id = _partnerId,
                shop_id = _shopId
            };

            var resp = await SendPostAsync(path, body, includeShopId: true, includeAuthToken: false);
            // Resposta esperada: { "error":0, "message":"success", "access_token":"...", "expires_in": 3600, ... }
            if (resp.TryGetProperty("access_token", out var tok))
            {
                _accessToken = tok.GetString();
                if (resp.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds))
                    _accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(seconds - 30);

                // Persistir o token de forma segura
                try
                {
                    SaveTokenSecure(_accessToken);
                    SimpleLogger.Log("Access token salvo de forma segura.");
                }
                catch (Exception ex)
                {
                    SimpleLogger.Log($"Falha ao salvar token seguro: {ex.Message}");
                }

                return _accessToken;
            }

            // se não retornou access_token, extrair erro para lançar
            var (code, msg) = ExtractError(resp);
            throw new ShopeeApiException("Falha ao obter access_token", code, msg);
        }

        // ---------------------------
        // Produtos / Estoque / Preço
        // ---------------------------
        public async Task<List<ProductModel>> GetProductsAsync()
        {
            var result = new List<ProductModel>();
            var page = 1;
            var pageSize = 50;

            while (true)
            {
                var path = "/api/v2/product/get_item_list";
                var body = new
                {
                    partner_id = _partnerId,
                    shop_id = _shopId,
                    page_size = pageSize,
                    page_no = page
                };

                var resp = await SendPostAsync(path, body, includeShopId: true);
                if (!resp.TryGetProperty("response", out var responseObj))
                    throw ConstructExceptionFromResponse(resp, "GetProductsAsync");

                if (!responseObj.TryGetProperty("items", out var items))
                    break;

                var itemsArray = items.EnumerateArray().ToArray();
                if (itemsArray.Length == 0) break;

                foreach (var it in itemsArray)
                {
                    var itemId = it.TryGetProperty("item_id", out var iid) ? iid.GetInt64() : 0;
                    var name = it.TryGetProperty("name", out var nm) ? nm.GetString() : "";
                    var stock = it.TryGetProperty("stock", out var st) ? st.GetInt32() : 0;
                    var price = ParseDecimalFromJsonElement(it, "price");

                    result.Add(new ProductModel
                    {
                        ItemId = itemId,
                        Name = name,
                        Stock = stock,
                        Price = price
                    });
                }

                if (itemsArray.Length < pageSize) break;
                page++;
            }

            return result;
        }

        public async Task<bool> UpdateStockPriceAsync(long itemId, int stock, decimal price)
        {
            // Atualizar estoque
            var stockPath = "/api/v2/product/update_stock";
            var stockBody = new
            {
                partner_id = _partnerId,
                shop_id = _shopId,
                item_id = itemId,
                stock = stock
            };

            var stockResp = await SendPostAsync(stockPath, stockBody, includeShopId: true);
            if (HasError(stockResp)) throw ConstructExceptionFromResponse(stockResp, "UpdateStockPriceAsync - update_stock");

            // Atualizar preço
            var pricePath = "/api/v2/product/update_price";
            var priceBody = new
            {
                partner_id = _partnerId,
                shop_id = _shopId,
                item_id = itemId,
                price = price
            };

            var priceResp = await SendPostAsync(pricePath, priceBody, includeShopId: true);
            if (HasError(priceResp)) throw ConstructExceptionFromResponse(priceResp, "UpdateStockPriceAsync - update_price");

            return true;
        }

        // ---------------------------
        // Pedidos
        // ---------------------------
        public async Task<List<OrderModel>> GetOrdersAsync()
        {
            var orders = new List<OrderModel>();
            var page = 1;
            var pageSize = 50;

            while (true)
            {
                var path = "/api/v2/orders/get_order_list";
                var body = new
                {
                    partner_id = _partnerId,
                    shop_id = _shopId,
                    page_size = pageSize,
                    page_no = page
                };

                var resp = await SendPostAsync(path, body, includeShopId: true);
                if (!resp.TryGetProperty("response", out var responseObj)) throw ConstructExceptionFromResponse(resp, "GetOrdersAsync");
                if (!responseObj.TryGetProperty("orders", out var arr)) break;

                var arrItems = arr.EnumerateArray().ToArray();
                if (arrItems.Length == 0) break;

                foreach (var o in arrItems)
                {
                    var orderId = o.TryGetProperty("order_sn", out var osn) ? osn.GetString() :
                                  (o.TryGetProperty("order_id", out var oid) ? oid.GetString() : "");
                    var buyer = o.TryGetProperty("buyer_username", out var bn) ? bn.GetString() : "";
                    var total = o.TryGetProperty("total_amount", out var ta) ? (ta.TryGetDecimal(out var d) ? d : 0m) : 0m;

                    var order = new OrderModel
                    {
                        OrderId = orderId,
                        BuyerName = buyer,
                        TotalPrice = total
                    };

                    if (o.TryGetProperty("order_lines", out var olines))
                    {
                        foreach (var li in olines.EnumerateArray())
                        {
                            order.Items.Add(new OrderItemModel
                            {
                                ItemId = li.TryGetProperty("item_id", out var iid) ? iid.GetInt64() : 0,
                                Name = li.TryGetProperty("item_name", out var iname) ? iname.GetString() : "",
                                Quantity = li.TryGetProperty("item_quantity", out var iq) ? iq.GetInt32() : 0,
                                Price = li.TryGetProperty("item_price", out var ip) && ip.TryGetDecimal(out var ipd) ? ipd : 0m
                            });
                        }
                    }
                    else
                    {
                        var detailPath = "/api/v2/orders/get_order_detail";
                        var detailBody = new
                        {
                            partner_id = _partner_id_wrapper(),
                            shop_id = _shopId,
                            order_sn = orderId
                        };
                        var detailResp = await SendPostAsync(detailPath, detailBody, includeShopId: true);
                        if (detailResp.TryGetProperty("response", out var detailRespObj) && detailRespObj.TryGetProperty("order_lines", out var detLines))
                        {
                            foreach (var li in detLines.EnumerateArray())
                            {
                                order.Items.Add(new OrderItemModel
                                {
                                    ItemId = li.TryGetProperty("item_id", out var iid) ? iid.GetInt64() : 0,
                                    Name = li.TryGetProperty("item_name", out var iname) ? iname.GetString() : "",
                                    Quantity = li.TryGetProperty("item_quantity", out var iq) ? iq.GetInt32() : 0,
                                    Price = li.TryGetProperty("item_price", out var ip) && ip.TryGetDecimal(out var ipd) ? ipd : 0m
                                });
                            }
                        }
                    }

                    orders.Add(order);
                }

                if (arrItems.Length < pageSize) break;
                page++;
            }

            return orders;
        }

        // small helper to satisfy anonymous object creation when code analysis complains about capture
        private long _partner_id_wrapper() => _partnerId;

        // ---------------------------
        // HTTP + assinatura + logs + erros
        // ---------------------------
        /// <summary>
        /// Envia POST para Shopee V2, gerando assinatura exatamente conforme documentação:
        /// Assinatura HMAC-SHA256 com chave = api_key, data = partner_id + path + timestamp + body (incluir shop_id se o endpoint requerer shop_id)
        /// IMPORTANT: confirme com a sua versão da documentação; alguns endpoints variam na concatenação.
        /// </summary>
        private async Task<JsonElement> SendPostAsync(string path, object bodyObj, bool includeShopId = false, bool includeAuthToken = true)
        {
            var urlPath = path.StartsWith("/") ? path : "/" + path;
            var host = BaseHost;
            var url = host + urlPath;

            var bodyJson = JsonSerializer.Serialize(bodyObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Construção do input da assinatura seguindo doc: partner_id + path + timestamp + body
            // Alguns endpoints pedem shop_id no input — incluímos quando includeShopId == true logo após partner_id
            var signInputBuilder = new StringBuilder();
            signInputBuilder.Append(_partnerId.ToString());
            if (includeShopId) signInputBuilder.Append(_shopId.ToString());
            signInputBuilder.Append(urlPath);
            signInputBuilder.Append(timestamp);
            signInputBuilder.Append(bodyJson);
            var signInput = signInputBuilder.ToString();

            var sign = ComputeHmacSha256(_apiKey, signInput);

            var qs = new List<string>
            {
                $"partner_id={_partnerId}",
                $"timestamp={timestamp}",
                $"sign={Uri.EscapeDataString(sign)}"
            };
            if (includeShopId)
                qs.Add($"shop_id={_shopId}");

            var reqUrl = url + "?" + string.Join("&", qs);

            // logs
            SimpleLogger.LogRequest(reqUrl, bodyJson);

            using var req = new HttpRequestMessage(HttpMethod.Post, reqUrl);
            req.Headers.Add("Accept", "application/json");
            if (includeAuthToken && !string.IsNullOrEmpty(_accessToken))
                req.Headers.Add("Authorization", $"Bearer {_accessToken}");

            req.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req);
            var respText = await resp.Content.ReadAsStringAsync();

            SimpleLogger.LogResponse(reqUrl, respText, (int)resp.StatusCode);

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(respText);
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Falha ao parsear resposta JSON: {ex.Message}");
                var fake = JsonSerializer.SerializeToElement(new { error = -1, message = "invalid json response", raw = respText });
                return fake;
            }

            var root = doc.RootElement.Clone();

            // Se header http não OK, log e lançar
            if (!resp.IsSuccessStatusCode)
            {
                var (errCode, errMsg) = ExtractError(root);
                var msg = $"HTTP {(int)resp.StatusCode} ao chamar Shopee: {errMsg}";
                SimpleLogger.Log(msg);
                throw new ShopeeApiException(msg, errCode, errMsg);
            }

            // Verificar campo de erro no payload
            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Number && err.GetInt32() != 0)
            {
                var (errCode, errMsg) = ExtractError(root);
                SimpleLogger.Log($"Erro Shopee API: code={errCode} message={errMsg}");
                throw new ShopeeApiException($"Erro API Shopee: {errMsg}", errCode, errMsg);
            }

            return root;
        }

        private static (int code, string message) ExtractError(JsonElement element)
        {
            try
            {
                if (element.TryGetProperty("error", out var e) && e.TryGetInt32(out var ec))
                {
                    var msg = element.TryGetProperty("message", out var m) ? m.GetString() : null;
                    return (ec, msg);
                }

                // tentar em response.error/message
                if (element.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.Object)
                {
                    if (r.TryGetProperty("error", out var re) && re.TryGetInt32(out var rec))
                        return (rec, r.TryGetProperty("message", out var rm) ? rm.GetString() : null);
                }
            }
            catch { }
            return (-1, null);
        }

        private static bool HasError(JsonElement elem)
        {
            if (elem.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Number && e.GetInt32() != 0) return true;
            if (elem.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.Object && r.TryGetProperty("error", out var re) && re.ValueKind == JsonValueKind.Number && re.GetInt32() != 0) return true;
            return false;
        }

        private static ShopeeApiException ConstructExceptionFromResponse(JsonElement resp, string ctx)
        {
            var (code, msg) = ExtractError(resp);
            var message = $"Erro Shopee ({ctx}): code={code} message={msg}";
            return new ShopeeApiException(message, code, msg);
        }

        private static decimal ParseDecimalFromJsonElement(JsonElement el, string propName)
        {
            if (!el.TryGetProperty(propName, out var je)) return 0m;
            if (je.ValueKind == JsonValueKind.Number && je.TryGetDecimal(out var d)) return d;
            if (je.ValueKind == JsonValueKind.String && decimal.TryParse(je.GetString(), out var d2)) return d2;
            return 0m;
        }

        private static string ComputeHmacSha256(string key, string data)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var bytes = Encoding.UTF8.GetBytes(data);
            var hash = hmac.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // ---------------------------
        // Token persistence (DPAPI + Windows Credential Manager)
        // ---------------------------
        private void SaveTokenSecure(string token)
        {
            // tenta salvar no Credential Manager; se falhar, salva em arquivo cifrado
            try
            {
                if (SaveTokenToCredentialManager(token))
                {
                    SimpleLogger.Log("Access token salvo no Windows Credential Manager.");
                    return;
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Falha ao salvar no Credential Manager: {ex.Message}");
            }

            // fallback para arquivo cifrado
            SaveTokenToEncryptedFile(token);
            SimpleLogger.Log("Access token salvo em arquivo cifrado (fallback).");
        }

        private string LoadTokenSecure()
        {
            // tenta Credential Manager primeiro
            try
            {
                var token = ReadTokenFromCredentialManager();
                if (!string.IsNullOrEmpty(token))
                {
                    SimpleLogger.Log("Token carregado do Windows Credential Manager.");
                    return token;
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Falha ao ler Credential Manager: {ex.Message}");
            }

            // fallback para arquivo cifrado
            var fileToken = ReadTokenFromEncryptedFile();
            if (!string.IsNullOrEmpty(fileToken))
            {
                SimpleLogger.Log("Token carregado de arquivo cifrado.");
            }
            return fileToken;
        }

        private void SaveTokenToEncryptedFile(string token)
        {
            Directory.CreateDirectory(AppFolder);
            var bytes = Encoding.UTF8.GetBytes(token);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllText(TokenFilePath, Convert.ToBase64String(protectedBytes), Encoding.UTF8);
        }

        private string ReadTokenFromEncryptedFile()
        {
            if (!File.Exists(TokenFilePath)) return null;
            var b64 = File.ReadAllText(TokenFilePath, Encoding.UTF8);
            var protectedBytes = Convert.FromBase64String(b64);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }

        // Windows Credential Manager P/Invoke
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern void CredFree([In] IntPtr buffer);

        private const uint CRED_TYPE_GENERIC = 1;
        private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        private bool SaveTokenToCredentialManager(string token)
        {
            var credential = new CREDENTIAL
            {
                Flags = 0,
                Type = CRED_TYPE_GENERIC,
                TargetName = CredentialTarget,
                Comment = "ShopeeIntegration access token",
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = null,
                UserName = Environment.UserName
            };

            var blob = Encoding.Unicode.GetBytes(token);
            credential.CredentialBlobSize = (uint)blob.Length;
            credential.CredentialBlob = Marshal.AllocCoTaskMem(blob.Length);
            Marshal.Copy(blob, 0, credential.CredentialBlob, blob.Length);

            var written = CredWrite(ref credential, 0);
            Marshal.FreeCoTaskMem(credential.CredentialBlob);

            if (!written)
            {
                var err = Marshal.GetLastWin32Error();
                SimpleLogger.Log($"CredWrite falhou com código: {err}");
            }

            return written;
        }

        private string ReadTokenFromCredentialManager()
        {
            var read = CredRead(CredentialTarget, CRED_TYPE_GENERIC, 0, out var credPtr);
            if (!read)
            {
                var err = Marshal.GetLastWin32Error();
                SimpleLogger.Log($"CredRead falhou com código: {err}");
                return null;
            }

            try
            {
                var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                if (cred.CredentialBlobSize > 0 && cred.CredentialBlob != IntPtr.Zero)
                {
                    var blob = new byte[cred.CredentialBlobSize];
                    Marshal.Copy(cred.CredentialBlob, blob, 0, (int)cred.CredentialBlobSize);
                    var token = Encoding.Unicode.GetString(blob).TrimEnd('\0');
                    return token;
                }
            }
            finally
            {
                CredFree(credPtr);
            }

            return null;
        }
    }
}
