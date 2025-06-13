using Core;

namespace ChatAppAPI.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> TryLogin(string emailOrPhone, string password);
    Task<User?> CreateUser(User user);
}