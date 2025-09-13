using System.Text.RegularExpressions;
using ChatAppAPI.Repositories.Interfaces;
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
     

    public UserRepositoryMongoDb()
    {
        _connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
        if (string.IsNullOrEmpty(_connectionString))
        {
            throw new ConnectionAbortedException("No connection string set");
        }
        _mongoClient = new MongoClient(_connectionString);
        _mongoDatabase = _mongoClient.GetDatabase("ChatApp");
        _userCollection = _mongoDatabase.GetCollection<User>("Users");
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
    
    // The function above and below can be optimized (if user found with Number, don't try to find it with email in the bottom functoin)
    // You know what maybe the whole bottom function can be rewritten
    public async Task<User?> TryLogin(string emailOrPhone, string password)
    {
        PasswordHasher<User> passwordHasher = new PasswordHasher<User>();
        
        var userExists = await GetUserByPhoneOrEmail(emailOrPhone);
        if (userExists == null)
            return new User { UserId = 0 };
        
        var hashedPassword = passwordHasher.VerifyHashedPassword(userExists, userExists.Password, password);
        if (hashedPassword == PasswordVerificationResult.Failed)
            return new User { UserId = 0 };
        
        var emailFilter = Builders<User>.Filter.Eq("Email", emailOrPhone);
        var phoneFilter = Builders<User>.Filter.Eq("PhoneNumber", emailOrPhone);
        var passwordFilter = Builders<User>.Filter.Eq("Password", userExists.Password);
        
        var emailPassFilter = Builders<User>.Filter.And(emailFilter, passwordFilter);

        try
        {
            var emailResult = await _userCollection.FindAsync(emailPassFilter).Result.FirstOrDefaultAsync();

            if (emailResult != null)
                return emailResult;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }

        try
        {
            var phonePassResult = await _userCollection.FindAsync(phoneFilter).Result.FirstOrDefaultAsync();
            
            if(phonePassResult != null)
                return phonePassResult;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
        
        return new User(){UserId = 0};
        
    }

    public async Task<User?> CreateUser(User user)
    {
        user.UserId = await GetMaxId() + 1;
        
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

    public async Task<List<User>?> GetQueriedUsers(string query, int page)
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
            new BsonDocument("$skip", page),
            new BsonDocument("$limit", 10)
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
    
    private async Task<int> GetMaxId()
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