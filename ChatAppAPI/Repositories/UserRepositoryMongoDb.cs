using System.Text.RegularExpressions;
using ChatAppAPI.Repositories.Interfaces;
using ChatAppAPI.Token;
using Core;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ChatAppAPI.Repositories;

public class UserRepositoryMongoDb : IUserRepository
{
    private readonly IMongoCollection<User> _userCollection;
    private readonly IMongoCollection<RefreshToken> _refreshTokenCollection;
    private readonly TokenProvider _tokenProvider;
    private readonly int _refreshTokenExpirationTimeInDays;
    private readonly ILogger<UserRepositoryMongoDb> _logger;

    public UserRepositoryMongoDb(
        TokenProvider tokenProvider,
        IMongoDatabase mongoDatabase,
        IConfiguration configuration,
        ILogger<UserRepositoryMongoDb> logger)
    {
        _tokenProvider = tokenProvider;
        _refreshTokenExpirationTimeInDays = configuration.GetValue<int>("Jwt:RefreshTokenExpirationInDays");
        _logger = logger;

        _userCollection = mongoDatabase.GetCollection<User>("Users");
        _refreshTokenCollection = mongoDatabase.GetCollection<RefreshToken>("RefreshTokens");
    }

    // Called from LoginWithRefreshToken endpoint
    public async Task<string> CheckRefreshToken(string refreshToken)
    {
        _logger.LogDebug("CheckRefreshToken called");

        try
        {
            var filter = Builders<RefreshToken>.Filter.Eq("Token", refreshToken);
            var refreshTokenResult = await _refreshTokenCollection.Find(filter).FirstOrDefaultAsync();

            if (refreshTokenResult == null)
            {
                _logger.LogWarning("CheckRefreshToken failed, refresh token was not found");
                return "";
            }

            if (refreshTokenResult.ExpiresOnUTC < DateTime.UtcNow)
            {
                _logger.LogWarning(
                    "CheckRefreshToken failed, refresh token for user {UserId} has expired",
                    refreshTokenResult.UserId);
                return "";
            }

            _logger.LogDebug(
                "CheckRefreshToken succeeded for user {UserId}",
                refreshTokenResult.UserId);

            return refreshTokenResult.Token;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CheckRefreshToken failed due to an exception");
            return "";
        }
    }

    public async Task<string> CreateRefreshToken(int userId)
    {
        _logger.LogDebug(
            "CreateRefreshToken called for user {UserId}",
            userId);

        var userToken = await RefreshTokenForUserExists(userId);

        // Refresh existing token 
        if (userToken != null)
        {
            try
            {
                var token = _tokenProvider.GenerateRefreshToken();
                var savedToken = new RefreshToken()
                {
                    Token = token,
                    Id = userToken.Id,
                    UserId = userId,
                    ExpiresOnUTC = DateTime.UtcNow.AddDays(_refreshTokenExpirationTimeInDays)
                };

                await _refreshTokenCollection.ReplaceOneAsync(r => r.Id == savedToken.Id, savedToken);

                _logger.LogInformation(
                    "CreateRefreshToken refreshed existing token for user {UserId}",
                    userId);

                return token;
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    "CreateRefreshToken failed while refreshing token for user {UserId}",
                    userId);
                return "";
            }
        }

