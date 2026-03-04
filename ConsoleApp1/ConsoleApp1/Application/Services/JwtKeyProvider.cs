using System.Text;
using ConsoleApp1.Application.Contracts.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ConsoleApp1.Application.Services;

public class JwtKeyProvider(IOptions<JwtOptions> optionsAccessor)
{
    private readonly JwtOptions _options = optionsAccessor.Value;

    public SigningCredentials GetCurrentSigningCredentials(out string keyId)
    {
        var keys = GetKeys();
        var selected = keys.FirstOrDefault(key => key.KeyId == _options.CurrentKeyId) ?? keys.First();
        keyId = selected.KeyId;
        return new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(selected.Secret)), SecurityAlgorithms.HmacSha256);
    }

    public IEnumerable<SecurityKey> GetValidationKeys()
    {
        return GetKeys().Select(key =>
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key.Secret));
            securityKey.KeyId = key.KeyId;
            return (SecurityKey)securityKey;
        });
    }

    private IReadOnlyList<JwtSigningKeyOptions> GetKeys()
    {
        if (_options.Keys.Count > 0)
        {
            return _options.Keys.Where(key => !string.IsNullOrWhiteSpace(key.KeyId) && !string.IsNullOrWhiteSpace(key.Secret)).ToList();
        }

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            return [];
        }

        return [new JwtSigningKeyOptions { KeyId = _options.CurrentKeyId, Secret = _options.SigningKey }];
    }
}