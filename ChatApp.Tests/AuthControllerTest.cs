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
        var result = await _authController.TryLogin(creds);
        
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
        var result = await _authController.LoginWithRefreshToken(0, null);
        
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
    
}