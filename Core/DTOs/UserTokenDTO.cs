namespace Core.DTOs;

public class UserTokenDTO
{
    public User User { get; set; }
    public string Token { get; set; }
    public string RefreshToken { get; set; }
}