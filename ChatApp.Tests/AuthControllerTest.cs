using ChatAppAPI.Controllers;
using Moq;
using ChatAppAPI.Repositories.Interfaces;
using ChatAppAPI.Token;
using Core;
using Core.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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
    
    [TestMethod]
    public async Task Login_returns_OK_response_with_valid_login_credentials()
    {
        // Arrange
        _userRepository.Setup(repo => repo.TryLogin("random@email.com", "password123"))
            .ReturnsAsync(new User(){Email = "random@email.com", FirstName = "Maddie", LastName = "Johnson", Password = "password123", PhoneNumber = "84652648", UserId = 4});
        _userRepository.Setup(repo => repo.CreateRefreshToken(It.IsAny<int>()))
            .ReturnsAsync("refresh-token-123");
        var creds = new LoginRecord("random@email.com", "password123");
        
        // Act
        var result = await _authController.TryLogin(creds);


        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<OkObjectResult>(result);
        
        var okObjectResult = (OkObjectResult)result;
        
        Assert.AreEqual(200, okObjectResult.StatusCode);
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
        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
        
        var unauthorizedObjectResult = (UnauthorizedObjectResult)result;
        
        Assert.AreEqual(401, unauthorizedObjectResult.StatusCode);
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
        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        
        var badRequestObjectResult = (BadRequestObjectResult)result;
        
        Assert.AreEqual(400, badRequestObjectResult.StatusCode);
    }
}