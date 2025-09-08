using ChatAppAPI.Repositories.Interfaces;
using Core;
using Microsoft.AspNetCore.Mvc;

namespace ChatAppAPI.Controllers;


[ApiController]
[Route("api/conversations")]
public class ConversationController : ControllerBase
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserRepository _userRepository;

    public ConversationController(IConversationRepository conversationRepository,  IUserRepository userRepository)
    {
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
    }


    [HttpGet]
    [Route("getConversations/{userId:int}")]
    public async Task<IActionResult> GetConversationsAsync(int userId)
    {
        var conversations = await _conversationRepository.GetConversationsAsync(userId);
        if (conversations == null || conversations.Count == 0)
            return NotFound();

        conversations = conversations.Where(c => !string.IsNullOrWhiteSpace(c.LastMessage)).ToList();
        conversations = conversations.OrderByDescending(o => o.Timestamp).ToList();

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
        
        return Ok(container);

    }
    
    
    [HttpPost]
    [Route("update")]
    public async Task<IActionResult> UpdateConversation(Conversation conversation)
    {
        conversation.Timestamp = DateTime.UtcNow;
        var result = await _conversationRepository.UpdateConversation(conversation);
        if(result.ConversationId == 0)
            return BadRequest();
        return Ok(result);
    }

    [HttpPost]
    [Route("updateSeenStatus")]
    public async Task<IActionResult> UpdateConversationSeenStatus(Conversation conversation)
    {
        var result = await _conversationRepository.UpdateConversationSeenStatus(conversation);
        if(result.ConversationId == 0)
            return BadRequest();
        return Ok(result);
    }

    [HttpGet]
    [Route("get/{userId:int}/{otherPersonId:int}")]
    public async Task<IActionResult> GetConversation(int userId, int otherPersonId)
    {
        var result = await _conversationRepository.GetConversation(userId, otherPersonId);
        if (result == null)
            return NotFound();
        return Ok(result);
    }


    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> CreateConversation(Conversation conversation)
    {
        conversation.Timestamp = DateTime.UtcNow;
        var result = await _conversationRepository.CreateConversation(conversation);
        if (result.ConversationId == 0)
            return BadRequest();
        return Ok(result);
    }
    
    
}