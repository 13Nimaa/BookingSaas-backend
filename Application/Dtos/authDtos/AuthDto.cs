
namespace  BookingSaas_backend.Application.Dtos.AuthDtos;

public sealed record UserDto(
    string UserId,
    string Name,
    string Email
//    string ProfileImage,
//     string Role)
);
public sealed record SignupDto(
    string Name,
     string Email,
   string Password,
   string ConfirmPassword);

public sealed record LoginDto(
    string Email,
   string Password);
    public sealed record AuthResponseDto(
    UserDto User,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
    public sealed record RefreshTokenRequestDto(string RefreshToken);
    public sealed record RefreshTokenResponseDto(string RefreshToken, string accessToken);