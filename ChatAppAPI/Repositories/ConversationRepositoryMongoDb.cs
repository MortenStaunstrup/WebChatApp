using ChatAppAPI.Repositories.Interfaces;
using Core;
using MongoDB.Driver;

namespace ChatAppAPI.Repositories;

public class ConversationRepositoryMongoDb : IConversationRepository
{
    private readonly IMongoCollection<Conversation> _conversations;
    private readonly ILogger<ConversationRepositoryMongoDb> _logger;

    public ConversationRepositoryMongoDb(
        IMongoDatabase mongoDatabase,
        ILogger<ConversationRepositoryMongoDb> logger
        )
    {
        _logger = logger;
        _conversations = mongoDatabase.GetCollection<Conversation>("Conversations");
    }

    public async Task<List<Conversation>?> GetConversationsAsync(int userId, int limit, int page)
    {
        _logger.LogDebug(
            "GetConversationsAsync called for user {UserId} with limit {Limit} and page {Page}",
            userId, limit, page);

        var filter = Builders<Conversation>.Filter.Eq("PersonAId", userId);
        var filter2 = Builders<Conversation>.Filter.Eq("PersonBId", userId);
        var orFilter = Builders<Conversation>.Filter.Or(filter, filter2);

        try
        {
            var result = await _conversations
                .Find(orFilter)
                .SortByDescending(c => c.Timestamp)
                .Skip(limit * page)
                .Limit(limit + 1)
                .ToListAsync();

            _logger.LogDebug(
                "GetConversationsAsync returned {Count} conversations for user {UserId}",
                result.Count, userId);

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "GetConversationsAsync failed for user {UserId} with limit {Limit} and page {Page}",
                userId, limit, page);

            return null;
        }
    }

    public async Task<Conversation> CreateConversation(Conversation conversation)
    {
        _logger.LogDebug(
            "CreateConversation called for participants {PersonAId} and {PersonBId}",
            conversation.PersonAId, conversation.PersonBId);

        conversation.ConversationId = await GetMaxId() + 1;

        try
        {
            await _conversations.InsertOneAsync(conversation);

            _logger.LogInformation(
                "CreateConversation succeeded with conversation id {ConversationId} for participants {PersonAId} and {PersonBId}",
                conversation.ConversationId, conversation.PersonAId, conversation.PersonBId);

            return conversation;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "CreateConversation failed for participants {PersonAId} and {PersonBId}",
                conversation.PersonAId, conversation.PersonBId);

            conversation.ConversationId = 0;
            return conversation;
        }
    }

    public async Task<Conversation> UpdateConversation(Conversation conversation)
    {
        _logger.LogDebug(
            "UpdateConversation called for conversation {ConversationId}",
            conversation.ConversationId);

        var filter = Builders<Conversation>.Filter.Eq("ConversationId", conversation.ConversationId);

        try
        {
            var result = await _conversations.ReplaceOneAsync(filter, conversation);

            if (result.IsAcknowledged)
            {
                _logger.LogInformation(
                    "UpdateConversation succeeded for conversation {ConversationId}",
                    conversation.ConversationId);

                return conversation;
            }

            _logger.LogWarning(
                "UpdateConversation was not acknowledged for conversation {ConversationId}",
                conversation.ConversationId);

            conversation.ConversationId = 0;
            return conversation;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "UpdateConversation failed for conversation {ConversationId}",
                conversation.ConversationId);

            conversation.ConversationId = 0;
            return conversation;
        }
    }
    
    public async Task<Conversation> UpdateConversationSeenStatus(Conversation conversation)
    {
        _logger.LogDebug(
            "UpdateConversationSeenStatus called for conversation {ConversationId}",
            conversation.ConversationId);

        var filter = Builders<Conversation>.Filter.Eq("ConversationId", conversation.ConversationId);

        try
        {
            var currentConvo = await GetConversationById(conversation.ConversationId);

            if (currentConvo == null)
            {
                _logger.LogWarning(
                    "UpdateConversationSeenStatus failed because conversation {ConversationId} was not found",
                    conversation.ConversationId);

                conversation.ConversationId = 0;
                return conversation;
            }

            var newConvo = new Conversation
            {
                ConversationId = conversation.ConversationId,
                PersonAId = conversation.PersonAId,
                PersonBId = conversation.PersonBId,
                LastMessage = conversation.LastMessage,
                SeenByReceiver = conversation.SeenByReceiver,
                SenderId = conversation.SenderId,
                Timestamp = currentConvo.Timestamp,
            };

            var result = await _conversations.ReplaceOneAsync(filter, newConvo);

            if (result.IsAcknowledged)
            {
                _logger.LogInformation(
                    "UpdateConversationSeenStatus succeeded for conversation {ConversationId}",
                    conversation.ConversationId);

                return newConvo;
            }

            _logger.LogWarning(
                "UpdateConversationSeenStatus was not acknowledged for conversation {ConversationId}",
                conversation.ConversationId);

            conversation.ConversationId = 0;
            return conversation;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "UpdateConversationSeenStatus failed for conversation {ConversationId}",
                conversation.ConversationId);

            conversation.ConversationId = 0;
            return conversation;
        }
    }

    public async Task<Conversation?> GetConversation(int userId, int otherPersonId)
    {
        _logger.LogDebug(
            "GetConversation called for user {UserId} and other person {OtherPersonId}",
            userId, otherPersonId);

        try
        {
            if (userId == otherPersonId)
            {
                var sameFilter = Builders<Conversation>.Filter.Eq("PersonAId", userId);
                var sameFilter2 = Builders<Conversation>.Filter.Eq("PersonBId", userId);
                var andFilter = Builders<Conversation>.Filter.And(sameFilter, sameFilter2);

                var sameResult = await _conversations.Find(andFilter).FirstOrDefaultAsync();

                if (sameResult == null)
                {
                    _logger.LogDebug(
                        "GetConversation found no self-conversation for user {UserId}",
                        userId);
                }
                else
                {
                    _logger.LogDebug(
                        "GetConversation found self-conversation {ConversationId} for user {UserId}",
                        sameResult.ConversationId, userId);
                }

                return sameResult;
            }

            var filter = Builders<Conversation>.Filter.Eq("PersonAId", userId);
            var filter2 = Builders<Conversation>.Filter.Eq("PersonBId", userId);
            var orFilter = Builders<Conversation>.Filter.Or(filter, filter2);

            var otherFilter = Builders<Conversation>.Filter.Eq("PersonAId", otherPersonId);
            var otherFilter2 = Builders<Conversation>.Filter.Eq("PersonBId", otherPersonId);
            var orFilter2 = Builders<Conversation>.Filter.Or(otherFilter, otherFilter2);

            var finalFilter = Builders<Conversation>.Filter.And(orFilter, orFilter2);

            var result = await _conversations.Find(finalFilter).FirstOrDefaultAsync();

            if (result == null)
            {
                _logger.LogDebug(
                    "GetConversation found no conversation for user {UserId} and other person {OtherPersonId}",
                    userId, otherPersonId);
            }
            else
            {
                _logger.LogDebug(
                    "GetConversation found conversation {ConversationId} for user {UserId} and other person {OtherPersonId}",
                    result.ConversationId, userId, otherPersonId);
            }

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "GetConversation failed for user {UserId} and other person {OtherPersonId}",
                userId, otherPersonId);

            return null;
        }
    }

    private async Task<Conversation?> GetConversationById(int id)
    {
        _logger.LogDebug("GetConversationById called for conversation {ConversationId}", id);

        try
        {
            var filter = Builders<Conversation>.Filter.Eq("ConversationId", id);
            var result = await _conversations.Find(filter).FirstOrDefaultAsync();

            if (result == null)
            {
                _logger.LogDebug("GetConversationById found no conversation for id {ConversationId}", id);
            }

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetConversationById failed for conversation {ConversationId}", id);
            return null;
        }
    }

    private async Task<int> GetMaxId()
    {
        _logger.LogDebug("GetMaxId called for conversations collection");

        try
        {
            var filter = Builders<Conversation>.Filter.Empty;
            var sort = Builders<Conversation>.Sort.Descending("_id");

            var result = await _conversations.Find(filter).Sort(sort).Limit(1).FirstOrDefaultAsync();
            var maxId = result?.ConversationId ?? 0;

            _logger.LogDebug("GetMaxId returned {MaxId}", maxId);

            return maxId;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetMaxId failed for conversations collection");
            return 0;
        }
    }
}