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
    private readonly string _connectionString;
    private readonly IMongoClient _mongoClient;
    private readonly IMongoDatabase _mongoDatabase;
    private readonly IMongoCollection<User> _userCollection;
    private readonly IMongoCollection<RefreshToken> _refreshTokenCollection;
    TokenProvider _tokenProvider;
    private readonly int refreshTokenExpirationTimeInDays;

    public UserRepositoryMongoDb(TokenProvider tokenProvider, IConfiguration configuration)
    {
        _connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
        if (string.IsNullOrEmpty(_connectionString))
        {
            throw new ConnectionAbortedException("No connection string set");
        }
        _mongoClient = new MongoClient(_connectionString);
        _mongoDatabase = _mongoClient.GetDatabase("ChatApp");
        _userCollection = _mongoDatabase.GetCollection<User>("Users");
        _refreshTokenCollection = _mongoDatabase.GetCollection<RefreshToken>("RefreshTokens");
        _tokenProvider = tokenProvider;
        refreshTokenExpirationTimeInDays = configuration.GetValue<int>("Jwt:RefreshTokenExpirationInDays");
    }

    // Called from LoginWithRefreshToken endpoint
    public async Task<string> CheckRefreshToken(string refreshToken)
    {
        var filter = Builders<RefreshToken>.Filter.Eq("Token", refreshToken);
        var refreshTokenResult = await _refreshTokenCollection.Find(filter).FirstOrDefaultAsync();
        if (refreshTokenResult == null)
            return "";
        
        if (refreshTokenResult.ExpiresOnUTC < DateTime.UtcNow)
            return "";
        
        return refreshTokenResult.Token;
    }

    public async Task<string> CreateRefreshToken(int userId)
    {
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
                    ExpiresOnUTC = DateTime.UtcNow.AddDays(refreshTokenExpirationTimeInDays)
                };
                await _refreshTokenCollection.ReplaceOneAsync(r => r.Id == savedToken.Id, savedToken);
                return token;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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
                ExpiresOnUTC = DateTime.UtcNow.AddDays(refreshTokenExpirationTimeInDays)
            };
            await _refreshTokenCollection.InsertOneAsync(savedToken);
            return token;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return "";
        }
    }

    // Used by CreateRefreshToken function
    private async Task<RefreshToken?> RefreshTokenForUserExists(int userId)
    {
        var filter = Builders<RefreshToken>.Filter.Eq("UserId", userId);
        var refreshToken = await _refreshTokenCollection.Find(filter).FirstOrDefaultAsync();
        return refreshToken;
    }

    private async Task<User?> GetUserByPhoneOrEmail(string PhoneOrEmail)
    {
        var filter = Builders<User>.Filter.Eq("Email", PhoneOrEmail);
        var user = await _userCollection.Find(filter).FirstOrDefaultAsync();
        if (user != null)
            return user;
        
        var filter2 = Builders<User>.Filter.Eq("PhoneNumber", PhoneOrEmail);
        var user2 = await _userCollection.Find(filter2).FirstOrDefaultAsync();
        if (user2 != null)
            return user2;
        return null;
    }
    
    public async Task<User?> TryLogin(string emailOrPhone, string password)
    {
        PasswordHasher<User> passwordHasher = new PasswordHasher<User>();
        
        var userExists = await GetUserByPhoneOrEmail(emailOrPhone);
        if (userExists == null)
            return new User { UserId = 0 };
        
        var hashedPassword = passwordHasher.VerifyHashedPassword(userExists, userExists.Password, password);
        if (hashedPassword == PasswordVerificationResult.Failed)
            return new User { UserId = 0 };

        return userExists;

    }

    public async Task<User?> CreateUser(User user)
    {
        user.UserId = await GetMaxUserId() + 1;
        
        var userExists = await UserExists(user.Email, user.PhoneNumber);
        
        if(userExists)
            return new User(){UserId = 0};
        
        try
        {
            PasswordHasher<User> passwordHasher = new PasswordHasher<User>();
            var hashedPassword = passwordHasher.HashPassword(user, user.Password);
            user.Password = hashedPassword;
            
            await _userCollection.InsertOneAsync(user);
            user.Password = "";
            return user;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
        
    }

    public async Task<List<User>?> GetQueriedUsers(string query, int limit, int page)
    {
        var handledString = query.Trim();
        
        var finalString = "";
        
        int concurrentSpaces = 0;
        foreach (var c in handledString)
        {
            if(!char.IsWhiteSpace(c))
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

        return await _userCollection.Aggregate<User>(pipeline).ToListAsync();
    }

    public async Task<User?> GetUserByUserIdAsync(int userId)
    {
        var filter = Builders<User>.Filter.Eq("UserId", userId);
        var projection = Builders<User>.Projection.Exclude("Password");
        
        return await _userCollection.Find(filter).Project<User>(projection).FirstOrDefaultAsync();
    }

    public async Task<List<User>?> GetUsersForConversationByUserIdAsync(List<int> userIds)
    {
        List<User> users = new List<User>();
        foreach (var userId in userIds)
        {
            var filter = Builders<User>.Filter.Eq("UserId", userId);
            var projection = Builders<User>.Projection.Exclude("Password").Exclude("PhoneNumber").Exclude("Email");
            var user =  await _userCollection.Find(filter).Project<User>(projection).FirstOrDefaultAsync();
            users.Add(user);
        }
        return users;
    }

    private async Task<User?> GetUserByUserIdWithPasswordAsync(int userId)
    {
        var filter = Builders<User>.Filter.Eq("UserId", userId);
        return await _userCollection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<int> UpdateUser(ProfileUser user)
    {
        var currentUser = await GetUserByUserIdWithPasswordAsync(user.UserId);

        if (user.Email != currentUser.Email)
        {
            var result = await UserExists(email: user.Email);
            if (result)
                return 0;
        }

        if (user.PhoneNumber != currentUser.PhoneNumber)
        {
            var result = await UserExists(phonenumber: user.PhoneNumber);
            if (result)
                return 0;
        }
        
        var filter = Builders<User>.Filter.Eq("UserId", user.UserId);

        try
        {
            User newUser = new User
            {
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Password = currentUser!.Password,
                UserId = user.UserId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ProfilePicture = user.ProfilePicture
            };
            await _userCollection.ReplaceOneAsync(filter, newUser);
            return 1;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return 2;
        }
        
    }

    private async Task<bool> UserExists(string email = "nothing", string phonenumber = "0")
    {
        if (email != "nothing" && !string.IsNullOrEmpty(email))
        {
            var filter = Builders<User>.Filter.Eq("Email", email);
            var result = await _userCollection.Find(filter).AnyAsync();
            if (result)
                return true;
        }
        if (phonenumber != "0" && !string.IsNullOrEmpty(phonenumber))
        {
            var filter  = Builders<User>.Filter.Eq("PhoneNumber", phonenumber);
            var result = await _userCollection.Find(filter).AnyAsync();
            if (result)
                return true;
        }
        return false;
    }

    private async Task<int> GetMaxRefreshTokenId()
    {
        var filter = Builders<RefreshToken>.Filter.Empty;
        
        var result = await _refreshTokenCollection
            .Find(filter)
            .SortByDescending(u => u.Id)
            .Limit(1)
            .FirstOrDefaultAsync();
        return result?.Id ?? 0;
    }
    
    private async Task<int> GetMaxUserId()
    {
        var filter = Builders<User>.Filter.Empty;
        
        var result = await _userCollection
            .Find(filter)
            .SortByDescending(u => u.UserId)
            .Limit(1)
            .FirstOrDefaultAsync();
        return result?.UserId ?? 0;
    }
}