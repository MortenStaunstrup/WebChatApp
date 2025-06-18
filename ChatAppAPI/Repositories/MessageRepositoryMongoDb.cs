using ChatAppAPI.Repositories.Interfaces;
using Core;
using Microsoft.AspNetCore.Connections;
using MongoDB.Driver;

namespace ChatAppAPI.Repositories;

public class MessageRepositoryMongoDb : IMessagesRepository
{
    private readonly string _connectionString;
    private readonly IMongoClient  _mongoClient;
    private readonly IMongoDatabase _mongoDatabase;
    private readonly IMongoCollection<Message> _messagesCollection;

    public MessageRepositoryMongoDb()
    {
        _connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
        if (string.IsNullOrEmpty(_connectionString))
        {
            throw new ConnectionAbortedException("No connection string set");
        }
        _mongoClient = new MongoClient(_connectionString);
        _mongoDatabase = _mongoClient.GetDatabase("ChatApp");
        _messagesCollection = _mongoDatabase.GetCollection<Message>("Messages");
    }
    
    
    
    public async Task<List<Message>?> GetMessages(int currentUserId, int otherUserId)
    {
        List<Message> messages = new List<Message>();
        
        var currFirstFilter = Builders<Message>.Filter.Eq("Sender", currentUserId);
        var currSecondFilter = Builders<Message>.Filter.Eq("Receiver", otherUserId);
        var firstCombinedFilter =  Builders<Message>.Filter.And(currFirstFilter, currSecondFilter);
        
        var firstList = await _messagesCollection.Find(firstCombinedFilter).ToListAsync();
        
        if(firstList != null)
            messages.AddRange(firstList);
        
        var otherFirstFilter = Builders<Message>.Filter.Eq("Sender", otherUserId);
        var otherSecondFilter = Builders<Message>.Filter.Eq("Receiver", currentUserId);
        var secondCombinedFilter = Builders<Message>.Filter.And(otherFirstFilter, otherSecondFilter);
        
        var secondList = await _messagesCollection.Find(secondCombinedFilter).ToListAsync();
        
        if(secondList != null)
            messages.AddRange(secondList);

        if (messages.Count > 0)
        {
            messages = messages.OrderByDescending(o => o.Timestamp).ToList();
        }
        
        return messages;
        
    }

    public async Task<int> SendMessage(Message message)
    {
        message.MessageId = await GetMaxId() + 1;
        
        try
        {
            await _messagesCollection.InsertOneAsync(message);
            return message.MessageId;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return 0;
        }
    }

    public async Task<Message?> GetSentMessage(int messageId)
    {
        var filter = Builders<Message>.Filter.Eq("MessageId", messageId);
        
        var message = await _messagesCollection.Find(filter).FirstOrDefaultAsync();
        return message;
    }

    private async Task<int> GetMaxId()
    {
        var filter = Builders<Message>.Filter.Empty;
        var sort = Builders<Message>.Sort.Descending("MessageId");
        
        var result = _messagesCollection.Find(filter).Sort(sort).Limit(1).FirstOrDefault();
        return result?.MessageId ?? 0;
        
    }
    
}