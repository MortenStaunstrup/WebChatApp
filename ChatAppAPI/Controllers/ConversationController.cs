using System.Net;
using ChatAppAPI.Repositories.Interfaces;
using ChatAppAPI.Token;
using Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace ChatAppAPI.Controllers;


[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationController : ControllerBase
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserRepository _userRepository;
    private readonly TokenProvider _tokenProvider;

    public ConversationController(IConversationRepository conversationRepository,  IUserRepository userRepository, TokenProvider tokenProvider)
    {
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _tokenProvider = tokenProvider;
    }

    private int DecodeAuthHeader(StringValues authHeader, out string token)
    {
        token = string.Empty;
        var tokenWithBearer = authHeader[0];
        if (string.IsNullOrEmpty(tokenWithBearer))
            return 0;
        token = tokenWithBearer.Remove(0, 7);
        return 1;
    }

    [HttpGet]
    [Authorize]
    [Route("getConversations/{userId:int}/{limit:int}/{page:int}")]
    public async Task<IActionResult> GetConversationsAsync(int userId, int limit, int page)
    {
        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);
        if (decodeResult == 0)
            return BadRequest();
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
            return BadRequest();
        
        // Make sure user Token corresponds to userId
        if (userId != authId)
            return Unauthorized();
        
        var conversations = await _conversationRepository.GetConversationsAsync(userId, limit, page);
        if (conversations == null)
            return Conflict();
        if(conversations.Count == 0)
            return NoContent();

        List<int> userIds = new List<int>();

        foreach (var conversation in conversations)
        {
            if(conversation.PersonAId == userId)
                userIds.Add(conversation.PersonBId);
            else
                userIds.Add(conversation.PersonAId);
        }
        
        var users = await _userRepository.GetUsersForConversationByUserIdAsync(userIds);

        if (users == null || users.Count < conversations.Count)
            return Conflict();
        
        
        ConversationsUsersContainer container = new ConversationsUsersContainer
        {
            Users = users,
            Conversations = conversations
        };
        
        if (conversations.Count < limit || conversations.Count == limit)
            return Accepted(container);

        container.Conversations = container.Conversations.GetRange(0, limit);
        container.Users = container.Users.GetRange(0, limit);
        return Ok(container);

    }
    
    
    [HttpPost]
    [Authorize]
    [Route("update")]
    public async Task<IActionResult> UpdateConversation(Conversation conversation)
    {
        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);
        if (decodeResult == 0)
            return BadRequest();
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
            return BadRequest();
        
        // Make sure user Token corresponds to either person A or B in conversation
        if (authId != conversation.PersonAId && authId != conversation.PersonBId)
            return Unauthorized();
        
        conversation.Timestamp = DateTime.UtcNow;
        var result = await _conversationRepository.UpdateConversation(conversation);
        if(result.ConversationId == 0)
            return BadRequest();
        return Ok(result);
    }

    [HttpPost]
    [Authorize]
    [Route("updateSeenStatus")]
    public async Task<IActionResult> UpdateConversationSeenStatus(Conversation conversation)
    {
        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);
        if (decodeResult == 0)
            return BadRequest();
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
            return BadRequest();
        
        // Make sure user Token corresponds to either person A or B in conversation
        if (authId != conversation.PersonAId && authId != conversation.PersonBId)
            return Unauthorized();
        
        var result = await _conversationRepository.UpdateConversationSeenStatus(conversation);
        if(result.ConversationId == 0)
            return BadRequest();
        return Ok(result);
    }

    [HttpGet]
    [Authorize]
    [Route("get/{userId:int}/{otherPersonId:int}")]
    public async Task<IActionResult> GetConversation(int userId, int otherPersonId)
    {
        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);
        if (decodeResult == 0)
            return BadRequest();
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
            return BadRequest();
        
        // Make sure user Token corresponds to userId
        if (authId != userId)
            return Unauthorized();
        
        var result = await _conversationRepository.GetConversation(userId, otherPersonId);
        if (result == null)
            return NotFound();
        
        var user = await _userRepository.GetUserByUserIdAsync(otherPersonId);
        if (user == null)
            return NotFound();

        var container = new ConversationUserContainer()
        {
            User = user,
            Conversation = result
        };
        
        return Ok(container);
    }


    [HttpPost]
    [Authorize]
    [Route("create")]
    public async Task<IActionResult> CreateConversation(Conversation conversation)
    {
        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);
        if (decodeResult == 0)
            return BadRequest();
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
            return BadRequest();
        
        // Make sure user Token corresponds to either person A or B
        if (authId != conversation.PersonAId && authId != conversation.PersonBId)
            return Unauthorized();
        
        conversation.Timestamp = DateTime.UtcNow;
        var result = await _conversationRepository.CreateConversation(conversation);
        if (result.ConversationId == 0)
            return BadRequest();
        return Ok(result);
    }
    
    
}