        // Create new refresh token
        try
        {
            var newId = await GetMaxRefreshTokenId() + 1;
            var token = _tokenProvider.GenerateRefreshToken();

            var savedToken = new RefreshToken()
            {
                Token = token,
                Id = newId,
                UserId = userId,
                ExpiresOnUTC = DateTime.UtcNow.AddDays(_refreshTokenExpirationTimeInDays)
            };

            await _refreshTokenCollection.InsertOneAsync(savedToken);

            _logger.LogInformation(
                "CreateRefreshToken created new refresh token for user {UserId}",
                userId);

            return token;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "CreateRefreshToken failed while creating token for user {UserId}",
                userId);
            return "";
        }
    }

    // Used by CreateRefreshToken function
    private async Task<RefreshToken?> RefreshTokenForUserExists(int userId)
    {
        _logger.LogDebug(
            "RefreshTokenForUserExists called for user {UserId}",
            userId);

        try
        {
            var filter = Builders<RefreshToken>.Filter.Eq("UserId", userId);
            var refreshToken = await _refreshTokenCollection.Find(filter).FirstOrDefaultAsync();

            if (refreshToken == null)
            {
                _logger.LogDebug(
                    "RefreshTokenForUserExists found no token for user {UserId}",
                    userId);
            }

            return refreshToken;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "RefreshTokenForUserExists failed for user {UserId}",
                userId);
            return null;
        }
    }

    private async Task<User?> GetUserByPhoneOrEmail(string phoneOrEmail)
    {
        _logger.LogDebug("GetUserByPhoneOrEmail called");

        try
        {
            var filter = Builders<User>.Filter.Eq("Email", phoneOrEmail);
            var user = await _userCollection.Find(filter).FirstOrDefaultAsync();
            if (user != null)
            {
                _logger.LogDebug(
                    "GetUserByPhoneOrEmail found user {UserId} by email",
                    user.UserId);
                return user;
            }

            var filter2 = Builders<User>.Filter.Eq("PhoneNumber", phoneOrEmail);
            var user2 = await _userCollection.Find(filter2).FirstOrDefaultAsync();
            if (user2 != null)
            {
                _logger.LogDebug(
                    "GetUserByPhoneOrEmail found user {UserId} by phone number",
                    user2.UserId);
                return user2;
            }

            _logger.LogDebug("GetUserByPhoneOrEmail found no matching user");
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetUserByPhoneOrEmail failed");
            return null;
        }
    }
    
    public async Task<User?> TryLogin(string emailOrPhone, string password)
    {
        _logger.LogDebug("TryLogin called");

        try
        {
            PasswordHasher<User> passwordHasher = new PasswordHasher<User>();
        
            var userExists = await GetUserByPhoneOrEmail(emailOrPhone);
            if (userExists == null)
            {
                _logger.LogInformation("TryLogin failed, user was not found");
                return new User { UserId = 0 };
            }
        
            var hashedPassword = passwordHasher.VerifyHashedPassword(userExists, userExists.Password, password);
            if (hashedPassword == PasswordVerificationResult.Failed)
            {
                _logger.LogInformation(
                    "TryLogin failed, invalid password for user {UserId}",
                    userExists.UserId);
                return new User { UserId = 0 };
            }

            _logger.LogInformation(
                "TryLogin succeeded for user {UserId}",
                userExists.UserId);

            return userExists;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TryLogin failed due to an exception");
            return null;
        }
    }

    public async Task<User?> CreateUser(User user)
    {
        _logger.LogDebug("CreateUser called");

        try
        {
            user.UserId = await GetMaxUserId() + 1;
        
            var userExists = await UserExists(user.Email, user.PhoneNumber);
            if (userExists)
            {
                _logger.LogWarning("CreateUser failed, user with matching email or phone number already exists");
                return new User() { UserId = 0 };
            }

            PasswordHasher<User> passwordHasher = new PasswordHasher<User>();
            var hashedPassword = passwordHasher.HashPassword(user, user.Password);
            user.Password = hashedPassword;
            
            await _userCollection.InsertOneAsync(user);
            user.Password = "";

            _logger.LogInformation(
                "CreateUser succeeded for user {UserId}",
                user.UserId);

            return user;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CreateUser failed");
            return null;
        }
    }

    public async Task<List<User>?> GetQueriedUsers(string query, int limit, int page)
    {
        _logger.LogDebug(
            "GetQueriedUsers called with query length {QueryLength}, limit {Limit}, page {Page}",
            query?.Length ?? 0, limit, page);

        try
        {
            var handledString = query.Trim();
            var finalString = "";
        
            int concurrentSpaces = 0;
            foreach (var c in handledString)
            {
                if (!char.IsWhiteSpace(c))
                    concurrentSpaces = 0;
                else if (char.IsWhiteSpace(c))
                    concurrentSpaces++;

                if (concurrentSpaces > 1)
                    continue;
            
                finalString += c;
            }

            var regex = $"^{Regex.Escape(finalString)}";
            var pipeline = new[]
            {
                new BsonDocument("$addFields", new BsonDocument("FullName", new BsonDocument("$concat", new BsonArray { "$FirstName", " ", "$LastName" }))),
                new BsonDocument("$match", new BsonDocument("FullName", new BsonDocument("$regex", regex).Add("$options", "i"))),
                new BsonDocument("$project", new BsonDocument {
                    { "FullName", 0 },
                    { "Password", 0 }
                }),
                new BsonDocument("$skip", page * limit),
                new BsonDocument("$limit", limit + 1)
            };

            var result = await _userCollection.Aggregate<User>(pipeline).ToListAsync();

            _logger.LogDebug(
                "GetQueriedUsers returned {Count} users",
                result.Count);

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "GetQueriedUsers failed with limit {Limit} and page {Page}",
                limit, page);
            return null;
        }
    }

    public async Task<User?> GetUserByUserIdAsync(int userId)
    {
        _logger.LogDebug(
            "GetUserByUserIdAsync called for user {UserId}",
            userId);

        try
        {
            var filter = Builders<User>.Filter.Eq("UserId", userId);
            var projection = Builders<User>.Projection.Exclude("Password");
        
            var result = await _userCollection.Find(filter).Project<User>(projection).FirstOrDefaultAsync();

            if (result == null)
            {
                _logger.LogDebug(
                    "GetUserByUserIdAsync found no user for user id {UserId}",
                    userId);
            }

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "GetUserByUserIdAsync failed for user {UserId}",
                userId);
            return null;
        }
    }

    public async Task<List<User>?> GetUsersForConversationByUserIdAsync(List<int> userIds)
    {
        _logger.LogDebug(
            "GetUsersForConversationByUserIdAsync called for {Count} user ids",
            userIds?.Count ?? 0);

        try
        {
            List<User> users = new List<User>();

            foreach (var userId in userIds)
            {
                var filter = Builders<User>.Filter.Eq("UserId", userId);
                var projection = Builders<User>.Projection.Exclude("Password").Exclude("PhoneNumber").Exclude("Email");
                var user = await _userCollection.Find(filter).Project<User>(projection).FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogWarning(
                        "GetUsersForConversationByUserIdAsync did not find user {UserId}",
                        userId);
                }

                users.Add(user);
            }

            _logger.LogDebug(
                "GetUsersForConversationByUserIdAsync returned {Count} users",
                users.Count);

            return users;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "GetUsersForConversationByUserIdAsync failed");
            return null;
        }
    }

    private async Task<User?> GetUserByUserIdWithPasswordAsync(int userId)
    {
        _logger.LogDebug(
            "GetUserByUserIdWithPasswordAsync called for user {UserId}",
            userId);

        try
        {
            var filter = Builders<User>.Filter.Eq("UserId", userId);
            var result = await _userCollection.Find(filter).FirstOrDefaultAsync();

            if (result == null)
            {
                _logger.LogDebug(
                    "GetUserByUserIdWithPasswordAsync found no user for user id {UserId}",
                    userId);
            }

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "GetUserByUserIdWithPasswordAsync failed for user {UserId}",
                userId);
            return null;
        }
    }

    public async Task<int> UpdateUser(ProfileUser user)
    {
        _logger.LogDebug(
            "UpdateUser called for user {UserId}",
            user.UserId);

        try
        {
            var currentUser = await GetUserByUserIdWithPasswordAsync(user.UserId);
            if (currentUser == null)
            {
                _logger.LogWarning(
                    "UpdateUser failed, current user {UserId} was not found",
                    user.UserId);
                return 2;
            }

            if (user.Email != currentUser.Email)
            {
                var result = await UserExists(email: user.Email);
                if (result)
                {
                    _logger.LogWarning(
                        "UpdateUser failed, email already exists for user {UserId}",
                        user.UserId);
                    return 0;
                }
            }

            if (user.PhoneNumber != currentUser.PhoneNumber)
            {
                var result = await UserExists(phonenumber: user.PhoneNumber);
                if (result)
                {
                    _logger.LogWarning(
                        "UpdateUser failed, phone number already exists for user {UserId}",
                        user.UserId);
                    return 0;
                }
            }
        
            var filter = Builders<User>.Filter.Eq("UserId", user.UserId);

            User newUser = new User
            {
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Password = currentUser.Password,
                UserId = user.UserId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ProfilePicture = user.ProfilePicture
            };

            await _userCollection.ReplaceOneAsync(filter, newUser);

            _logger.LogInformation(
                "UpdateUser succeeded for user {UserId}",
                user.UserId);

            return 1;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "UpdateUser failed for user {UserId}",
                user.UserId);
            return 2;
        }
    }

    private async Task<bool> UserExists(string email = "nothing", string phonenumber = "0")
    {
        _logger.LogDebug("UserExists called");

        try
        {
            if (email != "nothing" && !string.IsNullOrEmpty(email))
            {
                var filter = Builders<User>.Filter.Eq("Email", email);
                var result = await _userCollection.Find(filter).AnyAsync();
                if (result)
                {
                    _logger.LogDebug("UserExists found matching email");
                    return true;
                }
            }

            if (phonenumber != "0" && !string.IsNullOrEmpty(phonenumber))
            {
                var filter = Builders<User>.Filter.Eq("PhoneNumber", phonenumber);
                var result = await _userCollection.Find(filter).AnyAsync();
                if (result)
                {
                    _logger.LogDebug("UserExists found matching phone number");
                    return true;
                }
            }

            _logger.LogDebug("UserExists found no matching user");
            return false;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "UserExists failed");
            return false;
        }
    }

    private async Task<int> GetMaxRefreshTokenId()
    {
        _logger.LogDebug("GetMaxRefreshTokenId called");

        try
        {
            var filter = Builders<RefreshToken>.Filter.Empty;
        
            var result = await _refreshTokenCollection
                .Find(filter)
                .SortByDescending(u => u.Id)
                .Limit(1)
                .FirstOrDefaultAsync();

            var maxId = result?.Id ?? 0;

            _logger.LogDebug(
                "GetMaxRefreshTokenId returned {MaxId}",
                maxId);

            return maxId;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetMaxRefreshTokenId failed");
            return 0;
        }
    }
    
    private async Task<int> GetMaxUserId()
    {
        _logger.LogDebug("GetMaxUserId called");

        try
        {
            var filter = Builders<User>.Filter.Empty;
        
            var result = await _userCollection
                .Find(filter)
                .SortByDescending(u => u.UserId)
                .Limit(1)
                .FirstOrDefaultAsync();

            var maxId = result?.UserId ?? 0;

            _logger.LogDebug(
                "GetMaxUserId returned {MaxId}",
                maxId);

            return maxId;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetMaxUserId failed");
            return 0;
        }
    }
}