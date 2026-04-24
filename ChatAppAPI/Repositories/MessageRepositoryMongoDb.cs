using Azure;
using ChatAppAPI.Repositories.Interfaces;
using Core;
using Microsoft.AspNetCore.Connections;
using MongoDB.Driver;
using Azure.Storage.Files.Shares;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.IO;
using Azure.Identity;
using Azure.Storage;
using Core.DTOs;

namespace ChatAppAPI.Repositories;

public class MessageRepositoryMongoDb : IMessagesRepository
{
    private readonly IMongoCollection<Message> _messagesCollection;
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<MessageRepositoryMongoDb> _logger;

    public MessageRepositoryMongoDb(
        IMongoDatabase mongoDatabase,
        BlobContainerClient containerClient,
        ILogger<MessageRepositoryMongoDb> logger
        )
    {
        _messagesCollection = mongoDatabase.GetCollection<Message>("Messages");
        _containerClient = containerClient;
        _logger = logger;
    }
    
    public async Task<List<Message>?> GetMessages(int currentUserId, int otherUserId, int limit, int page)
    {
        _logger.LogDebug(
            "GetMessages called for current user {CurrentUserId}, other user {OtherUserId}, limit {Limit}, page {Page}",
            currentUserId, otherUserId, limit, page);

        var currFirstFilter = Builders<Message>.Filter.Eq("Sender", currentUserId);
        var currSecondFilter = Builders<Message>.Filter.Eq("Receiver", otherUserId);
        
        var currAndFilter = Builders<Message>.Filter.And(currFirstFilter, currSecondFilter);
        
        var otherFirstFilter = Builders<Message>.Filter.Eq("Sender", otherUserId);
        var otherSecondFilter = Builders<Message>.Filter.Eq("Receiver", currentUserId);
        
        var otherAndFilter = Builders<Message>.Filter.And(otherFirstFilter, otherSecondFilter);
        
        var finalFilter = Builders<Message>.Filter.Or(currAndFilter, otherAndFilter);
        
        try
        {
            // Extract 1 more to see if there are more messages after
            var list = await _messagesCollection
                .Find(finalFilter)
                .SortByDescending(m => m.Timestamp)
                .Skip(limit * page)
                .Limit(limit + 1)
                .ToListAsync();

            _logger.LogDebug(
                "GetMessages returned {Count} messages for current user {CurrentUserId} and other user {OtherUserId}",
                list.Count, currentUserId, otherUserId);

            return list;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "GetMessages failed for current user {CurrentUserId}, other user {OtherUserId}, limit {Limit}, page {Page}",
                currentUserId, otherUserId, limit, page);

            return null;
        }
    }

    public async Task<string> UploadFile(string fileName, byte[] file)
    {
        _logger.LogDebug(
            "UploadFile called for file {FileName} with size {FileSize}",
            fileName, file?.Length ?? 0);

        try
        {
            string blobName = fileName + Guid.NewGuid();
            BlobClient blobClient = _containerClient.GetBlobClient(blobName);

            var binData = BinaryData.FromBytes(file);

            await blobClient.UploadAsync(binData, true);

            _logger.LogInformation(
                "UploadFile succeeded for file {FileName}, stored as blob {BlobName}",
                fileName, blobName);

            return blobName;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "UploadFile failed for file {FileName}",
                fileName);

            return "";
        }
    }

    public async Task<ByteNameContainer?> DownloadFile(int messageId)
    {
        _logger.LogDebug(
            "DownloadFile called for message {MessageId}",
            messageId);

        var message = await GetSentMessage(messageId);
        if (message == null)
        {
            _logger.LogWarning(
                "DownloadFile failed because message {MessageId} was not found",
                messageId);

            return null;
        }
        
        BlobClient blobClient = _containerClient.GetBlobClient(message.FileURL);

        try
        {
            var download = await blobClient.DownloadAsync();
            if (download.HasValue)
            {
                using (var ms = new MemoryStream())
                {
                    await download.Value.Content.CopyToAsync(ms);

                    _logger.LogInformation(
                        "DownloadFile succeeded for message {MessageId} and blob {BlobName}",
                        messageId, message.FileURL);
                    
                    return new ByteNameContainer()
                    {
                        FileName = message.Content,
                        Bytes = ms.ToArray()
                    };
                }
            }

            _logger.LogWarning(
                "DownloadFile returned no content for message {MessageId} and blob {BlobName}",
                messageId, message.FileURL);

            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "DownloadFile failed for message {MessageId} and blob {BlobName}",
                messageId, message.FileURL);

            return null;
        }
    }

    public async Task<int> SendMessage(Message message)
    {
        _logger.LogDebug(
            "SendMessage called for sender {SenderId} and receiver {ReceiverId}",
            message.Sender, message.Receiver);

        message.MessageId = await GetMaxId() + 1;
        
        try
        {
            await _messagesCollection.InsertOneAsync(message);

            _logger.LogInformation(
                "SendMessage succeeded with message id {MessageId} for sender {SenderId} and receiver {ReceiverId}",
                message.MessageId, message.Sender, message.Receiver);

            return message.MessageId;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "SendMessage failed for sender {SenderId} and receiver {ReceiverId}",
                message.Sender, message.Receiver);

            return 0;
        }
    }

    public async Task<Message?> GetSentMessage(int messageId)
    {
        _logger.LogDebug(
            "GetSentMessage called for message {MessageId}",
            messageId);

        try
        {
            var filter = Builders<Message>.Filter.Eq("MessageId", messageId);
            var message = await _messagesCollection.Find(filter).FirstOrDefaultAsync();

            if (message == null)
            {
                _logger.LogDebug(
                    "GetSentMessage found no message for message id {MessageId}",
                    messageId);
            }
            else
            {
                _logger.LogDebug(
                    "GetSentMessage found message {MessageId}",
                    messageId);
            }

            return message;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "GetSentMessage failed for message {MessageId}",
                messageId);

            return null;
        }
    }

    public async Task<bool> UpdateSeenStatus(List<Message> messages)
    {
        _logger.LogDebug(
            "UpdateSeenStatus called for {Count} messages",
            messages?.Count ?? 0);

        try
        {
            foreach (var message in messages)
            {
                if (message.SeenByReceiver)
                {
                    _logger.LogDebug(
                        "Updating seen status for message {MessageId}",
                        message.MessageId);

                    var filter = Builders<Message>.Filter.Eq("_id", message.MessageId);
                    var update = Builders<Message>.Update.Set("SeenByReceiver", true);

                    await _messagesCollection.UpdateOneAsync(filter, update);
                }
            }

            _logger.LogInformation(
                "UpdateSeenStatus succeeded for {Count} messages",
                messages.Count);

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "UpdateSeenStatus failed for {Count} messages",
                messages?.Count ?? 0);

            return false;
        }
    }

    private async Task<int> GetMaxId()
    {
        _logger.LogDebug("GetMaxId called for messages collection");

        try
        {
            var filter = Builders<Message>.Filter.Empty;
            var sort = Builders<Message>.Sort.Descending("MessageId");
        
            var result = await _messagesCollection.Find(filter).Sort(sort).Limit(1).FirstOrDefaultAsync();
            var maxId = result?.MessageId ?? 0;

            _logger.LogDebug(
                "GetMaxId returned {MaxId} for messages collection",
                maxId);

            return maxId;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "GetMaxId failed for messages collection");

            return 0;
        }
    }
}