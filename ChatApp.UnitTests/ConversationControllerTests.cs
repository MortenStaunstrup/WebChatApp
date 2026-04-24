using ChatAppAPI.Controllers;
using ChatAppAPI.Repositories.Interfaces;
using ChatAppAPI.Token;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ChatApp.Tests;

public class ConversationControllerTests
{
    private Mock<ILogger<ConversationController>> _loggerMock;
    private Mock<IUserRepository> _userRepository;
    private Mock<IConversationRepository> _conversationRepository;
    private ConversationController _conversationController;
    private TokenProvider _tokenProvider;
    
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
        
        _tokenProvider = new TokenProvider(configRoot);
        
        _userRepository = new Mock<IUserRepository>();
        _conversationRepository = new Mock<IConversationRepository>();
        
        _loggerMock = new Mock<ILogger<ConversationController>>();

        _conversationController = new ConversationController(
            _conversationRepository.Object,
            _userRepository.Object,
            _tokenProvider,
            _loggerMock.Object);
        
        _conversationController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }
}