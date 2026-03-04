namespace ConsoleApp1.Application.Contracts.Auth;

public sealed record TokenResponse(
	string AccessToken,
	string TokenType,
	int ExpiresIn,
	string RefreshToken,
	int RefreshTokenExpiresIn);