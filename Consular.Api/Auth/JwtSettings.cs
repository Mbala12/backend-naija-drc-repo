namespace Consular.Api.Auth;

public class JwtSettings
{
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "consular-api";
    public int ExpiryMinutes { get; set; } = 480;
}
