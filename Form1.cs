using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

        private const int AuthCallbackPort = 8765;
        private static readonly string AuthRedirectUri = $"http://127.0.0.1:{AuthCallbackPort}/callback";
        private const int AuthCallbackTimeoutSeconds = 300; // 5 minutes

        private static readonly string CredentialsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShopeeIntegration", "credentials.json");

        public Form1()
        {
            InitializeComponent();
            Load += Form1_Load;
            FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                if (!File.Exists(CredentialsFilePath)) return;
                var json = File.ReadAllText(CredentialsFilePath, Encoding.UTF8);
                var config = JsonSerializer.Deserialize<CredentialsConfig>(json);
                if (config == null) return;
                if (!string.IsNullOrEmpty(config.PartnerId)) txtPartnerId.Text = config.PartnerId;
                if (!string.IsNullOrEmpty(config.ShopId)) txtShopId.Text = config.ShopId;
                if (!string.IsNullOrEmpty(config.ApiKey)) txtApiKey.Text = config.ApiKey;
                if (!string.IsNullOrEmpty(config.Environment))
                {
                    var idx = cbEnvironment.Items.IndexOf(config.Environment);
                    if (idx >= 0) cbEnvironment.SelectedIndex = idx;
                }
                if (!string.IsNullOrEmpty(config.AuthCode)) txtAuthCode.Text = config.AuthCode;
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Falha ao carregar credenciais: {ex.Message}");
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                var dir = Path.GetDirectoryName(CredentialsFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var config = new CredentialsConfig
                {
                    PartnerId = txtPartnerId.Text.Trim(),
                    ShopId = txtShopId.Text.Trim(),
                    ApiKey = txtApiKey.Text.Trim(),
                    Environment = cbEnvironment.SelectedItem?.ToString() ?? "Sandbox",
                    AuthCode = txtAuthCode.Text.Trim()
                };
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CredentialsFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Falha ao salvar credenciais: {ex.Message}");
            }
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
                MessageBox.Show("Partner ID inv˜lido.");
                return;
            }

            if (!long.TryParse(txtShopId.Text.Trim(), out var shopId))
            {
                MessageBox.Show("Shop ID inv˜lido.");
                return;
            }

            var apiKey = txtApiKey.Text.Trim();
            var env = cbEnvironment.SelectedItem?.ToString() ?? "Sandbox";

            _service = new ShopeeService(partnerId, shopId, apiKey, env == "Production" ? ShopeeEnvironment.Production : ShopeeEnvironment.Sandbox);
            SetStatus("Servi˜o inicializado.");
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
                SetStatus("Atualizando estoque/pre˜o...");
                var ok = await _service.UpdateStockPriceAsync(row.ItemId, newStock, newPrice);
                SetStatus(ok ? "Atualizado com sucesso." : "Falha na atualiza˜˜o.");
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

        private static async Task<(string code, string errorMessage)> WaitForAuthCallbackAsync()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{AuthCallbackPort}/");
            listener.Start();
            try
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(AuthCallbackTimeoutSeconds));
                var contextTask = listener.GetContextAsync();
                var completed = await Task.WhenAny(contextTask, timeoutTask).ConfigureAwait(false);
                if (completed != contextTask)
                {
                    return (null, "Tempo esgotado. Tente novamente ou cole o code manualmente.");
                }

                var context = await contextTask.ConfigureAwait(false);
                var request = context.Request;
                var query = request.Url?.Query?.TrimStart('?') ?? "";
                var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var idx = part.IndexOf('=');
                    if (idx < 0) continue;
                    var key = Uri.UnescapeDataString(part.Substring(0, idx));
                    var value = Uri.UnescapeDataString(part.Substring(idx + 1));
                    parsed[key] = value;
                }
                parsed.TryGetValue("code", out var code);
                parsed.TryGetValue("error", out var error);
                parsed.TryGetValue("error_description", out var errorDesc);
                if (string.IsNullOrEmpty(errorDesc)) parsed.TryGetValue("message", out errorDesc);

                const string successHtml = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Autoriza??o</title></head><body><p>Autoriza??o conclu?da. Voc? pode fechar esta janela.</p></body></html>";
                const string errorHtml = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Erro</title></head><body><p>Autoriza??o negada ou erro. Feche esta janela e tente novamente.</p></body></html>";

                var response = context.Response;
                response.ContentType = "text/html; charset=utf-8";
                response.ContentEncoding = Encoding.UTF8;

                if (!string.IsNullOrEmpty(error))
                {
                    var body = Encoding.UTF8.GetBytes(errorHtml);
                    response.ContentLength64 = body.Length;
                    await response.OutputStream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
                    response.OutputStream.Close();
                    return (null, string.IsNullOrEmpty(errorDesc) ? "Autoriza??o negada ou erro." : errorDesc);
                }

                if (string.IsNullOrEmpty(code))
                {
                    var body = Encoding.UTF8.GetBytes(errorHtml);
                    response.ContentLength64 = body.Length;
                    await response.OutputStream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
                    response.OutputStream.Close();
                    return (null, "Nenhum code recebido na URL.");
                }

                var successBody = Encoding.UTF8.GetBytes(successHtml);
                response.ContentLength64 = successBody.Length;
                await response.OutputStream.WriteAsync(successBody, 0, successBody.Length).ConfigureAwait(false);
                response.OutputStream.Close();
                return (code, null);
            }
            finally
            {
                try { listener.Stop(); } catch { /* ignore */ }
            }
        }

        private async void BtnGetAuthUrl_Click(object sender, EventArgs e)
        {
            if (_service == null)
            {
                MessageBox.Show("Conecte primeiro (clique em Conectar) para usar a API Key na assinatura.");
                return;
            }

            var redirectUri = AuthRedirectUri;

            var candidates = _service.GetAuthorizationUrlCandidates(redirectUri);
            if (candidates.Count == 0)
            {
                MessageBox.Show("Nenhuma URL candidata gerada (verifique partner_id/api_key).");
                return;
            }

            try
            {
                SetStatus("Aguardando autoriza??o no navegador...");
                try { Clipboard.SetText(candidates[0].Url); } catch { }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = candidates[0].Url, UseShellExecute = true });
                SimpleLogger.Log($"Opened auth URL with redirect {redirectUri}");

                var (code, errorMessage) = await WaitForAuthCallbackAsync().ConfigureAwait(false);

                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        if (!string.IsNullOrEmpty(errorMessage))
                        {
                            SetStatus("Falha na captura do code.");
                            MessageBox.Show(errorMessage);
                            return;
                        }
                        txtAuthCode.Text = code;
                        SetStatus("Code recebido. Obtendo access token...");
                    }));
                }

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    SetStatus("Falha na captura do code.");
                    if (!InvokeRequired) MessageBox.Show(errorMessage);
                    return;
                }

                string codeToUse = null;
                if (InvokeRequired)
                    codeToUse = (string)Invoke(new Func<string>(() => txtAuthCode.Text.Trim()));
                else
                    codeToUse = txtAuthCode.Text.Trim();

                var token = await _service.GetAccessTokenAsync(codeToUse).ConfigureAwait(false);
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        SetStatus("Token obtido e armazenado (seguro).");
                        MessageBox.Show("Access token obtido com sucesso.");
                    }));
                }
                else
                {
                    SetStatus("Token obtido e armazenado (seguro).");
                    MessageBox.Show("Access token obtido com sucesso.");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"BtnGetAuthUrl error: {ex.Message}");
                if (InvokeRequired)
                    Invoke(new Action(() =>
                    {
                        SetStatus("Erro ao obter token.");
                        MessageBox.Show("Erro: " + ex.Message);
                    }));
                else
                {
                    SetStatus("Erro ao obter token.");
                    MessageBox.Show("Erro: " + ex.Message);
                }
            }
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
                MessageBox.Show("Informe o authorization_code obtido ap˜s autoriza˜˜o.");
                return;
            }

            try
            {
                SetStatus("Obtendo access token...");
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

        private async void BtnRefreshToken_Click(object sender, EventArgs e)
        {
            if (_service == null)
            {
                MessageBox.Show("Conecte primeiro (clique em Conectar).");
                return;
            }

            try
            {
                SetStatus("Renovando access token...");
                await _service.RefreshTokenFromStoredAsync();
                SetStatus("Token renovado e salvo.");
                MessageBox.Show("Access token renovado com sucesso.");
            }
            catch (Exception ex)
            {
                SetStatus("Falha ao renovar token.");
                MessageBox.Show("Erro ao renovar token: " + ex.Message);
            }
        }
    }

    public enum ShopeeEnvironment
    {
        Sandbox,
        Production
    }

    public class CredentialsConfig
    {
        public string PartnerId { get; set; } = "";
        public string ShopId { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string Environment { get; set; } = "Sandbox";
        public string AuthCode { get; set; } = "";
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

        // Sandbox V2 (doc 2025-09-15): API calls vs auth use different hosts
        private const string SandboxApiHost = "https://openplatform.sandbox.test-stable.shopee.sg";
        private const string SandboxAuthHost = "https://open.sandbox.test-stable.shopee.com";
        private const string ProductionHost = "https://partner.shopeemobile.com";

        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShopeeIntegration");
        private static readonly string TokenFilePath = Path.Combine(AppFolder, "access_token.dat");
        private static readonly string RefreshTokenFilePath = Path.Combine(AppFolder, "refresh_token.dat");
        private static readonly string TokenExpiresAtFilePath = Path.Combine(AppFolder, "token_expires_at.dat");
        private const string CredentialTarget = "ShopeeIntegration_AccessToken";
        private const string CredentialTargetRefreshToken = "ShopeeIntegration_RefreshToken";

        private string _accessToken = null;
        private string _refreshToken = null;
        private DateTime? _accessTokenExpiresAt = null;

        public ShopeeService(long partnerId, long shopId, string apiKey, ShopeeEnvironment env)
        {
            _partnerId = partnerId;
            _shop_id_check(shopId);
            _shopId = shopId;
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _env = env;
            _http = new HttpClient(new HttpClientHandler { UseCookies = false });

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

            try
            {
                var refresh = LoadRefreshTokenSecure();
                if (!string.IsNullOrEmpty(refresh))
                {
                    _refreshToken = refresh;
                    SimpleLogger.Log("Refresh token carregado do armazenamento seguro.");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Falha ao carregar refresh token: {ex.Message}");
            }

            try
            {
                var expiresAt = LoadTokenExpiresAt();
                if (expiresAt.HasValue)
                    _accessTokenExpiresAt = expiresAt;
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Falha ao carregar expira??o do token: {ex.Message}");
            }
        }

        // quick sanity-check to avoid accidental misassignment (keeps code robust)
        private void _shop_id_check(long shopId)
        {
            // no-op placeholder in case we want validation later
        }

        /// <summary>Host for API calls (token/get, product, orders, etc.). Sandbox V2 uses openplatform.sandbox.test-stable.shopee.sg.</summary>
        private string ApiBaseHost => _env == ShopeeEnvironment.Production ? ProductionHost : SandboxApiHost;

        private static string MaskString(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
        }

        // Authorization
        public string GetAuthorizationUrl(string redirectUri)
        {
            var (url, _, _) = BuildAuthorizationUrlWithSign(redirectUri, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            SimpleLogger.Log($"Generated auth URL (masked): {url.Replace(_apiKey, "[REDACTED]")}");
            return url;
        }

        // Sandbox V2: open.sandbox.test-stable.shopee.com/auth with auth_type, redirect_uri, response_type (no sign).
        // Production: partner.shopeemobile.com + auth_partner with sign.
        public List<AuthUrlCandidate> GetAuthorizationUrlCandidates(string redirectUri)
        {
            if (_env == ShopeeEnvironment.Sandbox)
            {
                var url = $"{SandboxAuthHost}/auth?auth_type=seller&partner_id={_partnerId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code";
                SimpleLogger.Log($"AuthCandidate | Sandbox V2 (no sign) | Url={url}");
                return new List<AuthUrlCandidate> { new AuthUrlCandidate(url, "(Sandbox V2: no sign)", "") };
            }

            var host = ProductionHost;
            var path = "/api/v2/shop/auth_partner";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signInput = $"{_partnerId}{path}{timestamp}";
            var sign = ShopeeCrypto.ComputeHmacSha256PartnerKey(_apiKey, signInput);
            var urlProd = $"{host}{path}?partner_id={_partnerId}&timestamp={timestamp}&sign={sign}&redirect={Uri.EscapeDataString(redirectUri)}";
            SimpleLogger.Log($"AuthCandidate | partner_id+path+timestamp | SignInputMasked='{MaskString(signInput,200)}' | Sign={sign} | Url={urlProd.Replace(_apiKey, "[REDACTED]")}");
            return new List<AuthUrlCandidate> { new AuthUrlCandidate(urlProd, MaskString(signInput, 200), sign) };
        }

        // Sandbox V2: URL without sign. Production: auth_partner with sign (timestamp for diagnostics).
        public (string Url, string SignInput, string Sign) BuildAuthorizationUrlWithSign(string redirectUri, long timestamp)
        {
            if (_env == ShopeeEnvironment.Sandbox)
            {
                var url = $"{SandboxAuthHost}/auth?auth_type=seller&partner_id={_partnerId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code";
                SimpleLogger.Log($"BuildAuthUrl | Sandbox V2 (no sign) | Url={url}");
                return (url, "(Sandbox V2: no sign)", "");
            }

            var host = ProductionHost;
            var path = "/api/v2/shop/auth_partner";
            var ts = timestamp.ToString();
            var signInput = $"{_partnerId}{path}{ts}";
            var sign = ShopeeCrypto.ComputeHmacSha256PartnerKey(_apiKey, signInput);
            var urlProd = $"{host}{path}?partner_id={_partnerId}&timestamp={ts}&sign={sign}&redirect={Uri.EscapeDataString(redirectUri)}";
            SimpleLogger.Log($"BuildAuthUrl | SignInputMasked='{MaskString(signInput,200)}' | Sign={sign} | Url={urlProd.Replace(_apiKey, "[REDACTED]")}");
            return (urlProd, signInput, sign);
        }

        // Exchange authorization code for access token (V2: body uses "code", sign = partner_id + path + timestamp + shop_id)
        public async Task<string> GetAccessTokenAsync(string authCode)
        {
            var path = "/api/v2/auth/token/get";
            var body = new Dictionary<string, object>
            {
                ["code"] = authCode,
                ["partner_id"] = _partnerId,
                ["shop_id"] = _shopId
            };

            var resp = await SendPostAsync(path, body, includeShopId: true, includeAuthToken: false);
            if (resp.TryGetProperty("access_token", out var tok))
            {
                _accessToken = tok.GetString();
                if (resp.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds))
                    _accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(seconds - 30);
                if (resp.TryGetProperty("refresh_token", out var refTok))
                    _refreshToken = refTok.GetString();

                try
                {
                    SaveTokenSecure(_accessToken);
                    SimpleLogger.Log("Access token salvo de forma segura.");
                }
                catch (Exception ex)
                {
                    SimpleLogger.Log($"Falha ao salvar token seguro: {ex.Message}");
                }
                try
                {
                    if (!string.IsNullOrEmpty(_refreshToken))
                    {
                        SaveRefreshTokenSecure(_refreshToken);
                        SimpleLogger.Log("Refresh token salvo de forma segura.");
                    }
                }
                catch (Exception ex)
                {
                    SimpleLogger.Log($"Falha ao salvar refresh token: {ex.Message}");
                }
                try
                {
                    if (_accessTokenExpiresAt.HasValue)
                        SaveTokenExpiresAt(_accessTokenExpiresAt);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Log($"Falha ao salvar expira??o do token: {ex.Message}");
                }

                return _accessToken;
            }

            var (code, msg) = ExtractError(resp);
            throw new ShopeeApiException("Falha ao obter access_token", code, msg);
        }

        /// <summary>Refresh access token using refresh_token (V2: path /api/v2/auth/access_token/get, sign = partner_id + path + timestamp + shop_id).</summary>
        public async Task<string> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Refresh token is required.", nameof(refreshToken));

            var path = "/api/v2/auth/access_token/get";
            var body = new Dictionary<string, object>
            {
                ["shop_id"] = _shopId,
                ["refresh_token"] = refreshToken,
                ["partner_id"] = _partnerId
            };

            var resp = await SendPostAsync(path, body, includeShopId: true, includeAuthToken: false);
            if (resp.TryGetProperty("access_token", out var tok))
            {
                _accessToken = tok.GetString();
                if (resp.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds))
                    _accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(seconds - 30);
                if (resp.TryGetProperty("refresh_token", out var refTok))
                    _refreshToken = refTok.GetString();

                try
                {
                    SaveTokenSecure(_accessToken);
                    SimpleLogger.Log("Access token atualizado (refresh) e salvo de forma segura.");
                }
                catch (Exception ex)
                {
                    SimpleLogger.Log($"Falha ao salvar token seguro ap?s refresh: {ex.Message}");
                }
                try
                {
                    if (!string.IsNullOrEmpty(_refreshToken))
                    {
                        SaveRefreshTokenSecure(_refreshToken);
                        SimpleLogger.Log("Refresh token atualizado e salvo.");
                    }
                }
                catch (Exception ex)
                {
                    SimpleLogger.Log($"Falha ao salvar refresh token ap?s refresh: {ex.Message}");
                }
                try
                {
                    if (_accessTokenExpiresAt.HasValue)
                        SaveTokenExpiresAt(_accessTokenExpiresAt);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Log($"Falha ao salvar expira??o ap?s refresh: {ex.Message}");
                }

                return _accessToken;
            }

            var (code, msg) = ExtractError(resp);
            throw new ShopeeApiException("Falha ao renovar access_token (refresh)", code, msg);
        }

        /// <summary>Refresh access token using the stored refresh_token.</summary>
        public async Task<string> RefreshTokenFromStoredAsync()
        {
            var refresh = _refreshToken;
            if (string.IsNullOrWhiteSpace(refresh))
                refresh = LoadRefreshTokenSecure();
            if (string.IsNullOrWhiteSpace(refresh))
                throw new ShopeeApiException("Refresh token n?o encontrado. Fa?a login novamente (Gerar Auth URL) para obter um novo token.", -1, null);
            return await RefreshTokenAsync(refresh).ConfigureAwait(false);
        }

        /// <summary>Ensures access token is valid; if expired, refreshes it using stored refresh_token.</summary>
        private async Task EnsureValidAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(_accessToken)) return;
            if (_accessTokenExpiresAt.HasValue && DateTime.UtcNow >= _accessTokenExpiresAt.Value)
            {
                try
                {
                    await RefreshTokenFromStoredAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Log($"EnsureValidAccessToken: falha ao renovar token: {ex.Message}");
                    throw;
                }
            }
        }

        public async Task<List<ProductModel>> GetProductsAsync()
        {
            var result = new List<ProductModel>();
            var offset = 0;
            const int pageSize = 100;
            var path = "/api/v2/product/get_item_list";

            while (true)
            {
                var queryParams = new Dictionary<string, string>
                {
                    ["offset"] = offset.ToString(),
                    ["page_size"] = pageSize.ToString(),
                    ["item_status"] = "NORMAL"
                };

                var resp = await SendGetAsync(path, queryParams);
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
                offset += pageSize;
            }

            return result;
        }

        public async Task<bool> UpdateStockPriceAsync(long itemId, int stock, decimal price)
        {
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

        public async Task<List<OrderModel>> GetOrdersAsync()
        {
            var orders = new List<OrderModel>();
            var path = "/api/v2/order/get_order_list";
            var timeTo = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeFrom = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeSeconds();

            var queryParams = new Dictionary<string, string>
            {
                ["time_range_field"] = "create_time",
                ["time_to"] = timeTo.ToString(),
                ["time_from"] = timeFrom.ToString(),
                ["page_size"] = "100"
            };

            var resp = await SendGetAsync(path, queryParams);
            if (!resp.TryGetProperty("response", out var responseObj))
                throw ConstructExceptionFromResponse(resp, "GetOrdersAsync");

            if (!responseObj.TryGetProperty("order_list", out var arr) && !responseObj.TryGetProperty("orders", out arr))
                return orders;

            var arrItems = arr.EnumerateArray().ToArray();
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
                else if (!string.IsNullOrEmpty(orderId))
                {
                    var detailPath = "/api/v2/order/get_order_detail";
                    var detailParams = new Dictionary<string, string> { ["order_sn_list"] = orderId };
                    var detailResp = await SendGetAsync(detailPath, detailParams);
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

            return orders;
        }

        private async Task<JsonElement> SendPostAsync(string path, object bodyObj, bool includeShopId = false, bool includeAuthToken = true)
        {
            if (includeAuthToken)
                await EnsureValidAccessTokenAsync().ConfigureAwait(false);
            var urlPath = path.StartsWith("/") ? path : "/" + path;
            var host = ApiBaseHost;
            var url = host + urlPath;

            var bodyJson = JsonSerializer.Serialize(bodyObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            string signInput;
            bool isAuthTokenGet = urlPath.IndexOf("/api/v2/auth/token/get", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isAuthAccessTokenGet = urlPath.IndexOf("/api/v2/auth/access_token/get", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isAuthTokenGet || isAuthAccessTokenGet)
            {
                // Auth token/get and access_token/get (refresh): sign = partner_id + path + timestamp + shop_id (V2, no body in sign)
                signInput = $"{_partnerId}{urlPath}{timestamp}{_shopId}";
            }
            else
            {
                // Shop/other POST: partner_id + shop_id + path + timestamp + body
                var signInputBuilder = new StringBuilder();
                signInputBuilder.Append(_partnerId.ToString());
                if (includeShopId) signInputBuilder.Append(_shopId.ToString());
                signInputBuilder.Append(urlPath);
                signInputBuilder.Append(timestamp);
                signInputBuilder.Append(bodyJson);
                signInput = signInputBuilder.ToString();
            }

            // Auth endpoints: use partner key as raw UTF-8 (Sandbox/API expect literal key string, not hex-decoded "shpk" payload)
            string sign = (isAuthTokenGet || isAuthAccessTokenGet)
                ? ShopeeCrypto.ComputeHmacSha256(_apiKey, signInput)
                : ShopeeCrypto.ComputeHmacSha256PartnerKey(_apiKey, signInput);

            var qs = new List<string>
            {
                $"partner_id={_partnerId}",
                $"timestamp={timestamp}",
                $"sign={Uri.EscapeDataString(sign)}"
            };
            if (includeShopId)
                qs.Add($"shop_id={_shopId}");

            var reqUrl = url + "?" + string.Join("&", qs);

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

            if (!resp.IsSuccessStatusCode)
            {
                var (errCode, errMsg) = ExtractError(root);
                var msg = $"HTTP {(int)resp.StatusCode} ao chamar Shopee: {errMsg}";
                SimpleLogger.Log(msg);
                throw new ShopeeApiException(msg, errCode, errMsg);
            }

            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Number && err.GetInt32() != 0)
            {
                var (errCode, errMsg) = ExtractError(root);
                SimpleLogger.Log($"Erro Shopee API: code={errCode} message={errMsg}");
                throw new ShopeeApiException($"Erro API Shopee: {errMsg}", errCode, errMsg);
            }

            return root;
        }

        /// <summary>GET request for V2 APIs that use sign = partner_id + path + timestamp + access_token + shop_id (e.g. get_item_list).</summary>
        private async Task<JsonElement> SendGetAsync(string path, IReadOnlyDictionary<string, string> queryParams)
        {
            await EnsureValidAccessTokenAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(_accessToken))
                throw new ShopeeApiException("Access token is required for this API call.");

            var urlPath = path.StartsWith("/") ? path : "/" + path;
            var host = ApiBaseHost;
            var url = host + urlPath;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var signInput = $"{_partnerId}{urlPath}{timestamp}{_accessToken}{_shopId}";
            // Use raw UTF-8 key (same as auth endpoints) so Sandbox accepts the sign; Production may accept either
            var sign = ShopeeCrypto.ComputeHmacSha256(_apiKey, signInput);

            var qs = new List<string>
            {
                $"partner_id={_partnerId}",
                $"timestamp={timestamp}",
                $"sign={Uri.EscapeDataString(sign)}",
                $"shop_id={_shopId}",
                $"access_token={Uri.EscapeDataString(_accessToken)}"
            };
            if (queryParams != null)
            {
                foreach (var kv in queryParams)
                    qs.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value ?? "")}");
            }

            var reqUrl = url + "?" + string.Join("&", qs);
            SimpleLogger.LogRequest(reqUrl, "");

            using var req = new HttpRequestMessage(HttpMethod.Get, reqUrl);
            req.Headers.Add("Accept", "application/json");

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

            if (!resp.IsSuccessStatusCode)
            {
                var (errCode, errMsg) = ExtractError(root);
                var msg = $"HTTP {(int)resp.StatusCode} ao chamar Shopee: {errMsg}";
                SimpleLogger.Log(msg);
                throw new ShopeeApiException(msg, errCode, errMsg);
            }

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

        // Token persistence
        private void SaveTokenSecure(string token)
        {
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

            SaveTokenToEncryptedFile(token);
            SimpleLogger.Log("Access token salvo em arquivo cifrado (fallback).");
        }

        private string LoadTokenSecure()
        {
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

        private void SaveRefreshTokenSecure(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken)) return;
            try
            {
                if (SaveRefreshTokenToCredentialManager(refreshToken))
                {
                    SimpleLogger.Log("Refresh token salvo no Windows Credential Manager.");
                    return;
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Falha ao salvar refresh token no Credential Manager: {ex.Message}");
            }
            SaveRefreshTokenToEncryptedFile(refreshToken);
            SimpleLogger.Log("Refresh token salvo em arquivo cifrado (fallback).");
        }

        private string LoadRefreshTokenSecure()
        {
            try
            {
                var token = ReadRefreshTokenFromCredentialManager();
                if (!string.IsNullOrEmpty(token))
                    return token;
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"Falha ao ler refresh token do Credential Manager: {ex.Message}");
            }
            return ReadRefreshTokenFromEncryptedFile();
        }

        private void SaveRefreshTokenToEncryptedFile(string token)
        {
            Directory.CreateDirectory(AppFolder);
            var bytes = Encoding.UTF8.GetBytes(token);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllText(RefreshTokenFilePath, Convert.ToBase64String(protectedBytes), Encoding.UTF8);
        }

        private string ReadRefreshTokenFromEncryptedFile()
        {
            if (!File.Exists(RefreshTokenFilePath)) return null;
            var b64 = File.ReadAllText(RefreshTokenFilePath, Encoding.UTF8);
            var protectedBytes = Convert.FromBase64String(b64);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }

        private bool SaveRefreshTokenToCredentialManager(string token)
        {
            var credential = new CREDENTIAL
            {
                Flags = 0,
                Type = CRED_TYPE_GENERIC,
                TargetName = CredentialTargetRefreshToken,
                Comment = "ShopeeIntegration refresh token",
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
                SimpleLogger.Log($"CredWrite (refresh) falhou com c?digo: {err}");
            }
            return written;
        }

        private string ReadRefreshTokenFromCredentialManager()
        {
            var read = CredRead(CredentialTargetRefreshToken, CRED_TYPE_GENERIC, 0, out var credPtr);
            if (!read) return null;
            try
            {
                var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                if (cred.CredentialBlobSize > 0 && cred.CredentialBlob != IntPtr.Zero)
                {
                    var blob = new byte[cred.CredentialBlobSize];
                    Marshal.Copy(cred.CredentialBlob, blob, 0, (int)cred.CredentialBlobSize);
                    return Encoding.Unicode.GetString(blob).TrimEnd('\0');
                }
            }
            finally
            {
                CredFree(credPtr);
            }
            return null;
        }

        private void SaveTokenExpiresAt(DateTime? expiresAtUtc)
        {
            if (!expiresAtUtc.HasValue) return;
            Directory.CreateDirectory(AppFolder);
            File.WriteAllText(TokenExpiresAtFilePath, expiresAtUtc.Value.ToUniversalTime().Ticks.ToString(), Encoding.UTF8);
        }

        private DateTime? LoadTokenExpiresAt()
        {
            if (!File.Exists(TokenExpiresAtFilePath)) return null;
            var s = File.ReadAllText(TokenExpiresAtFilePath, Encoding.UTF8).Trim();
            if (string.IsNullOrEmpty(s)) return null;
            if (long.TryParse(s, out var ticks))
                return new DateTime(ticks, DateTimeKind.Utc);
            return null;
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
                SimpleLogger.Log($"CredWrite falhou com c˜digo: {err}");
            }

            return written;
        }

        private string ReadTokenFromCredentialManager()
        {
            var read = CredRead(CredentialTarget, CRED_TYPE_GENERIC, 0, out var credPtr);
            if (!read)
            {
                var err = Marshal.GetLastWin32Error();
                SimpleLogger.Log($"CredRead falhou com c˜digo: {err}");
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

