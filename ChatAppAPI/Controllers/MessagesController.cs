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
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
     private readonly IMessagesRepository _messagesRepository;
     private readonly TokenProvider _tokenProvider;
     private readonly ILogger<MessagesController> _logger;

     public MessagesController(
          IMessagesRepository messagesRepository,
          TokenProvider tokenProvider,
          ILogger<MessagesController> logger)
     {
          _messagesRepository = messagesRepository;
          _tokenProvider = tokenProvider;
          _logger = logger;
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
     [Route("getMessages/{currentUserId:int}/{otherUserId:int}/{limit:int}/{page:int}")]
     public async Task<IActionResult> GetMessages(int currentUserId, int otherUserId, int limit, int page)
     {
          _logger.LogInformation(
               "GetMessages endpoint called for current user {CurrentUserId}, other user {OtherUserId}, limit {Limit}, page {Page}",
               currentUserId, otherUserId, limit, page);

          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
          {
               _logger.LogWarning(
                    "GetMessages failed, could not decode authorization header for current user {CurrentUserId}",
                    currentUserId);
               return BadRequest();
          }
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
          {
               _logger.LogWarning(
                    "GetMessages failed, token did not contain a valid user id for current user {CurrentUserId}",
                    currentUserId);
               return BadRequest();
          }
          
          // Make sure user Token corresponds to either sender or receiver
          if (authId != currentUserId && authId != otherUserId)
          {
               _logger.LogWarning(
                    "GetMessages unauthorized, token user id {AuthUserId} does not match current user {CurrentUserId} or other user {OtherUserId}",
                    authId, currentUserId, otherUserId);
               return Unauthorized();
          }
          
          var result = await _messagesRepository.GetMessages(currentUserId, otherUserId, limit, page);
          if (result == null)
          {
               _logger.LogWarning(
                    "GetMessages failed, repository returned null for current user {CurrentUserId} and other user {OtherUserId}",
                    currentUserId, otherUserId);
               return Conflict();
          }

          if (result.Count == 0 && page == 0)
          {
               _logger.LogInformation(
                    "GetMessages found no messages on first page for current user {CurrentUserId} and other user {OtherUserId}",
                    currentUserId, otherUserId);
               return NotFound();
          }

          if (result.Count == 0)
          {
               _logger.LogInformation(
                    "GetMessages returned no content for current user {CurrentUserId}, other user {OtherUserId}, page {Page}",
                    currentUserId, otherUserId, page);
               return NoContent();
          }

          if (result.Count < limit || result.Count == limit)
          {
               _logger.LogInformation(
                    "GetMessages returning accepted with {Count} messages for current user {CurrentUserId} and other user {OtherUserId}",
                    result.Count, currentUserId, otherUserId);
               return Accepted(result);
          }
          
          _logger.LogInformation(
               "GetMessages returning ok with truncated result set of {Limit} messages from total {Count} for current user {CurrentUserId} and other user {OtherUserId}",
               limit, result.Count, currentUserId, otherUserId);

          return Ok(result.GetRange(0, limit));
     }
     
     [HttpGet]
     [Route("getSentMessage/{messageId:int}")]
     public async Task<IActionResult> GetSentMessage(int messageId)
     {
          _logger.LogInformation(
               "GetSentMessage endpoint called for message {MessageId}",
               messageId);

          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
          {
               _logger.LogWarning(
                    "GetSentMessage failed, could not decode authorization header for message {MessageId}",
                    messageId);
               return BadRequest();
          }
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
          {
               _logger.LogWarning(
                    "GetSentMessage failed, token did not contain a valid user id for message {MessageId}",
                    messageId);
               return BadRequest();
          }
          
          var result = await _messagesRepository.GetSentMessage(messageId);

          if (result != null)
          {
               // Make sure user Token corresponds to either sender or receiver
               if (result.Sender != authId && result.Receiver != authId)
               {
                    _logger.LogWarning(
                         "GetSentMessage unauthorized, token user id {AuthUserId} does not match sender {SenderId} or receiver {ReceiverId} for message {MessageId}",
                         authId, result.Sender, result.Receiver, messageId);
                    return Unauthorized();
               }

               _logger.LogInformation(
                    "GetSentMessage succeeded for message {MessageId}",
                    messageId);
               return Ok(result);
          }

          _logger.LogWarning(
               "GetSentMessage failed, repository returned null for message {MessageId}",
               messageId);
          return Conflict();
     }
     
     [HttpPost]
     [Route("sendFile")]
     public async Task<IActionResult> SendFile(MessageFileContainer container)
     {
          _logger.LogInformation(
               "SendFile endpoint called for sender {SenderId} and file {FileName}",
               container?.SenderId, container?.FileName);

          if (container == null)
          {
               _logger.LogWarning("SendFile failed, container payload was null");
               return BadRequest();
          }

          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
          {
               _logger.LogWarning(
                    "SendFile failed, could not decode authorization header for sender {SenderId}",
                    container.SenderId);
               return BadRequest();
          }
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
          {
               _logger.LogWarning(
                    "SendFile failed, token did not contain a valid user id for sender {SenderId}",
                    container.SenderId);
               return BadRequest();
          }
          
          // Make sure user Token corresponds to sender
          if (authId != container.SenderId)
          {
               _logger.LogWarning(
                    "SendFile unauthorized, token user id {AuthUserId} does not match sender id {SenderId}",
                    authId, container.SenderId);
               return Unauthorized();
          }
          
          var result = await _messagesRepository.UploadFile(container.FileName, container.File);
          if (!string.IsNullOrWhiteSpace(result))
          {
               _logger.LogInformation(
                    "SendFile succeeded for sender {SenderId} and file {FileName}",
                    container.SenderId, container.FileName);
               return Ok(result);
          }

          _logger.LogWarning(
               "SendFile failed, repository returned empty result for sender {SenderId} and file {FileName}",
               container.SenderId, container.FileName);
          return BadRequest();
     }
     
     [HttpGet]
     [Route("getFile/{messageId:int}")]
     public async Task<IActionResult> GetFile(int messageId)
     {
          _logger.LogInformation(
               "GetFile endpoint called for message {MessageId}",
               messageId);

          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
          {
               _logger.LogWarning(
                    "GetFile failed, could not decode authorization header for message {MessageId}",
                    messageId);
               return BadRequest();
          }
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
          {
               _logger.LogWarning(
                    "GetFile failed, token did not contain a valid user id for message {MessageId}",
                    messageId);
               return BadRequest();
          }
          
          var message = await _messagesRepository.GetSentMessage(messageId);
          if (message == null)
          {
               _logger.LogWarning(
                    "GetFile failed, message {MessageId} was not found",
                    messageId);
               return BadRequest();
          }
          
          // Make sure user Token corresponds to either sender or receiver
          if (message.Sender != authId && message.Receiver != authId)
          {
               _logger.LogWarning(
                    "GetFile unauthorized, token user id {AuthUserId} does not match sender {SenderId} or receiver {ReceiverId} for message {MessageId}",
                    authId, message.Sender, message.Receiver, messageId);
               return Unauthorized();
          }
          
          var result = await _messagesRepository.DownloadFile(messageId);
          if (result != null)
          {
               _logger.LogInformation(
                    "GetFile succeeded for message {MessageId}",
                    messageId);
               return Ok(result);
          }

          _logger.LogWarning(
               "GetFile failed, repository returned null for message {MessageId}",
               messageId);
          return Conflict();
     }
     
     [HttpPost]
     [Route("createMessage")]
     public async Task<IActionResult> CreateMessage(Message message)
     {
          _logger.LogInformation(
               "CreateMessage endpoint called for sender {SenderId} and receiver {ReceiverId}",
               message?.Sender, message?.Receiver);

          if (message == null)
          {
               _logger.LogWarning("CreateMessage failed, message payload was null");
               return BadRequest();
          }

          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
          {
               _logger.LogWarning(
                    "CreateMessage failed, could not decode authorization header for sender {SenderId}",
                    message.Sender);
               return BadRequest();
          }
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
          {
               _logger.LogWarning(
                    "CreateMessage failed, token did not contain a valid user id for sender {SenderId}",
                    message.Sender);
               return BadRequest();
          }
          
          // Make sure user Token corresponds to either sender or receiver
          if (message.Sender != authId)
          {
               _logger.LogWarning(
                    "CreateMessage unauthorized, token user id {AuthUserId} does not match sender id {SenderId}",
                    authId, message.Sender);
               return Unauthorized();
          }
          
          message.Timestamp = DateTime.UtcNow;
          
          var result = await _messagesRepository.SendMessage(message);
          if (result != 0)
          {
               _logger.LogInformation(
                    "CreateMessage succeeded with message id {MessageId} for sender {SenderId} and receiver {ReceiverId}",
                    result, message.Sender, message.Receiver);
               return Ok(result);
          }

          _logger.LogWarning(
               "CreateMessage failed, repository returned 0 for sender {SenderId} and receiver {ReceiverId}",
               message.Sender, message.Receiver);
          return Conflict();
     }
     
     [HttpPost]
     [Route("updateSeen")]
     public async Task<IActionResult> UpdateSeenStatus(List<Message> messages)
     {
          _logger.LogInformation(
               "UpdateSeenStatus endpoint called for {Count} messages",
               messages?.Count ?? 0);

          if (messages == null || messages.Count == 0)
               return NoContent();

          Request.Headers.TryGetValue("Authorization", out var values);
          int decodeResult = DecodeAuthHeader(values, out string token);
          if (decodeResult == 0)
          {
               _logger.LogWarning("UpdateSeenStatus failed, could not decode authorization header");
               return BadRequest();
          }
        
          var authId = _tokenProvider.GetUserId(token);
          if (authId < 1)
          {
               _logger.LogWarning("UpdateSeenStatus failed, token did not contain a valid user id");
               return BadRequest();
          }

          if (messages[0].Sender != authId && messages[0].Receiver != authId)
          {
               _logger.LogWarning("UpdateSeenStatus failed, user chat was not updated by one of the users in the conversation");
               return Unauthorized();
          }
          
          var result = await _messagesRepository.UpdateSeenStatus(messages);
          if (result)
          {
               _logger.LogInformation(
                    "UpdateSeenStatus succeeded for user {AuthUserId} on {Count} messages",
                    authId, messages.Count);
               return Ok();
          }

          _logger.LogWarning(
               "UpdateSeenStatus failed for user {AuthUserId} on {Count} messages",
               authId, messages.Count);
          return BadRequest();
     }
}