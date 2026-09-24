
namespace  BookingSaas_backend.Application.Dtos.AuthDtos;

public sealed record UserDto(
    string UserId,
    string Name,
    string PhoneNumber
//    string ProfileImage,
//     string Role)
);
public sealed record SignupDto(
    string Name,
    string PhoneNumber,
   string Password,
   string ConfirmPassword);

public sealed record LoginDto(
    string PhoneNumber,
   string Password);
    public sealed record AuthResponseDto(
    UserDto User,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
    public sealed record RefreshTokenRequestDto(string RefreshToken);
    public sealed record RefreshTokenResponseDto(string RefreshToken, string accessToken);