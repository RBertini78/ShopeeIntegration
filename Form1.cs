using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
            var redirectUri = "https://your-redirect-uri.example.com/"; // substituir pelo seu redirect URI registrado
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
                    SetStatus("Token obtido e armazenado (in-memory).");
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

    public class ShopeeService
    {
        private readonly long _partnerId;
        private readonly long _shopId;
        private readonly string _apiKey;
        private readonly ShopeeEnvironment _env;
        private readonly HttpClient _http;

        // Base hosts (ajuste se Shopee alterar)
        private const string SandboxHost = "https://partner.test.shopeemobile.com";
        private const string ProductionHost = "https://partner.shopeemobile.com";

        // Exposed for creating authorization URL
        public static string GetAuthorizationUrl(long partnerId, string redirectUri, bool production = false)
        {
            var host = production ? ProductionHost : SandboxHost;
            // Path e parâmetros podem variar conforme a documentação — ajuste se necessário.
            // Exemplo common: /api/v2/shop/auth_partner?partner_id={partnerId}&redirect={redirectUri}
            var path = "/api/v2/shop/auth_partner";
            var url = $"{host}{path}?partner_id={partnerId}&redirect={Uri.EscapeDataString(redirectUri)}";
            return url;
        }

        // In-memory token (para exemplo). Persista em storage seguro em produção.
        private string _accessToken = null;
        private DateTime? _accessTokenExpiresAt = null;

        public ShopeeService(long partnerId, long shopId, string apiKey, ShopeeEnvironment env)
        {
            _partnerId = partnerId;
            _shopId = shopId;
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _env = env;
            _http = new HttpClient(new HttpClientHandler { UseCookies = false });
        }

        private string BaseHost => _env == ShopeeEnvironment.Production ? ProductionHost : SandboxHost;

        /// <summary>
        /// Troca o authorization_code pelo access_token (persistir token).
        /// Atenção: ajuste o path e o body conforme a documentação oficial Shopee V2.
        /// </summary>
        public async Task<string> GetAccessTokenAsync(string authCode)
        {
            // Endpoint (ajustar se doc indicar outro)
            var path = "/api/v2/auth/token/get";
            var body = new
            {
                authorization_code = authCode,
                partner_id = _partnerId,
                // shop_id pode ser necessário em alguns fluxos; incluir se requerido
                shop_id = _shopId
            };

            var resp = await SendPostAsync(path, body, includeShopId: true, includeAuthToken: false);
            // Exemplo de parsing: verificar campos retornados (ajuste conforme resposta real)
            if (resp.TryGetProperty("access_token", out var tok))
            {
                _accessToken = tok.GetString();
                if (resp.TryGetProperty("expires_in", out var exp))
                {
                    var seconds = exp.GetInt32();
                    _accessTokenExpiresAt = DateTime.UtcNow.AddSeconds(seconds - 30);
                }
                return _accessToken;
            }

            return null;
        }

        /// <summary>
        /// Lista produtos do shop; paginação simples.
        /// Ajuste path/body conforme documento da Shopee V2.
        /// </summary>
        public async Task<List<ProductModel>> GetProductsAsync()
        {
            var result = new List<ProductModel>();
            var page = 1;
            var pageSize = 50;

            while (true)
            {
                var path = "/api/v2/product/get_item_list"; // pode ser diferente; ajuste conforme doc
                var body = new
                {
                    partner_id = _partnerId,
                    shop_id = _shopId,
                    page_size = pageSize,
                    page_no = page
                };

                var resp = await SendPostAsync(path, body, includeShopId: true);
                // Espera: { "error":0, "message":"success", "response": { "items": [ ... ] } }
                if (!resp.TryGetProperty("response", out var responseObj))
                    break;

                if (!responseObj.TryGetProperty("items", out var items))
                    break;

                var itemsArray = items.EnumerateArray().ToArray();
                if (itemsArray.Length == 0) break;

                foreach (var it in itemsArray)
                {
                    // Ajuste mapeamento conforme schema real
                    var itemId = it.TryGetProperty("item_id", out var iid) ? iid.GetInt64() : (it.TryGetProperty("item_id_long", out var iid2) ? iid2.GetInt64() : 0);
                    var name = it.TryGetProperty("name", out var nm) ? nm.GetString() : "";
                    var stock = it.TryGetProperty("stock", out var st) ? st.GetInt32() : 0;
                    var price = 0m;
                    if (it.TryGetProperty("price", out var pr))
                    {
                        if (pr.ValueKind == JsonValueKind.Number && pr.TryGetDecimal(out var dec))
                            price = dec;
                        else if (pr.ValueKind == JsonValueKind.String && decimal.TryParse(pr.GetString(), out var dec2))
                            price = dec2;
                    }

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

        /// <summary>
        /// Atualiza estoque e preço de um item. Pode chamar endpoints separados conforme necessário.
        /// </summary>
        public async Task<bool> UpdateStockPriceAsync(long itemId, int stock, decimal price)
        {
            // 1) Atualizar estoque
            var stockPath = "/api/v2/product/update_stock"; // ajuste
            var stockBody = new
            {
                partner_id = _partnerId,
                shop_id = _shopId,
                item_id = itemId,
                stock = stock
            };

            var stockResp = await SendPostAsync(stockPath, stockBody, includeShopId: true);
            var stockOk = !stockResp.TryGetProperty("error", out var errStock) || errStock.GetInt32() == 0;

            // 2) Atualizar preço
            var pricePath = "/api/v2/product/update_price"; // ajuste
            var priceBody = new
            {
                partner_id = _partnerId,
                shop_id = _shopId,
                item_id = itemId,
                price = price
            };

            var priceResp = await SendPostAsync(pricePath, priceBody, includeShopId: true);
            var priceOk = !priceResp.TryGetProperty("error", out var errPrice) || errPrice.GetInt32() == 0;

            return stockOk && priceOk;
        }

        /// <summary>
        /// Lista pedidos (com detalhes básicos). Ajuste path/body conforme doc.
        /// </summary>
        public async Task<List<OrderModel>> GetOrdersAsync()
        {
            var orders = new List<OrderModel>();
            var page = 1;
            var pageSize = 50;

            while (true)
            {
                var path = "/api/v2/orders/get_order_list"; // ajuste
                var body = new
                {
                    partner_id = _partnerId,
                    shop_id = _shopId,
                    page_size = pageSize,
                    page_no = page
                };

                var resp = await SendPostAsync(path, body, includeShopId: true);
                if (!resp.TryGetProperty("response", out var responseObj)) break;
                if (!responseObj.TryGetProperty("orders", out var arr)) break;

                var arrItems = arr.EnumerateArray().ToArray();
                if (arrItems.Length == 0) break;

                foreach (var o in arrItems)
                {
                    var orderId = o.TryGetProperty("order_sn", out var osn) ? osn.GetString() : (o.TryGetProperty("order_id", out var oid) ? oid.GetString() : "");
                    var buyer = o.TryGetProperty("buyer_username", out var bn) ? bn.GetString() : "";
                    var total = o.TryGetProperty("total_amount", out var ta) ? (ta.TryGetDecimal(out var d) ? d : 0m) : 0m;

                    var order = new OrderModel
                    {
                        OrderId = orderId,
                        BuyerName = buyer,
                        TotalPrice = total
                    };

                    // Se não houver itens, buscar detalhes
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
                        // Buscar detalhes do pedido, se necessário
                        var detailPath = "/api/v2/orders/get_order_detail"; // ajuste
                        var detailBody = new
                        {
                            partner_id = _partnerId,
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

        /// <summary>
        /// Helper genérico para POST à Shopee V2.
        /// Gera query string: partner_id, timestamp, sign, shop_id (se includeShopId).
        /// O algoritmo de assinatura pode variar entre versões da API — ajuste o método ComputeSignature conforme necessário.
        /// </summary>
        private async Task<JsonElement> SendPostAsync(string path, object bodyObj, bool includeShopId = false, bool includeAuthToken = true)
        {
            var urlPath = path.StartsWith("/") ? path : "/" + path;
            var host = BaseHost;
            var url = host + urlPath;

            var bodyJson = JsonSerializer.Serialize(bodyObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Assinatura HMAC-SHA256: ajuste a concatenação se doc indicar formato diferente
            var signInput = $"{_partnerId}{(includeShopId ? _shopId.ToString() : "")}{timestamp}{urlPath}{bodyJson}";
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

            using var req = new HttpRequestMessage(HttpMethod.Post, reqUrl);
            req.Headers.Add("Accept", "application/json");
            if (includeAuthToken && !string.IsNullOrEmpty(_accessToken))
                req.Headers.Add("Authorization", _accessToken); // ajustar se Shopee exigir "Authorization: Bearer {token}"

            req.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req);
            var respText = await resp.Content.ReadAsStringAsync();

            try
            {
                var doc = JsonDocument.Parse(respText);
                return doc.RootElement.Clone();
            }
            catch (Exception)
            {
                // Retornar objeto JSON minimal com erro se não for JSON
                var fake = JsonSerializer.SerializeToElement(new { error = -1, message = "invalid json response", raw = respText });
                return fake;
            }
        }

        private static string ComputeHmacSha256(string key, string data)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var bytes = Encoding.UTF8.GetBytes(data);
            var hash = hmac.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
