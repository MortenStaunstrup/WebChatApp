using ChatAppAPI.Repositories.Interfaces;
using Core;
using Microsoft.AspNetCore.Mvc;

namespace ChatAppAPI.Controllers;

[ApiController]
[Route("api/messages")]
public class MessagesController : ControllerBase
{
     private readonly IMessagesRepository _messagesRepository;

     public MessagesController(IMessagesRepository messagesRepository)
     {
          _messagesRepository = messagesRepository;
     }

     [HttpGet]
     [Route("getMessages/{currentUserId:int}/{otherUserId:int}")]
     public async Task<IActionResult> GetMessages(int currentUserId, int otherUserId)
     {
          var result = await _messagesRepository.GetMessages(currentUserId, otherUserId);
          if (result != null && result.Count > 0)
               return Ok(result);
          return Conflict();
     }

     [HttpGet]
     [Route("getSentMessage/{messageId:int}")]
     public async Task<IActionResult> GetSentMessage(int messageId)
     {
          var result = await _messagesRepository.GetSentMessage(messageId);
          if (result != null)
               return Ok(result);
          return Conflict();
     }

     [HttpPost]
     [Route("createMessage")]
     public async Task<IActionResult> CreateMessage(Message message)
     {
          message.Timestamp = DateTime.UtcNow;
          
          var result = await _messagesRepository.SendMessage(message);
          if (result != 0)
               return Ok(result);
          return Conflict();
     }

     [HttpPost]
     [Route("updateSeen")]
     public async Task<IActionResult> UpdateSeenStatus(List<Message> messages)
     {
          var result = await _messagesRepository.UpdateSeenStatus(messages);
          if(result)
               return Ok();
          return BadRequest();
     }
     
}