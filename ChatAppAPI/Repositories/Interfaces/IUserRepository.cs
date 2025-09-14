using Core;

namespace ChatAppAPI.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> TryLogin(string emailOrPhone, string password);
    Task<User?> CreateUser(User user);
    Task<List<User>?> GetQueriedUsers(string query, int limit, int page);
    Task<User?> GetUserByUserIdAsync(int userId);
    Task<List<User>?> GetUsersForConversationByUserIdAsync(List<int> userIds);
    Task<int> UpdateUser(ProfileUser user);
}