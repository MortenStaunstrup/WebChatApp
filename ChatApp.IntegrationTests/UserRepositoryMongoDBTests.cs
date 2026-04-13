using ChatAppAPI.Repositories;
using ChatAppAPI.Repositories.Interfaces;
using ChatAppAPI.Token;
using Core;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Moq;

namespace ChatApp.IntegrationTests;

[TestClass]
public sealed class UserRepositoryMongoDBTests
{
    private TokenProvider _tokenProvider = null!;
    
    private IMongoClient _mongoClient = null!;
    private IMongoDatabase _database = null!;
    private UserRepositoryMongoDb _userRepository = null!;
    private string _databaseName = null!;

    private IMongoCollection<RefreshToken> _refreshTokenCollection = null!;
    
    // Input validation is handled in the respective controller
    
    [TestInitialize]
    public void Initialize_Tests()
    {
        // Should mirror appsettings.json in 'ChatAppAPI'
        var configRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:ExpirationInMinutes", "30" },
                { "Jwt:Issuer", "chatappme" },
                { "Jwt:Audience", "account" },
                { "Jwt:RefreshTokenExpirationInDays", "7" }
            })
            .Build();

        var connectionString =
            Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
            ?? "mongodb://localhost:27017";

        _mongoClient = new MongoClient(connectionString);

        _databaseName = $"ChatAppTest_{Guid.NewGuid():N}";
        _database = _mongoClient.GetDatabase(_databaseName);
        
        Environment.SetEnvironmentVariable("JWT_SECRET", "randomassvariablefortestingpurposesonly1521/%(%&¤#&#())/%%¤%&#");
        
        _tokenProvider = new TokenProvider(configRoot);
        
        _userRepository = new UserRepositoryMongoDb(_tokenProvider, _database, configRoot);
        
        _refreshTokenCollection = _database.GetCollection<RefreshToken>("RefreshTokens");
    }
    
    [TestCleanup]
    public void Cleanup()
    {
        _mongoClient.DropDatabase(_databaseName);
    }
    
    // TryLogin method tests
    //
    //

    [TestMethod]
    public async Task TryLogin_returns_user_with_valid_login()
    {
        // Arrange
        var email = "myEmail@email.com";
        var password = "password123";
        var phoneNumber = "45612785";

        var user = new User()
        {
            Email = email,
            Password = password,
            PhoneNumber = phoneNumber,
            FirstName = "random",
            LastName = "random",
            UserId = 23523
        };
        await _userRepository.CreateUser(user);
        
        // Act
        
        var phoneResult = await _userRepository.TryLogin(phoneNumber, password);
        var emailResult = await _userRepository.TryLogin(email, password);
        
        // Assert
        Assert.IsNotNull(phoneResult);
        Assert.IsInstanceOfType(phoneResult, typeof(User));
        Assert.AreEqual(user.Email, phoneResult.Email);
        Assert.AreEqual(user.UserId, phoneResult.UserId);
        
        Assert.IsNotNull(emailResult);
        Assert.IsInstanceOfType(emailResult, typeof(User));
        Assert.AreEqual(user.Email, emailResult.Email);
        Assert.AreEqual(user.UserId, emailResult.UserId);
    }
    
    [TestMethod] 
    public async Task TryLogin_returns_userIdZero_if_user_not_found()
    {
        // Arrange
        var password = "strongpassword125";
        var email = "myemail@gmail.com";
        // Act
        
        var result = await _userRepository.TryLogin(email, password);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.UserId);
    }
    
    [TestMethod] 
    public async Task TryLogin_returns_userIdZero_if_password_wrong()
    {
        // Arrange
        var email = "myemail@email.com";
        var wrongPassword = "wrongpassword123";
        var user = new User()
        {
            UserId = 23523,
            Email = email,
            Password = "correctPassword346",
            PhoneNumber = "78945612",
            FirstName = "random",
            LastName = "random"
        };
        await _userRepository.CreateUser(user);
        
        // Act
        
        var result = await _userRepository.TryLogin(email, wrongPassword);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.UserId);
    }
    
    // CheckRefreshToken tests
    //
    //

    [TestMethod]
    public async Task CheckRefreshToken_returns_refresh_token_on_valid_refresh_token()
    {
        // Arrange
        var token = Guid.NewGuid().ToString();
        var refreshToken = new RefreshToken()
        {
            Id = 123,
            Token = token,
            UserId = 536,
            ExpiresOnUTC = DateTime.UtcNow.AddMinutes(30),
        };
        await _refreshTokenCollection.InsertOneAsync(refreshToken);

        // Act
        var result = await _userRepository.CheckRefreshToken(token);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(token, result);
    }
    
    [TestMethod]
    public async Task CheckRefreshToken_returns_empty_string_on_invalid_refresh_token()
    {
        // Arrange
        var token =  Guid.NewGuid().ToString();
        
        // Act
        var result = await _userRepository.CheckRefreshToken(token);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(string.Empty, result);
    }
    
    [TestMethod]
    public async Task CheckRefreshToken_returns_empty_string_on_expired_refresh_token()
    {
        // Arrange
        var token = Guid.NewGuid().ToString();
        var refreshToken = new RefreshToken()
        {
            Id = 123,
            Token = token,
            UserId = 536,
            ExpiresOnUTC = DateTime.UtcNow.AddMinutes(-30)
        };
        await _refreshTokenCollection.InsertOneAsync(refreshToken);
        
        // Act
        var result = await _userRepository.CheckRefreshToken(token); 
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(string.Empty, result);
    }
    
    // CreateRefreshToken tests
    //
    //

    [TestMethod]
    public async Task CreateRefreshToken_creates_refresh_token_if_not_exist_and_returns_it()
    {
        // Arrange
        var userId = 23523;
        
        // Act
        var result = await _userRepository.CreateRefreshToken(userId);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreNotEqual(string.Empty, result);
    }
    
    [TestMethod]
    public async Task CreateRefreshToken_creates_new_refresh_token_if_already_exist_and_returns_it()
    {
        // Arrange
        var userId = 23523;
        var firstToken = await _userRepository.CreateRefreshToken(userId);
        Assert.IsNotNull(firstToken);
        Assert.AreNotEqual(string.Empty, firstToken);
        
        // Act
        var secondToken = await _userRepository.CreateRefreshToken(userId);
        
        // Assert
        Assert.IsNotNull(secondToken);
        Assert.AreNotEqual(string.Empty, secondToken);
        Assert.AreNotEqual(firstToken, secondToken);
    }
    
    // CreateUser tests
    //
    //
    
    [TestMethod]
    public async Task CreateUser_returns_user_on_success()
    {
        // Arrange
        var user = new User()
        {
            UserId = 23523,
            FirstName = "random",
            LastName = "random",
            Email = "random@gmail.com",
            PhoneNumber = "78945612",
            Password = "correctPassword123",
        };

        // Act
        var result = await _userRepository.CreateUser(user);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(User));
        Assert.AreEqual(user.UserId, result.UserId);
        Assert.AreEqual(user.FirstName, result.FirstName);
        Assert.AreEqual(user.LastName, result.LastName);
    }
    
    
    [TestMethod]
    public async Task CreateUser_returns_user_with_id_zero_if_user_already_exists()
    {
        // Arrange
        var existingUser = new User()
        {
            UserId = 233,
            FirstName = "random",
            LastName = "random",
            Email = "random@gmail.com",
            PhoneNumber = "78945612",
            Password = "correctPassword123"
        };
        await _userRepository.CreateUser(existingUser);

        // Act
        var result = await _userRepository.CreateUser(existingUser);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(User));
        Assert.AreNotEqual(existingUser.UserId, result.UserId);
        Assert.AreEqual(0, result.UserId);
    }
    
    [TestMethod]
    public async Task CreateUser_does_not_return_user_password()
    {
        // Arrange
        var user = new User()
        {
            UserId = 23523,
            FirstName = "random",
            LastName = "random",
            Email = "random@gmail.com",
            PhoneNumber = "78945612",
            Password = "correctPassword123",
        };

        // Act
        var result = await _userRepository.CreateUser(user);
        Console.WriteLine($"Result id: {result.UserId}");
        Console.WriteLine($"Result password: {result.Password}");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(User));
        Assert.AreEqual(string.Empty, result.Password);
    }
    
    // GetQueriedUsers tests
    //
    //

    [TestMethod]
    public async Task GetQueriedUsers_returns_correct_users()
    {
        // Arrange
        var query = "Morten";
        var page = 0;
        var limit = 10;
        var user1 = new User()
        {
            UserId = 1, FirstName = "Morten", LastName = "Nielsen", Email = "random@gmail.com", Password = "random",
            PhoneNumber = "99999999"
        };
        var user2 = new User()
        {
            UserId = 2, FirstName = "Morten", LastName = "Frederiksen", Email = "random2@gmail.com", Password = "random",
            PhoneNumber = "99999998"
        };
        var user3 = new User()
        {
            UserId = 3, FirstName = "Yggdrasil", LastName = "Hjalmar", Email = "random3@gmail.com", Password = "random",
            PhoneNumber = "79846213"
        };
        await _userRepository.CreateUser(user1);
        await _userRepository.CreateUser(user2);
        await _userRepository.CreateUser(user3);
        
        // Act
        var result = await _userRepository.GetQueriedUsers(query, limit, page);
        
        // Assert
        Assert.IsNotNull(result);
        foreach (var user in result)
        {
            Console.WriteLine($"{user.UserId}: {user.FirstName} {user.LastName} {user.Email}");
        }
        Assert.IsInstanceOfType(result, typeof(List<User>));
        Assert.HasCount(2, result);
        Assert.AreEqual("Morten", result[0].FirstName);
        Assert.AreEqual("Morten", result[1].FirstName);
        
    }
    
    [TestMethod]
    public async Task GetQueriedUsers_returns_correct_users_limit_plus_one()
    {
        // Arrange
        var query = "Morten";
        var page = 0;
        var limit = 1;
        var user1 = new User()
        {
            UserId = 1, FirstName = "Morten", LastName = "Nielsen", Email = "random@gmail.com", Password = "random",
            PhoneNumber = "99999999"
        };
        var user2 = new User()
        {
            UserId = 2, FirstName = "Morten", LastName = "Frederiksen", Email = "random2@gmail.com", Password = "random",
            PhoneNumber = "99999998"
        };
        var user3 = new User()
        {
            UserId = 3, FirstName = "Yggdrasil", LastName = "Hjalmar", Email = "random3@gmail.com", Password = "random",
            PhoneNumber = "79846213"
        };
        await _userRepository.CreateUser(user1);
        await _userRepository.CreateUser(user2);
        await _userRepository.CreateUser(user3);
        
        // Act
        var result = await _userRepository.GetQueriedUsers(query, limit, page);
        
        // Assert
        Assert.IsNotNull(result);
        foreach (var user in result)
        {
            Console.WriteLine($"{user.UserId}: {user.FirstName} {user.LastName} {user.Email}");
        }
        Assert.IsInstanceOfType(result, typeof(List<User>));
        Assert.HasCount(limit + 1, result);
    }
    
    [TestMethod]
    public async Task GetQueriedUsers_returns_correct_users_page()
    {
        // Arrange
        var query = "Morten";
        var page = 1;
        var limit = 1;
        var user1 = new User()
        {
            UserId = 1, FirstName = "Morten", LastName = "Nielsen", Email = "random@gmail.com", Password = "random",
            PhoneNumber = "99999999"
        };
        var user2 = new User()
        {
            UserId = 2, FirstName = "Morten", LastName = "Frederiksen", Email = "random2@gmail.com", Password = "random",
            PhoneNumber = "99999998"
        };
        var user3 = new User()
        {
            UserId = 3, FirstName = "Yggdrasil", LastName = "Hjalmar", Email = "random3@gmail.com", Password = "random",
            PhoneNumber = "79846213"
        };
        await _userRepository.CreateUser(user1);
        await _userRepository.CreateUser(user2);
        await _userRepository.CreateUser(user3);
        
        // Act
        var result = await _userRepository.GetQueriedUsers(query, limit, page);
        
        // Assert
        Assert.IsNotNull(result);
        foreach (var user in result)
        {
            Console.WriteLine($"{user.UserId}: {user.FirstName} {user.LastName} {user.Email}");
        }
        Assert.IsInstanceOfType(result, typeof(List<User>));
        Assert.HasCount(limit, result);
    }
    
    [TestMethod]
    public async Task GetQueriedUsers_returns_correct_users_trimming_query()
    {
        // Arrange
        var query = "    Yggdrasil     Hjalmar     ";
        var page = 0;
        var limit = 20;
        var user1 = new User()
        {
            UserId = 1, FirstName = "Morten", LastName = "Nielsen", Email = "random@gmail.com", Password = "random",
            PhoneNumber = "99999999"
        };
        var user2 = new User()
        {
            UserId = 2, FirstName = "Morten", LastName = "Frederiksen", Email = "random2@gmail.com", Password = "random",
            PhoneNumber = "99999998"
        };
        var user3 = new User()
        {
            UserId = 3, FirstName = "Yggdrasil", LastName = "Hjalmar", Email = "random3@gmail.com", Password = "random",
            PhoneNumber = "79846213"
        };
        await _userRepository.CreateUser(user1);
        await _userRepository.CreateUser(user2);
        await _userRepository.CreateUser(user3);
        
        // Act
        var result = await _userRepository.GetQueriedUsers(query, limit, page);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(List<User>));
        Console.WriteLine($"User: {result[0].FirstName} {result[0].LastName} {result[0].Email}");
        Assert.HasCount(1, result);
        Assert.AreEqual("Yggdrasil", result[0].FirstName);
        Assert.AreEqual("Hjalmar", result[0].LastName);
    }
    
    [TestMethod]
    public async Task GetQueriedUsers_returns_correct_users_with_firstname_and_lastname()
    {
        // Arrange
        var query = "Morten Nielsen";
        var page = 0;
        var limit = 20;
        var user1 = new User()
        {
            UserId = 1, FirstName = "Morten", LastName = "Nielsen", Email = "random@gmail.com", Password = "random",
            PhoneNumber = "99999999"
        };
        var user2 = new User()
        {
            UserId = 2, FirstName = "Morten", LastName = "Frederiksen", Email = "random2@gmail.com", Password = "random",
            PhoneNumber = "99999998"
        };
        var user3 = new User()
        {
            UserId = 3, FirstName = "Yggdrasil", LastName = "Hjalmar", Email = "random3@gmail.com", Password = "random",
            PhoneNumber = "79846213"
        };
        await _userRepository.CreateUser(user1);
        await _userRepository.CreateUser(user2);
        await _userRepository.CreateUser(user3);
        
        // Act
        var result = await _userRepository.GetQueriedUsers(query, limit, page);
        
        // Assert
        Assert.IsNotNull(result);
        foreach (var user in result)
        {
            Console.WriteLine($"{user.UserId}: {user.FirstName} {user.LastName} {user.Email}");
        }
        Assert.IsInstanceOfType(result, typeof(List<User>));
        Assert.HasCount(1, result);
        Assert.AreEqual("Morten", result[0].FirstName);
        Assert.AreEqual("Nielsen", result[0].LastName);
    }
    
    // GetUserByUserIdAsync tests
    //
    //

    [TestMethod]
    public async Task GetUserByUserIdAsync_returns_existing_user()
    {
        // Arrange
        var userId = 1;
        var firstName = "Drake";
        var lastName = "McLoving";
        var email = "drake@drake.com";
        var password = "drake";
        var phoneNumber = "123456789";
        var currentUser = new User()
        {
            UserId = userId, FirstName = firstName, LastName = lastName, Email = email, Password = password,
            PhoneNumber = phoneNumber
        };
        await _userRepository.CreateUser(currentUser);
        
        // Act
        var result = await _userRepository.GetUserByUserIdAsync(userId);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(User));
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(firstName, result.FirstName);
        Assert.AreEqual(lastName, result.LastName);
        Assert.AreEqual(email, result.Email);
    }
    
    [TestMethod]
    public async Task GetUserByUserIdAsync_returns_null_if_user_not_found()
    {
        // Arrange
        
        
        // Act
        var result = await _userRepository.GetUserByUserIdAsync(1);
        
        // Assert
        Assert.IsNull(result);
    }
    
    [TestMethod]
    public async Task GetUserByUserIdAsync_returns_existing_user_excluded_password()
    {
        // Arrange
        var userId = 1;
        var firstName = "Drake";
        var lastName = "McLoving";
        var email = "drake@drake.com";
        var password = "drake";
        var phoneNumber = "123456789";
        var currentUser = new User()
        {
            UserId = userId, FirstName = firstName, LastName = lastName, Email = email, Password = password,
            PhoneNumber = phoneNumber
        };
        await _userRepository.CreateUser(currentUser);
    
        // Act
        var result = await _userRepository.GetUserByUserIdAsync(userId);
    
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(User));
        Assert.AreEqual(userId, result.UserId);
        Assert.AreEqual(firstName, result.FirstName);
        Assert.AreEqual(lastName, result.LastName);
        Assert.AreEqual(email, result.Email);
        Assert.IsNull(result.Password);
    }
    
    // GetUsersForConversationByUserIdAsync tests
    //
    //
    
    [TestMethod]
    public async Task GetUsersForConversationByUserIdAsync_returns_all_users()
    {
        // Arrange
        var user1 = new User()
        {
            UserId = 1,
            FirstName = "Morten",
            LastName = "Frederiksen",
            Email = "morten@gmail.com",
            Password = "morten",
            PhoneNumber = "79846213"
        };
        var user2 = new User()
        {
            UserId = 2,
            FirstName = "Yggdrasil",
            LastName = "Hjalmar",
            Email = "ygg@hotmail.com",
            Password = "ygg",
            PhoneNumber = "54862318"
        };
        var user3 = new User()
        {
            UserId = 3,
            FirstName = "Emil",
            LastName = "Mortensen",
            Email = "em@gmail.com",
            Password = "em123456789",
            PhoneNumber = "78994320156"
        };
        await _userRepository.CreateUser(user1);
        await _userRepository.CreateUser(user2);
        await _userRepository.CreateUser(user3);
        
        List<int> ids = new List<int>(){1,2,3};

        // Act
        var result = await _userRepository.GetUsersForConversationByUserIdAsync(ids);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(List<User>));
        Assert.HasCount(3, result);
        foreach (var user in result)
        {
            Console.WriteLine($"{user.UserId}: {user.FirstName} {user.LastName} {user.Email}");
        }
        Assert.AreEqual("Morten", result[0].FirstName);
        Assert.AreEqual("Yggdrasil", result[1].FirstName);
        Assert.AreEqual("Emil", result[2].FirstName);
    }
    
    [TestMethod]
    public async Task GetUsersForConversationByUserIdAsync_returns_all_users_excluding_password_phonenumber_email()
    {
        // Arrange
        var user1 = new User()
        {
            UserId = 1,
            FirstName = "Morten",
            LastName = "Frederiksen",
            Email = "morten@gmail.com",
            Password = "morten",
            PhoneNumber = "79846213"
        };
        var user2 = new User()
        {
            UserId = 2,
            FirstName = "Yggdrasil",
            LastName = "Hjalmar",
            Email = "ygg@hotmail.com",
            Password = "ygg",
            PhoneNumber = "54862318"
        };
        var user3 = new User()
        {
            UserId = 3,
            FirstName = "Emil",
            LastName = "Mortensen",
            Email = "em@gmail.com",
            Password = "em123456789",
            PhoneNumber = "78994320156"
        };
        await _userRepository.CreateUser(user1);
        await _userRepository.CreateUser(user2);
        await _userRepository.CreateUser(user3);
        
        List<int> ids = new List<int>(){1,2,3};

        // Act
        var result = await _userRepository.GetUsersForConversationByUserIdAsync(ids);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(List<User>));
        Assert.HasCount(3, result);
        
        Assert.AreEqual("Morten", result[0].FirstName);
        Assert.AreEqual("Yggdrasil", result[1].FirstName);
        Assert.AreEqual("Emil", result[2].FirstName);
        
        Assert.IsNull(result[0].Email);
        Assert.IsNull(result[1].Email);
        Assert.IsNull(result[2].Email);
        Assert.IsNull(result[0].Password);
        Assert.IsNull(result[1].Password);
        Assert.IsNull(result[2].Password);
        Assert.IsNull(result[0].PhoneNumber);
        Assert.IsNull(result[1].PhoneNumber);
        Assert.IsNull(result[2].PhoneNumber);
    }
    
    // UpdateUser tests
    //
    //
    
    [TestMethod]
    public async Task UpdateUser_correctly_updates_user_and_returns_1()
    {
        // Arrange
        var userId = 1;
        var firstName = "Drake";
        var lastName = "McLoving";
        var email = "drake@drake.com";
        var password = "drake";
        var phoneNumber = "123456789";
        var currentUser = new User()
        {
            UserId = userId, FirstName = firstName, LastName = lastName, Email = email, Password = password,
            PhoneNumber = phoneNumber
        };
        await _userRepository.CreateUser(currentUser);

        var newEmail = "newEmail.haha";
        var newPhonenumber = "44561174";
        var newUser = new ProfileUser()
        {
            Email = newEmail,
            UserId = userId,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = newPhonenumber
        };

        // Act
        var result = await _userRepository.UpdateUser(newUser);
        var userResult = await _userRepository.GetUserByUserIdAsync(newUser.UserId);

        // Assert
        Assert.AreEqual(1, result);
        Assert.IsNotNull(userResult);
        Assert.AreEqual(newEmail, userResult.Email);
        Assert.AreEqual(newPhonenumber, userResult.PhoneNumber);
        Assert.AreEqual(currentUser.UserId, userResult.UserId);
    }
    
    [TestMethod]
    public async Task UpdateUser_returns_0_if_phonenumber_or_email_already_exists()
    {
        // Arrange

        var existingEmail = "taken@email.com";
        var existingPhonenumber = "44561174";
        var existingUser = new User()
        {
            UserId = 1,
            FirstName = "Morten",
            LastName = "Frederiksen",
            Email = existingEmail,
            Password = "drake",
            PhoneNumber = existingPhonenumber
        };
        await _userRepository.CreateUser(existingUser);
        
        var userId = 2;
        var firstName = "Drake";
        var lastName = "McLoving";
        var password = "drake";
        var drakeCurrentEmail = "drake@drake.com";
        var phoneNumber = "123456789";
        var currentUser = new User()
        {
            UserId = userId, FirstName = firstName, LastName = lastName, Email = drakeCurrentEmail, Password = password,
            PhoneNumber = phoneNumber
        };
        await _userRepository.CreateUser(currentUser);
        
        var newUser = new ProfileUser()
        {
            Email = existingEmail,
            UserId = userId,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = existingPhonenumber
        };

        // Act
        var result = await _userRepository.UpdateUser(newUser);
        var userResult = await _userRepository.GetUserByUserIdAsync(2);

        // Assert
        Assert.AreEqual(0, result);
        Assert.IsNotNull(userResult);
        Assert.AreNotEqual(existingEmail, userResult.Email);
        Assert.AreNotEqual(existingPhonenumber, userResult.PhoneNumber);
    }
    
}