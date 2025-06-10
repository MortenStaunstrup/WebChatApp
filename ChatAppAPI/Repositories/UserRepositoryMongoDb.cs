using ChatAppAPI.Repositories.Interfaces;
using Core;

namespace ChatAppAPI.Repositories;

public class UserRepositoryMongoDb : IUserRepository
{
    public Task<User?> TryLogin(string emailOrPhone, string password)
    {
        throw new NotImplementedException();
    }
}