namespace ShopeeIntegration
{
    // Tipo simples e público para representar uma URL candidata de autorização
    public sealed record AuthUrlCandidate(string Url, string SignInputMasked, string Sign);
}