using ChatAppAPI.Repositories.Interfaces;
using Core;
using MongoDB.Driver;

namespace ChatAppAPI.Repositories;

public class ConversationRepositoryMongoDb : IConversationRepository
{
    private readonly string _connectionString;
    private readonly IMongoClient _mongoClient;
    private readonly IMongoDatabase _mongoDatabase;
    private readonly IMongoCollection<Conversation> _conversations;


    public ConversationRepositoryMongoDb()
    {
        _connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
        _mongoClient = new MongoClient(_connectionString);
        _mongoDatabase = _mongoClient.GetDatabase("ChatApp");
        _conversations = _mongoDatabase.GetCollection<Conversation>("Conversations");
    }


    public async Task<List<Conversation>?> GetConversationsAsync(int userId, int limit, int page)
    {
        var filter =  Builders<Conversation>.Filter.Eq("PersonAId", userId);
        var filter2 = Builders<Conversation>.Filter.Eq("PersonBId", userId);
        var orFilter = Builders<Conversation>.Filter.Or(filter, filter2);
        
        var result =  await _conversations.Find(orFilter).Skip(limit * page).Limit(limit).ToListAsync();
        return result;
        
    }

    public async Task<Conversation> CreateConversation(Conversation conversation)
    {
        conversation.ConversationId = await GetMaxId() + 1;

        try
        {
            await _conversations.InsertOneAsync(conversation);
            return conversation;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            conversation.ConversationId = 0;
            return conversation;
        }
        
    }

    public async Task<Conversation> UpdateConversation(Conversation conversation)
    {
        var filter = Builders<Conversation>.Filter.Eq("ConversationId", conversation.ConversationId);

        try
        {
            var result = await _conversations.ReplaceOneAsync(filter, conversation);
            if(result.IsAcknowledged)
                return conversation;
            
            conversation.ConversationId = 0;
            return conversation;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            conversation.ConversationId = 0;
            return conversation;
        }
    }
    
    public async Task<Conversation> UpdateConversationSeenStatus(Conversation conversation)
    {
        var filter = Builders<Conversation>.Filter.Eq("ConversationId",  conversation.ConversationId);

        try
        {
            var currentConvo = await GetConversationById(conversation.ConversationId);
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
                return newConvo;
            
            conversation.ConversationId = 0;
            return conversation;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            conversation.ConversationId = 0;
            return conversation;
        }
    }

    public async Task<Conversation?> GetConversation(int userId, int otherPersonId)
    {

        if (userId == otherPersonId)
        {
            var sameFilter = Builders<Conversation>.Filter.Eq("PersonAId", userId);
            var sameFilter2 = Builders<Conversation>.Filter.Eq("PersonBId", userId);
            var andFilter = Builders<Conversation>.Filter.And(sameFilter, sameFilter2);
            
            var sameResult = await _conversations.Find(andFilter).FirstOrDefaultAsync();
            return sameResult;
        }
        
        var filter = Builders<Conversation>.Filter.Eq("PersonAId", userId);
        var filter2 = Builders<Conversation>.Filter.Eq("PersonBId", userId); 
        var orFilter = Builders<Conversation>.Filter.Or(filter, filter2);
        
        var otherFilter = Builders<Conversation>.Filter.Eq("PersonAId", otherPersonId);
        var otherFilter2 = Builders<Conversation>.Filter.Eq("PersonBId", otherPersonId);
        var orFilter2 = Builders<Conversation>.Filter.Or(otherFilter, otherFilter2);
        
        var finalFilter =  Builders<Conversation>.Filter.And(orFilter, orFilter2);
        
        var result = await _conversations.Find(finalFilter).FirstOrDefaultAsync();
        return result;
        
    }

    private async Task<Conversation?> GetConversationById(int id)
    {
        var filter = Builders<Conversation>.Filter.Eq("ConversationId", id);
        var result = await _conversations.Find(filter).FirstOrDefaultAsync();
        return result;
    }

    private async Task<int> GetMaxId()
    {
        var filter = Builders<Conversation>.Filter.Empty;
        var sort = Builders<Conversation>.Sort.Descending("_id");
        
        var result = await _conversations.Find(filter).Sort(sort).Limit(1).FirstOrDefaultAsync();
        return result?.ConversationId ?? 0;
        
    }
    
    
}