using ChatAppAPI.Controllers;
using Moq;
using ChatAppAPI.Repositories.Interfaces;
using ChatAppAPI.Token;
using Core;
using Core.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Range = Moq.Range;

namespace ChatApp.Tests;

[TestClass]
public sealed class AuthControllerTest
{
    private Mock<IUserRepository> _userRepository;
    private AuthController _authController;
    
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

        Environment.SetEnvironmentVariable("JWT_SECRET", "randomassvariablefortestingpurposesonly1521/%(%&¤#&#())/%%¤%&#");
        
        _userRepository = new Mock<IUserRepository>();

        _authController = new AuthController(_userRepository.Object, new TokenProvider(configRoot));
    }
    
    // Login endpoint tests
    //
    //
    
    [TestMethod]
    public async Task Login_returns_OK_response_with_valid_login_credentials()
    {
        // Arrange
        _userRepository.Setup(repo => repo.TryLogin(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new User(){Email = "random@email.com", FirstName = "Maddie", LastName = "Johnson", Password = "password123", PhoneNumber = "84652648", UserId = 4});
        _userRepository.Setup(repo => repo.CreateRefreshToken(It.IsAny<int>()))
            .ReturnsAsync("new-refresh-token-123");
        var creds = new LoginRecord("random@email.com", "password123");
        
        // Act
        var result = await _authController.TryLogin(creds);


        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<OkObjectResult>(result);
        
        var okObjectResult = result as OkObjectResult;
        
        Assert.AreEqual(200, okObjectResult!.StatusCode);
        Assert.IsInstanceOfType<UserTokenDTO>(okObjectResult.Value);
        
        var userTokenDto = okObjectResult.Value as UserTokenDTO;
        
        Assert.AreEqual("placeholder", userTokenDto!.User.Password);
        Assert.IsNotNull(userTokenDto.User);
        Assert.IsNotNull(userTokenDto.Token);
        Assert.IsNotNull(userTokenDto.RefreshToken);
    }

    [TestMethod]
    public async Task Login_returns_Unauthorized_response_with_unauthorized_login_credentials()
    {
        // Arrange
        _userRepository.Setup(repo => repo.TryLogin("random@email.com", "incorrect"))
            .ReturnsAsync(new User(){UserId = 0});
        var creds = new LoginRecord("random@email.com", "incorrect");
        
        // Act
        var result = await _authController.TryLogin(creds);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
        
        var unauthorizedObjectResult = result as UnauthorizedObjectResult;
        
        Assert.AreEqual(401, unauthorizedObjectResult!.StatusCode);
    }
    
    [TestMethod]
    public async Task Login_returns_BadRequest_when_repository_down()
    {
        // Arrange
        _userRepository.Setup(repo => repo.TryLogin("random@email.com", "password123"))
            .ReturnsAsync((User?)null);
        var creds = new LoginRecord("random@email.com", "password123");
        
        // Act
        var result = await _authController.TryLogin(creds);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        
        var badRequestObjectResult = result as BadRequestObjectResult;
        
        Assert.AreEqual(400, badRequestObjectResult!.StatusCode);
    }
    
    [TestMethod]
    public async Task Login_returns_BadRequest_with_no_input()
    {
        // Arrange
        LoginRecord? creds = null;
        
        // Act
        var result = await _authController.TryLogin(creds!);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<BadRequestResult>(result);
        
        var badRequestObjectResult = result as BadRequestResult;
        
        Assert.AreEqual(400, badRequestObjectResult!.StatusCode);
    }
    
    // Login Refreshtoken endpoint tests
    //
    //
    
    [TestMethod]
    public async Task Loginrefresh_with_valid_refreshtoken_and_userId_returns_OK_object_result_with_new_JWT_and_refresh_token()
    {
        // Arrange
        var refreshToken = "refresh-token-123";
        var oldToken = "veryoldtokenthatisnotgoingtobeusedandinstedgoingtoberefreshedaswell523552623%¤/%¤/¤/";
        var userId = 4;
        _userRepository.Setup(repo => repo.CheckRefreshToken(refreshToken))
            .ReturnsAsync(refreshToken);
        _userRepository.Setup(repo => repo.CreateRefreshToken(userId))
            .ReturnsAsync("new-refresh-token-123");
        _userRepository.Setup(repo => repo.GetUserByUserIdAsync(userId))
            .ReturnsAsync(new User(){UserId = 4, Email = "randomemail", FirstName = "John", LastName = "Ygdrasil", Password = "randompassword", PhoneNumber = "78945612"});
        
        // Act
        var result = await _authController.LoginWithRefreshToken(userId, refreshToken);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<OkObjectResult>(result);
        
        var okObjectResult = result as OkObjectResult;
        Assert.AreEqual(200, okObjectResult!.StatusCode);
        Assert.IsInstanceOfType<TokensDto>(okObjectResult.Value);
        
        var userTokenDto = okObjectResult.Value as TokensDto;
        
        Assert.IsNotNull(userTokenDto!.RefreshToken);
        Assert.IsNotNull(userTokenDto.Token);
        Assert.AreNotEqual(oldToken, userTokenDto.Token);
        Assert.AreNotEqual(refreshToken, userTokenDto.RefreshToken);
    }

    [TestMethod]
    public async Task Loginrefresh_returns_Badrequest_on_invalid_userId()
    {
        // Arrange
        var randomToken = "random-token-123";
        
        // Act
        var result = await _authController.LoginWithRefreshToken(It.IsInRange(0, int.MinValue, Range.Inclusive), randomToken);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<BadRequestResult>(result);
    }
    
    [TestMethod]
    public async Task Loginrefresh_with_missing_parameters_returns_BadRequest()
    {
        // Arrange
        
        
        // Act
        var result = await _authController.LoginWithRefreshToken(0, null!);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<BadRequestResult>(result);
    }
    
    [TestMethod]
    public async Task Loginrefresh_refreshtoken_does_not_exist_returns_Unauthorized_response()
    {
        // Arrange
        var refreshToken = "refresh-token-123";
        _userRepository.Setup(repo => repo.CheckRefreshToken(refreshToken))
            .ReturnsAsync("");
        
        // Act
        var result = await _authController.LoginWithRefreshToken(26436234, refreshToken);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<UnauthorizedResult>(result);
    }
    
    [TestMethod]
    public async Task Loginrefresh_user_does_not_exist_throws_exception()
    {
        // Arrange
        var randomRefreshToken = "random-refresh-token-123";
        var newRefreshToken = "new-refresh-token-123";
        var randomUserId = 4;
        _userRepository.Setup(repo => repo.CheckRefreshToken(randomRefreshToken))
            .ReturnsAsync(newRefreshToken);
        _userRepository.Setup(repo => repo.GetUserByUserIdAsync(randomUserId))
            .ReturnsAsync((User?)null);
        
        // Act
        
        // Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await _authController.LoginWithRefreshToken(randomUserId,
                randomRefreshToken));
    }
    
    // Create user endpoint tests
    //
    //
    
    [TestMethod]
    public async Task Create_returns_OK_object_result()
    {
        // Arrange
        string email = "randomemail";
        string firstName = "John";
        string lastName = "Ygdrasil";
        string password = "randompassword";
        string phoneNumber = "78945612";
        var validUser = new User()
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Password = password,
            PhoneNumber = phoneNumber
        };
        _userRepository.Setup(repo => repo.CreateUser(validUser))
            .ReturnsAsync(new User(){UserId = 6, Email = email, FirstName = firstName, LastName = lastName, Password = "placeholder", PhoneNumber = phoneNumber});

        // Act
        
        var result = await _authController.CreateUser(validUser);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var okObjectResult = result as OkObjectResult;
        
        Assert.AreEqual(200, okObjectResult!.StatusCode);
        var user = okObjectResult.Value as User;
        
        Assert.AreEqual(email, user!.Email);
        Assert.AreEqual(firstName, user.FirstName);
        Assert.AreEqual(lastName, user.LastName);
        Assert.AreEqual(phoneNumber, user.PhoneNumber);
        
        // Don't send the users password back to them
        Assert.AreNotEqual(password, user.Password);
    }
    
    [TestMethod]
    public async Task Create_returns_Badrequest_on_missing_required_user_properties()
    {
        // Arrange
        string firstName = "John";
        string lastName = "Ygdrasil";
        string password = "randompassword";
        string phoneNumber = "78945612";
        var invalidUser = new User()
        {
            FirstName = firstName,
            LastName = lastName,
            Password = password,
            PhoneNumber = phoneNumber
        };
        
        // Act
        var result = await _authController.CreateUser(invalidUser);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<BadRequestResult>(result);
        var badRequestResult = result as BadRequestResult;
        Assert.AreEqual(400, badRequestResult!.StatusCode);
    }
    
    [TestMethod]
    public async Task Create_returns_Badrequest_on_empty_or_whitespace_strings()
    {
        // Arrange
        string email = "   ";
        string firstName = "John";
        string lastName = "   ";
        string password = "   ";
        string phoneNumber = "78945612";
        var invalidUser = new User()
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Password = password,
            PhoneNumber = phoneNumber
        };

        // Act
        
        var result = await _authController.CreateUser(invalidUser);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<BadRequestResult>(result);
        var badRequestResult = result as BadRequestResult;
        
        Assert.AreEqual(400, badRequestResult!.StatusCode);
    }
    
    [TestMethod]
    public async Task Create_returns_Unauthorized_result_if_user_email_or_phonenumber_already_exists()
    {
        // Arrange
        string email = "randomemail";
        string firstName = "John";
        string lastName = "Ygdrasil";
        string password = "randompassword";
        string phoneNumber = "78945612";
        var existingUser = new User()
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Password = password,
            PhoneNumber = phoneNumber
        };
        _userRepository.Setup(repo => repo.CreateUser(existingUser))
            .ReturnsAsync(new User() { UserId = 0 });

        // Act
        
        var result = await _authController.CreateUser(existingUser);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<UnauthorizedResult>(result);
        var unauthorizedResult = result as UnauthorizedResult;
        Assert.AreEqual(401, unauthorizedResult!.StatusCode);
    }
    
    
    // Get queried users endpoint tests
    //
    //

    [TestMethod]
    public async Task Queried_users_endpoint_returns_Ok_object_result_on_valid_query()
    {
        // Arrange
        string validQuery = "John Pork Davidson";
        int page = 1;
        int pageLimit = 2;
        _userRepository.Setup(repo => repo.GetQueriedUsers(validQuery, page, pageLimit))
            .ReturnsAsync(new List<User>()
            {
                new User(){UserId = 1, Email = "randomahhemail", FirstName = "John", LastName = "Pork Davidson", PhoneNumber =  "78945612", Password = "placeholder"},
                new User(){UserId = 2, Email = "randomahhemail2@hotmail.com", FirstName = "John", LastName = "Pork",  PhoneNumber =  "78945612", Password = "placeholder"}
            });

        // Act
        var result = await _authController.GetQueriedUsers(validQuery, page, pageLimit);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var okObjectResult = result as OkObjectResult;
        Assert.AreEqual(200, okObjectResult!.StatusCode);
        
        var users = okObjectResult.Value as List<User>;
        Assert.IsLessThanOrEqualTo(pageLimit, users!.Count);
        Assert.AreNotEqual(0, users.Count);
    }
    
    [TestMethod]
    public async Task Queried_users_endpoint_returns_No_content_result_on_no_users()
    {
        // Arrange
        string validQuery = "James May";
        int page = 1;
        int pageLimit = 15;
        _userRepository.Setup(repo => repo.GetQueriedUsers(validQuery, page, pageLimit))
            .ReturnsAsync(new List<User>());

        // Act
        var result = await _authController.GetQueriedUsers(validQuery, page, pageLimit);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<NoContentResult>(result);
    }
    
    [TestMethod]
    public async Task Queried_users_endpoint_returns_Accepted_result_on_fewer_users_than_page_limit()
    {
        // Arrange
        string validQuery = "James May";
        int page = 1;
        int pageLimit = 15;
        _userRepository.Setup(repo => repo.GetQueriedUsers(validQuery, page, pageLimit))
            .ReturnsAsync(new List<User>()
            {
                new User(){Email = "jm@topgun.uk", UserId = 782, LastName = "May", FirstName = "James", Password = "placeholder", PhoneNumber = "78546666"}
            });

        // Act
        var result = await _authController.GetQueriedUsers(validQuery, page, pageLimit);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<AcceptedResult>(result);
        var acceptedResult = result as AcceptedResult;

        Assert.IsNotNull(acceptedResult);
        Assert.AreEqual(202, acceptedResult.StatusCode);
        var users = acceptedResult.Value as List<User>;
        
        Assert.IsNotNull(users);
        Assert.IsLessThan(pageLimit, users.Count);
        Assert.AreNotEqual(0, users.Count);
    }
    
    [TestMethod]
    public async Task Queried_users_endpoint_returns_Badrequest_on_page_or_pageLimit_lessThan_or_equal_zero()
    {
        // Arrange
        string validQuery = "James May";
        int page = 0;
        int pageLimit = 0;

        int negativePage = -978;
        int negativepageLimit = -15;

        // Act
        var result = await _authController.GetQueriedUsers(validQuery, page, pageLimit);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<BadRequestResult>(result);
        var badRequestResult = result as BadRequestResult;
        Assert.IsNotNull(badRequestResult);
        Assert.AreEqual(400, badRequestResult.StatusCode);
    }
    
    [TestMethod]
    public async Task Queried_users_endpoint_returns_Badrequest_on_missing_or_empty_query_parameter()
    {
        // Arrange
        string invalidQuery = "    ";
        string invalidQuery2 = "";
        int page = 1;
        int pageLimit = 15;

        // Act
        var result = await _authController.GetQueriedUsers(invalidQuery, page, pageLimit);
        var result2 = await _authController.GetQueriedUsers(invalidQuery2, page, pageLimit);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result2);
        Assert.IsInstanceOfType<BadRequestResult>(result);
        Assert.IsInstanceOfType<BadRequestResult>(result2);
        var badRequestResult = result as BadRequestResult;
        var badRequestResult2 = result2 as BadRequestResult;
        Assert.IsNotNull(badRequestResult);
        Assert.AreEqual(400, badRequestResult.StatusCode);
        Assert.IsNotNull(badRequestResult2);
        Assert.AreEqual(400, badRequestResult2.StatusCode);
        
    }
    
    [TestMethod]
    public async Task Queried_users_endpoint_returns_Conflict_on_server_down()
    {
        // Arrange
        string validQuery = "John Pork Davidson";
        int page = 1;
        int pageLimit = 2;
        _userRepository.Setup(repo => repo.GetQueriedUsers(validQuery, page, pageLimit))
            .ReturnsAsync((List<User>)null!);

        // Act
        var result = await _authController.GetQueriedUsers(validQuery, page, pageLimit);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ConflictResult>(result);
        var conflictResult = result as ConflictResult;
        Assert.IsNotNull(conflictResult);
        Assert.AreEqual(409, conflictResult.StatusCode);
        
    }
    
    // Get user by id endpoint tests
    //
    //
    
    [TestMethod]
    public async Task Get_user_by_id_returns_OK_object_result()
    {
        // Arrange
        var userId = 46;
        _userRepository.Setup(repo => repo.GetUserByUserIdAsync(userId))
            .ReturnsAsync(new User(){UserId = userId, FirstName = "Brian", LastName = "Hugney", Email = "hugney@something.com", Password = "Placeholder", PhoneNumber = "78964655"});
        
        // Act
        var result = await _authController.GetUser(userId);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var okObjectResult = result as OkObjectResult;
        
        Assert.IsNotNull(okObjectResult);
        Assert.AreEqual(200, okObjectResult.StatusCode);
        var user = okObjectResult.Value as User;
        Assert.IsNotNull(user);
        Assert.AreEqual(userId, user.UserId);
    }
    
    [TestMethod]
    public async Task Get_user_by_id_returns_BadRequest_with_negative_or_zero_id()
    {
        // Arrange
        var negativeId = -1;
        var zeroUserId = 0;
        
        // Act
        var negativeResult = await _authController.GetUser(negativeId);
        var zeroResult = await _authController.GetUser(zeroUserId);
        
        // Assert
        Assert.IsNotNull(zeroResult);
        Assert.IsNotNull(negativeResult);
        Assert.IsInstanceOfType<BadRequestResult>(zeroResult);
        Assert.IsInstanceOfType<BadRequestResult>(negativeResult);
        
        var negativeObjectResult = negativeResult as BadRequestResult;
        var zeroObjectResult = zeroResult as BadRequestResult;
        Assert.IsNotNull(negativeObjectResult);
        Assert.IsNotNull(zeroObjectResult);
        Assert.AreEqual(400, negativeObjectResult.StatusCode);
        Assert.AreEqual(400, zeroObjectResult.StatusCode);
    }
    
    [TestMethod]
    public async Task Get_user_by_id_does_NOT_return_user_password_or_db_password()
    {
        // Arrange
        var userId = 785;
        _userRepository.Setup(repo => repo.GetUserByUserIdAsync(userId))
            .ReturnsAsync(new User(){UserId = userId, Password = "Placeholder", PhoneNumber = "78964655", FirstName = "Megan", LastName = "Fox", Email = "meg@gmail.com"});
        
        // Act
        var result = await _authController.GetUser(userId);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<OkObjectResult>(result);
        var okObjectResult = result as OkObjectResult;
        Assert.IsNotNull(okObjectResult);
        Assert.AreEqual(200, okObjectResult.StatusCode);
        var user = okObjectResult.Value as User;
        
        Assert.IsNull(user!.Password);
    }
    
    [TestMethod]
    public async Task Get_user_by_id_returns_NotFound_result_when_user_not_existing()
    {
        // Arrange
        var nonExistingUserId = 8654;
        _userRepository.Setup(repo => repo.GetUserByUserIdAsync(nonExistingUserId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _authController.GetUser(nonExistingUserId);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<NotFoundResult>(result);
    }
    
    // Update user endpoint tests
    //
    //
    
    [TestMethod]
    public async Task Update_user_returns_OK_result()
    {
        // Arrange
        var userId = 78642;
        var newUser = new ProfileUser()
        {
            FirstName = "Brian",
            LastName = "Hugney",
            Email = "brian@email.com",
            PhoneNumber = "84576215",
            UserId = userId,
        };
        _userRepository.Setup(repo => repo.UpdateUser(newUser))
            .ReturnsAsync(userId);
        
        // Act
        // TODO change to use JWT token
        _authController.HttpContext.Request.Headers["Authorization"] = "Bearer " + newUser.UserId;
        var result = await _authController.UpdateUser(newUser);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<OkResult>(result);
    }
    
    [TestMethod]
    public async Task Update_user_returns_unauthorized_result_when_updating_other_user()
    {
        // Arrange
        var newUser = new ProfileUser()
        {
            FirstName = "Brian",
            LastName = "Hugney",
            Email = "brian@email.com",
            PhoneNumber = "84576215",
            UserId = 78642,
        };

        // Act

        // Assert

    }
}