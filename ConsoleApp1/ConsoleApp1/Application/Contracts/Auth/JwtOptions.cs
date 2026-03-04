namespace ConsoleApp1.Application.Contracts.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public string CurrentKeyId { get; set; } = "default";
    public List<JwtSigningKeyOptions> Keys { get; set; } = [];
    public int AccessTokenMinutes { get; set; } = 60;
}

public sealed class JwtSigningKeyOptions
{
    public string KeyId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}