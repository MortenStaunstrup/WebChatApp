using System.Net;
using ChatAppAPI.Repositories.Interfaces;
using ChatAppAPI.Token;
using Core;
using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;

namespace ChatAppAPI.Controllers;

[ApiController]
[EnableRateLimiting("UserBasedPolicy")]
[Route("api/conversations")]
[Authorize]
public class ConversationController : ControllerBase
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserRepository _userRepository;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<ConversationController> _logger;
    
    public ConversationController(
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        TokenProvider tokenProvider,
        ILogger<ConversationController> logger)
    {
        _logger = logger;
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _tokenProvider = tokenProvider;
    }

    private int DecodeAuthHeader(StringValues authHeader, out string token)
    {
        token = string.Empty;
        var tokenWithBearer = authHeader[0];
        if (string.IsNullOrEmpty(tokenWithBearer))
        {
            _logger.LogWarning("Authorization header was missing or empty");
            return 0;
        }

        token = tokenWithBearer.Remove(0, 7);
        return 1;
    }
    
    [HttpGet]
    [Authorize]
    [Route("getConversations/{userId:int}/{limit:int}/{page:int}")]
    public async Task<IActionResult> GetConversationsAsync(int userId, int limit, int page)
    {
        _logger.LogInformation(
            "GetConversationsAsync endpoint called for user {UserId} with limit {Limit} and page {Page}",
            userId, limit, page);

        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);
        if (decodeResult == 0)
        {
            _logger.LogWarning(
                "GetConversationsAsync failed, could not decode authorization header for user {UserId}",
                userId);
            return BadRequest();
        }
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
        {
            _logger.LogWarning(
                "GetConversationsAsync failed, token did not contain a valid user id for requested user {UserId}",
                userId);
            return BadRequest();
        }
        
        // Make sure user Token corresponds to userId
        if (userId != authId)
        {
            _logger.LogWarning(
                "GetConversationsAsync unauthorized, token user id {AuthUserId} does not match requested user id {RequestedUserId}",
                authId, userId);
            return Unauthorized();
        }
        
        var conversations = await _conversationRepository.GetConversationsAsync(userId, limit, page);
        if (conversations == null)
        {
            _logger.LogWarning(
                "GetConversationsAsync failed, repository returned null for user {UserId}",
                userId);
            return Conflict();
        }

        if (conversations.Count == 0)
        {
            _logger.LogInformation(
                "GetConversationsAsync returned no content for user {UserId}",
                userId);
            return NoContent();
        }

        List<int> userIds = new List<int>();

        foreach (var conversation in conversations)
        {
            if (conversation.PersonAId == userId)
                userIds.Add(conversation.PersonBId);
            else
                userIds.Add(conversation.PersonAId);
        }

        _logger.LogInformation(
            "GetConversationsAsync retrieved {ConversationCount} conversations for user {UserId}, resolving associated users",
            conversations.Count, userId);
        
        var users = await _userRepository.GetUsersForConversationByUserIdAsync(userIds);

        if (users == null || users.Count < conversations.Count)
        {
            _logger.LogWarning(
                "GetConversationsAsync failed while resolving users for user {UserId}. User count: {UserCount}, conversation count: {ConversationCount}",
                userId, users?.Count ?? 0, conversations.Count);
            return Conflict();
        }
        
        ConversationsUsersContainer container = new ConversationsUsersContainer
        {
            Users = users,
            Conversations = conversations
        };
        
        if (conversations.Count < limit || conversations.Count == limit)
        {
            _logger.LogInformation(
                "GetConversationsAsync returning accepted for user {UserId} with {ConversationCount} conversations",
                userId, conversations.Count);
            return Accepted(container);
        }

        container.Conversations = container.Conversations.GetRange(0, limit);
        container.Users = container.Users.GetRange(0, limit);

        _logger.LogInformation(
            "GetConversationsAsync returning ok for user {UserId} with truncated result set of {Limit} conversations from total {ConversationCount}",
            userId, limit, conversations.Count);

        return Ok(container);
    }
    
    [HttpPost]
    [Authorize]
    [Route("update")]
    public async Task<IActionResult> UpdateConversation(Conversation conversation)
    {
        _logger.LogInformation(
            "UpdateConversation endpoint called for conversation {ConversationId}",
            conversation?.ConversationId);

        if (conversation == null)
        {
            _logger.LogWarning("UpdateConversation failed, conversation payload was null");
            return BadRequest();
        }

        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);
        if (decodeResult == 0)
        {
            _logger.LogWarning(
                "UpdateConversation failed, could not decode authorization header for conversation {ConversationId}",
                conversation.ConversationId);
            return BadRequest();
        }
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
        {
            _logger.LogWarning(
                "UpdateConversation failed, token did not contain a valid user id for conversation {ConversationId}",
                conversation.ConversationId);
            return BadRequest();
        }
        
        // Make sure user Token corresponds to either person A or B in conversation
        if (authId != conversation.PersonAId && authId != conversation.PersonBId)
        {
            _logger.LogWarning(
                "UpdateConversation unauthorized, token user id {AuthUserId} does not match conversation participants {PersonAId} or {PersonBId} for conversation {ConversationId}",
                authId, conversation.PersonAId, conversation.PersonBId, conversation.ConversationId);
            return Unauthorized();
        }
        
        conversation.Timestamp = DateTime.UtcNow;

        _logger.LogInformation(
            "Updating conversation {ConversationId} for participants {PersonAId} and {PersonBId}",
            conversation.ConversationId, conversation.PersonAId, conversation.PersonBId);

        var result = await _conversationRepository.UpdateConversation(conversation);
        if (result.ConversationId == 0)
        {
            _logger.LogWarning(
                "UpdateConversation failed, repository returned invalid conversation id for conversation {ConversationId}",
                conversation.ConversationId);
            return BadRequest();
        }

        _logger.LogInformation(
            "UpdateConversation succeeded for conversation {ConversationId}",
            result.ConversationId);
        return Ok(result);
    }
    
    [HttpPost]
    [Authorize]
    [Route("updateSeenStatus")]
    public async Task<IActionResult> UpdateConversationSeenStatus(Conversation conversation)
    {
        _logger.LogInformation(
            "UpdateConversationSeenStatus endpoint called for conversation {ConversationId}",
            conversation?.ConversationId);

        if (conversation == null)
        {
            _logger.LogWarning("UpdateConversationSeenStatus failed, conversation payload was null");
            return BadRequest();
        }

        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);
        if (decodeResult == 0)
        {
            _logger.LogWarning(
                "UpdateConversationSeenStatus failed, could not decode authorization header for conversation {ConversationId}",
                conversation.ConversationId);
            return BadRequest();
        }
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
        {
            _logger.LogWarning(
                "UpdateConversationSeenStatus failed, token did not contain a valid user id for conversation {ConversationId}",
                conversation.ConversationId);
            return BadRequest();
        }
        
        // Make sure user Token corresponds to either person A or B in conversation
        if (authId != conversation.PersonAId && authId != conversation.PersonBId)
        {
            _logger.LogWarning(
                "UpdateConversationSeenStatus unauthorized, token user id {AuthUserId} does not match conversation participants {PersonAId} or {PersonBId} for conversation {ConversationId}",
                authId, conversation.PersonAId, conversation.PersonBId, conversation.ConversationId);
            return Unauthorized();
        }
        
        var result = await _conversationRepository.UpdateConversationSeenStatus(conversation);
        if (result.ConversationId == 0)
        {
            _logger.LogWarning(
                "UpdateConversationSeenStatus failed, repository returned invalid conversation id for conversation {ConversationId}",
                conversation.ConversationId);
            return BadRequest();
        }

        _logger.LogInformation(
            "UpdateConversationSeenStatus succeeded for conversation {ConversationId}",
            result.ConversationId);
        return Ok(result);
    }
    
    [HttpGet]
    [Authorize]
    [Route("get/{userId:int}/{otherPersonId:int}")]
    public async Task<IActionResult> GetConversation(int userId, int otherPersonId)
    {
        _logger.LogInformation(
            "GetConversation endpoint called for user {UserId} and other person {OtherPersonId}",
            userId, otherPersonId);

        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);
        if (decodeResult == 0)
        {
            _logger.LogWarning(
                "GetConversation failed, could not decode authorization header for user {UserId} and other person {OtherPersonId}",
                userId, otherPersonId);
            return BadRequest();
        }
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
        {
            _logger.LogWarning(
                "GetConversation failed, token did not contain a valid user id for requested user {UserId}",
                userId);
            return BadRequest();
        }
        
        // Make sure user Token corresponds to userId
        if (authId != userId)
        {
            _logger.LogWarning(
                "GetConversation unauthorized, token user id {AuthUserId} does not match requested user id {RequestedUserId}",
                authId, userId);
            return Unauthorized();
        }
        
        var result = await _conversationRepository.GetConversation(userId, otherPersonId);
        if (result == null)
        {
            _logger.LogWarning(
                "GetConversation did not find conversation for user {UserId} and other person {OtherPersonId}",
                userId, otherPersonId);
            return NotFound();
        }
        
        var user = await _userRepository.GetUserByUserIdAsync(otherPersonId);
        if (user == null)
        {
            _logger.LogWarning(
                "GetConversation found conversation but did not find other user {OtherPersonId} for user {UserId}",
                otherPersonId, userId);
            return NotFound();
        }

        var container = new ConversationUserContainer()
        {
            User = user,
            Conversation = result
        };

        _logger.LogInformation(
            "GetConversation succeeded for user {UserId} and other person {OtherPersonId}, conversation {ConversationId}",
            userId, otherPersonId, result.ConversationId);
        
        return Ok(container);
    }
    
    [HttpPost]
    [Authorize]
    [Route("create")]
    public async Task<IActionResult> CreateConversation(Conversation conversation)
    {
        _logger.LogInformation(
            "CreateConversation endpoint called for participants {PersonAId} and {PersonBId}",
            conversation?.PersonAId, conversation?.PersonBId);

        if (conversation == null)
        {
            _logger.LogWarning("CreateConversation failed, conversation payload was null");
            return BadRequest();
        }

        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);
        if (decodeResult == 0)
        {
            _logger.LogWarning(
                "CreateConversation failed, could not decode authorization header for participants {PersonAId} and {PersonBId}",
                conversation.PersonAId, conversation.PersonBId);
            return BadRequest();
        }
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
        {
            _logger.LogWarning(
                "CreateConversation failed, token did not contain a valid user id for participants {PersonAId} and {PersonBId}",
                conversation.PersonAId, conversation.PersonBId);
            return BadRequest();
        }
        
        // Make sure user Token corresponds to either person A or B
        if (authId != conversation.PersonAId && authId != conversation.PersonBId)
        {
            _logger.LogWarning(
                "CreateConversation unauthorized, token user id {AuthUserId} does not match participants {PersonAId} or {PersonBId}",
                authId, conversation.PersonAId, conversation.PersonBId);
            return Unauthorized();
        }
        
        conversation.Timestamp = DateTime.UtcNow;

        _logger.LogInformation(
            "Creating conversation for participants {PersonAId} and {PersonBId}",
            conversation.PersonAId, conversation.PersonBId);

        var result = await _conversationRepository.CreateConversation(conversation);
        if (result.ConversationId == 0)
        {
            _logger.LogWarning(
                "CreateConversation failed, repository returned invalid conversation id for participants {PersonAId} and {PersonBId}",
                conversation.PersonAId, conversation.PersonBId);
            return BadRequest();
        }

        _logger.LogInformation(
            "CreateConversation succeeded with conversation {ConversationId} for participants {PersonAId} and {PersonBId}",
            result.ConversationId, result.PersonAId, result.PersonBId);
        return Ok(result);
    }
}