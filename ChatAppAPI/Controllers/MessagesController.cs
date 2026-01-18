using ChatAppAPI.Repositories.Interfaces;
using ChatAppAPI.Token;
using Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace ChatAppAPI.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
     private readonly IMessagesRepository _messagesRepository;
     private readonly TokenProvider _tokenProvider;

     public MessagesController(IMessagesRepository messagesRepository, TokenProvider tokenProvider)
     {
          _messagesRepository = messagesRepository;
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
     [Route("getMessages/{currentUserId:int}/{otherUserId:int}/{limit:int}/{page:int}")]
     public async Task<IActionResult> GetMessages(int currentUserId, int otherUserId, int limit, int page)
     {
          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
               return BadRequest();
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
               return BadRequest();
          
          // Make sure user Token corresponds to either sender or receiver
          if (authId != currentUserId && authId != otherUserId)
               return Unauthorized();
          
          var result = await _messagesRepository.GetMessages(currentUserId, otherUserId, limit, page);
          if (result == null)
               return Conflict();
          if (result.Count == 0 && page == 0)
               return NotFound();
          if (result.Count == 0)
               return NoContent();
          if (result.Count < limit || result.Count == limit)
               return Accepted(result);
          
          return Ok(result.GetRange(0, limit));
          
     }

     [HttpGet]
     [Route("getSentMessage/{messageId:int}")]
     public async Task<IActionResult> GetSentMessage(int messageId)
     {
          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
               return BadRequest();
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
               return BadRequest();
          
          var result = await _messagesRepository.GetSentMessage(messageId);

          if (result != null)
          {
               // Make sure user Token corresponds to either sender or receiver
               if (result.Sender != authId && result.Receiver != authId)
                    return Unauthorized();
               return Ok(result);
          }
          return Conflict();
     }

     [HttpPost]
     [Route("sendFile")]
     public async Task<IActionResult> SendFile(MessageFileContainer container)
     {
          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
               return BadRequest();
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
               return BadRequest();
          
          // Make sure user Token corresponds to sender
          if (authId != container.SenderId)
               return Unauthorized();
          
          var result = await _messagesRepository.UploadFile(container.FileName, container.SenderId, container.File);
          if (!string.IsNullOrWhiteSpace(result))
               return Ok(result);
          return BadRequest();
     }

     [HttpGet]
     [Route("getFile{messageId:int}")]
     public async Task<IActionResult> GetFile(int messageId)
     {
          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
               return BadRequest();
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
               return BadRequest();
          
          var message = await _messagesRepository.GetSentMessage(messageId);
          if (message == null)
               return BadRequest();
          
          // Make sure user Token corresponds to either sender or receiver
          if (message.Sender != authId && message.Receiver != authId)
               return Unauthorized();
          
          var result = await _messagesRepository.DownloadFile(messageId);
          if (result != null)
          {
               return Ok(result);
          }
          return Conflict();
     }

     [HttpPost]
     [Route("createMessage")]
     public async Task<IActionResult> CreateMessage(Message message)
     {
          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
               return BadRequest();
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
               return BadRequest();
          
          // Make sure user Token corresponds to either sender or receiver
          if (message.Sender != authId)
               return Unauthorized();
          
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
          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
               return BadRequest();
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
               return BadRequest();
          
          var result = await _messagesRepository.UpdateSeenStatus(messages);
          if(result)
               return Ok();
          return BadRequest();
     }
     
}