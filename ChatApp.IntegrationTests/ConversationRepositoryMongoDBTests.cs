using Azure.Storage.Blobs;
using ChatAppAPI.Repositories;
using Core;
using MongoDB.Driver;

namespace ChatApp.IntegrationTests;

public class ConversationRepositoryMongoDBTests
{
    private IMongoClient _mongoClient = null!;
    private IMongoDatabase _database = null!;
    private ConversationRepositoryMongoDb _conversationRepository = null!;
    private string _databaseName = null!;

    private IMongoCollection<Conversation> _conversationCollection = null!;
    
    [TestInitialize]
    public void Initialize_Tests()
    {
            var connectionString =
            Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
            ?? "mongodb://localhost:27017";

        _mongoClient = new MongoClient(connectionString);

        _databaseName = $"ChatAppTest_{Guid.NewGuid():N}";
        _database = _mongoClient.GetDatabase(_databaseName);
        
        _conversationCollection = _database.GetCollection<Conversation>("Conversations");
    }
    
    [TestCleanup]
    public void Cleanup()
    {
        _mongoClient.DropDatabase(_databaseName);
    }
    
    // GetConversationsAsync tests
    //
    //

    [TestMethod]
    public async Task GetConversationsAsync_returns_all_users_conversations()
    {
        // Arrange
        var limit = 20;
        var page = 0;
        var conversation1 = new Conversation()
        {
            ConversationId = 1,
            PersonAId = 1,
            PersonBId = 2,
            LastMessage = "You up?",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-53)
        };
        var conversation2 = new Conversation()
        {
            ConversationId = 2,
            PersonAId = 1,
            PersonBId = 3,
            LastMessage = "Wanna play?",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-35)
        };
        var conversation3 = new Conversation()
        {
            ConversationId = 3,
            PersonAId = 1,
            PersonBId = 4,
            LastMessage = "Everybody is ignoring me....",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-23)
        };
        var convos = new List<Conversation>(){conversation1, conversation2, conversation3};
        await _conversationCollection.InsertManyAsync(convos);

        // Act
        var result = await _conversationRepository.GetConversationsAsync(1, limit, page);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(3, result);
    }
    
    [TestMethod]
    public async Task GetConversationsAsync_returns_all_users_conversations_ordered_descending()
    {
        // Arrange
        var limit = 20;
        var page = 0;
        var conversation1 = new Conversation()
        {
            ConversationId = 1,
            PersonAId = 1,
            PersonBId = 2,
            LastMessage = "You up?",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-53)
        };
        var conversation2 = new Conversation()
        {
            ConversationId = 2,
            PersonAId = 1,
            PersonBId = 3,
            LastMessage = "Wanna play?",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-35)
        };
        var conversation3 = new Conversation()
        {
            ConversationId = 3,
            PersonAId = 1,
            PersonBId = 4,
            LastMessage = "Everybody is ignoring me....",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-23)
        };
        var convos = new List<Conversation>(){conversation1, conversation2, conversation3};
        await _conversationCollection.InsertManyAsync(convos);

        // Act
        var result = await _conversationRepository.GetConversationsAsync(1, limit, page);
        var expected = convos.OrderByDescending(x => x.Timestamp).ToList();
        
        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(3, result);
        CollectionAssert.AreEquivalent(expected, result);
    }
    
    [TestMethod]
    public async Task GetConversationsAsync_returns_one_conversation_more_than_limit()
    {
        // Arrange
        var limit = 1;
        var page = 0;
        var conversation1 = new Conversation()
        {
            ConversationId = 1,
            PersonAId = 1,
            PersonBId = 2,
            LastMessage = "You up?",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-53)
        };
        var conversation2 = new Conversation()
        {
            ConversationId = 2,
            PersonAId = 1,
            PersonBId = 3,
            LastMessage = "Wanna play?",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-35)
        };
        var conversation3 = new Conversation()
        {
            ConversationId = 3,
            PersonAId = 1,
            PersonBId = 4,
            LastMessage = "Everybody is ignoring me....",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-23)
        };
        var convos = new List<Conversation>(){conversation1, conversation2, conversation3};
        await _conversationCollection.InsertManyAsync(convos);

        // Act
        var result = await _conversationRepository.GetConversationsAsync(1, limit, page);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result);
    }
    
    [TestMethod]
    public async Task GetConversationsAsync_paging_works()
    {
        // Arrange
        var limit = 10;
        var page = 2;
        var conversation1 = new Conversation()
        {
            ConversationId = 1,
            PersonAId = 1,
            PersonBId = 2,
            LastMessage = "You up?",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-53)
        };
        var conversation2 = new Conversation()
        {
            ConversationId = 2,
            PersonAId = 1,
            PersonBId = 3,
            LastMessage = "Wanna play?",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-35)
        };
        var conversation3 = new Conversation()
        {
            ConversationId = 3,
            PersonAId = 1,
            PersonBId = 4,
            LastMessage = "Everybody is ignoring me....",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-23)
        };
        var convos = new List<Conversation>(){conversation1, conversation2, conversation3};
        await _conversationCollection.InsertManyAsync(convos);

        // Act
        var result = await _conversationRepository.GetConversationsAsync(1, limit, page);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(1, result);
    }
    
    [TestMethod]
    public async Task GetConversationsAsync_limit_works()
    {
        // Arrange
        var limit = 1;
        var page = 1;
        var conversation1 = new Conversation()
        {
            ConversationId = 1,
            PersonAId = 1,
            PersonBId = 2,
            LastMessage = "You up?",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-53)
        };
        var conversation2 = new Conversation()
        {
            ConversationId = 2,
            PersonAId = 1,
            PersonBId = 3,
            LastMessage = "Wanna play?",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-35)
        };
        var conversation3 = new Conversation()
        {
            ConversationId = 3,
            PersonAId = 1,
            PersonBId = 4,
            LastMessage = "Everybody is ignoring me....",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-23)
        };
        var convos = new List<Conversation>(){conversation1, conversation2, conversation3};
        await _conversationCollection.InsertManyAsync(convos);

        // Act
        var result = await _conversationRepository.GetConversationsAsync(1, limit, page);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result);
    }
    
    [TestMethod]
    public async Task GetConversationsAsync_returns_empty_list_if_no_conversations_exist()
    {
        // Arrange
        var limit = 200;
        var page = 0;

        // Act
        var result = await _conversationRepository.GetConversationsAsync(1, limit, page);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsEmpty(result);
    }
    
    // CreateConversation tests
    //
    //
    
    [TestMethod]
    public async Task CreateConversation_creates_conversation()
    {
        // Arrange
        var conversation = new Conversation()
        {
            ConversationId = 0,
            PersonAId = 1,
            PersonBId = 2,
            LastMessage = "My guy",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-23)
        };

        // Act
        var result = await _conversationRepository.CreateConversation(conversation);
        var filter = Builders<Conversation>.Filter.Eq("_id", result.ConversationId);
        var mongoDbResult = await _conversationCollection.Find(filter).FirstOrDefaultAsync();
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreNotEqual(0, result.ConversationId);
        Assert.AreEqual(result, mongoDbResult);
    }
    
    [TestMethod]
    public async Task CreateConversation_auto_increments_ids()
    {
        // Arrange
        var conversation1 = new Conversation()
        {
            ConversationId = 0,
            PersonAId = 1,
            PersonBId = 2,
            LastMessage = "My guy",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-23)
        };
        var conversation2 = new Conversation()
        {
            ConversationId = 0,
            PersonAId = 1,
            PersonBId = 2,
            LastMessage = "My guy",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-23)
        };

        // Act
        var result = await _conversationRepository.CreateConversation(conversation1);
        var result2 = await _conversationRepository.CreateConversation(conversation2);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.ConversationId);
        Assert.AreEqual(2, result2.ConversationId);
    }
    
    // UpdateConversation tests
    //
    //
    
    [TestMethod]
    public async Task UpdateConversation_returns_conversation_with_id_0_on_fail()
    {
        // Arrange
        var conversation1 = new Conversation()
        {
            ConversationId = 0,
            PersonAId = 1,
            PersonBId = 2,
            LastMessage = "My guy",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-23)
        };
        var conversation2 = new Conversation()
        {
            ConversationId = 0,
            PersonAId = 1,
            PersonBId = 2,
            LastMessage = "My guy",
            SenderId = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-23)
        };

        // Act
        var result = await _conversationRepository.CreateConversation(conversation1);
        var result2 = await _conversationRepository.CreateConversation(conversation2);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.ConversationId);
        Assert.AreEqual(2, result2.ConversationId);
    }
}