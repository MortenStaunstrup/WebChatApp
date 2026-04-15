using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage;
using ChatAppAPI.Repositories;
using ChatAppAPI.Token;
using Core;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace ChatApp.IntegrationTests;

[TestClass]
public class MessageRepositoryMongoDBTests
{
    private IMongoClient _mongoClient = null!;
    private IMongoDatabase _database = null!;
    private MessageRepositoryMongoDb _messageRepository = null!;
    private string _databaseName = null!;
    private BlobServiceClient _blobServiceClient = null!;
    private string _blobStorageName = null!;

    private IMongoCollection<Message> _messageCollection = null!;
    
    [TestInitialize]
    public void Initialize_Tests()
    {
        // Docker container will be set to host port 27018
        // Therefore MONGO_CONNECTION_STRING should be set to mongodb://test:test123@localhost:27018/?authSource=admin
        var connectionString =
            Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
            ?? "mongodb://localhost:27017";

        _mongoClient = new MongoClient(connectionString);

        _databaseName = $"ChatAppTest_{Guid.NewGuid():N}";
        _database = _mongoClient.GetDatabase(_databaseName);
        
        // REQUIRES AZURITE DOCKER CONTAINER TO BE OPEN
        _blobStorageName = $"blobstoragetest-{Guid.NewGuid():N}"; 
        _blobServiceClient = new BlobServiceClient(
            "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"
            );
        var container = _blobServiceClient.GetBlobContainerClient(_blobStorageName);
        container.CreateIfNotExists();
        _messageRepository = new MessageRepositoryMongoDb(_database, container);
        _messageCollection = _database.GetCollection<Message>("Messages");
    }
    
    [TestCleanup]
    public void Cleanup()
    {
        _mongoClient.DropDatabase(_databaseName);
        _blobServiceClient.DeleteBlobContainer(_blobStorageName);
    }
    
    // UploadFile tests
    //
    //

    [TestMethod]
    public async Task UploadFile_uploads_file_to_azure_blob_storage_and_returns_blob_URL()
    {
        // Arrange
        var fileBytes = new byte[] {   3, 235, 235, 205,  18, 137, 208,  78, 196, 195  };
        var fileName = "MyFileBytesIsVeryGoodHahaPleaseClickThisTextRightNowAndDownload.txt";
        var messageText = "MyFileBytesIsVeryGoodH";
        
        // Act
        var result = await _messageRepository.UploadFile(fileName, fileBytes);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreNotEqual(result, fileName);
        Assert.AreNotEqual(result, messageText);
    }
    
    // DownloadFile tests
    //
    //
    
    [TestMethod]
    public async Task DownloadFile_returns_file_from_message()
    {
        // Arrange
        var fileBytes = new byte[] {   3, 235, 235, 205,  18, 137, 208,  78, 196, 195  };
        var fileName = "MyFileBytesIsVeryGoodHahaPleaseClickThisTextRightNowAndDownload.txt";
        
        string blobName = fileName + Guid.NewGuid();
        BlobClient blobClient = _blobServiceClient.GetBlobContainerClient(_blobStorageName).GetBlobClient(blobName);

        var binData = BinaryData.FromBytes(fileBytes);
        await blobClient.UploadAsync(binData, true);
        var message = new Message()
        {
            MessageId = 1,
            Content = fileName,
            FileURL = blobName,
            IsFile = true,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow
        };
        await _messageCollection.InsertOneAsync(message);
        
        // Act

        var fileRes = await _messageRepository.DownloadFile(message.MessageId);

        // Assert
        Assert.IsNotNull(fileRes);
        Assert.IsNotNull(fileRes.Bytes);
        Assert.IsNotNull(fileRes.FileName);
        Assert.AreEqual(fileName, fileRes.FileName);
        Assert.AreEqual(fileBytes.Length, fileRes.Bytes.Length);
    }
    
    [TestMethod]
    public async Task DownloadFile_returns_null_if_file_not_exists()
    {
        // Arrange
        var fileName = "MyFileBytesIsVeryGoodHahaPleaseClickThisTextRightNowAndDownload.txt";
        var messageText = "MyFileBytesIsVeryGoodH";
        var message = new Message()
        {
            MessageId = 1,
            Content = messageText,
            FileURL = "URLToFileNotExisting",
            IsFile = true,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow
        };
        await _messageCollection.InsertOneAsync(message);
        
        // Act
        var fileRes = await _messageRepository.DownloadFile(1);

        // Assert
        Assert.IsNull(fileRes);
    }
    
    [TestMethod]
    public async Task DownloadFile_returns_null_if_message_not_exists()
    {
        // Arrange
        
        // Act
        var fileRes = await _messageRepository.DownloadFile(1);

        // Assert
        Assert.IsNull(fileRes);
    }
    
    // GetMessages tests
    //
    //

    [TestMethod]
    public async Task GetMessages_returns_all_correct_messages()
    {
        // Arrange
        int limit = 20;
        int page = 0;
        int currentUser = 1;
        int otherUser = 2;
        var message1 = new Message()
        {
            MessageId = 1,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message2 = new Message()
        {
            MessageId = 2,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 1,
            Sender = 2,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-2)
        };
        var message3 = new Message()
        {
            MessageId = 3,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-1)
        };
        var messageNotSent = new Message()
        {
            MessageId = 4,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 52,
            Sender = 252,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-52)
        };
        var messageNotSent2 = new Message()
        {
            MessageId = 5,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 252,
            Sender = 52,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-587)
        };
        await _messageCollection.InsertOneAsync(message1);
        await _messageCollection.InsertOneAsync(message2);
        await _messageCollection.InsertOneAsync(message3);
        await _messageCollection.InsertOneAsync(messageNotSent);
        await _messageCollection.InsertOneAsync(messageNotSent2);

        // Act
        
        var result = await _messageRepository.GetMessages(currentUser, otherUser, limit, page);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(3, result);
    }
    
    [TestMethod]
    public async Task GetMessages_returns_messages_with_timestamp_in_descending_order()
    {
        // Arrange
        int limit = 20;
        int page = 0;
        int currentUser = 1;
        int otherUser = 2;
        var message1 = new Message()
        {
            MessageId = 1,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message2 = new Message()
        {
            MessageId = 2,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 1,
            Sender = 2,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-2)
        };
        var message3 = new Message()
        {
            MessageId = 3,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-1)
        };
        var messageNotSent = new Message()
        {
            MessageId = 4,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 52,
            Sender = 252,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-52)
        };
        var messageNotSent2 = new Message()
        {
            MessageId = 5,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 252,
            Sender = 52,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-587)
        };
        await _messageCollection.InsertOneAsync(message1);
        await _messageCollection.InsertOneAsync(message2);
        await _messageCollection.InsertOneAsync(message3);
        await _messageCollection.InsertOneAsync(messageNotSent);
        await _messageCollection.InsertOneAsync(messageNotSent2);

        // Act
        
        var result = await _messageRepository.GetMessages(currentUser, otherUser, limit, page);
        
        // Assert
        Assert.IsNotNull(result);
        var expectedResult = result.OrderByDescending(m => m.MessageId).ToList();
        Assert.HasCount(3, result);
        CollectionAssert.AreEqual(expectedResult, result);
    }
    
     [TestMethod]
    public async Task GetMessages_returns_empty_list_if_no_messages_existing()
    {
        // Arrange
        int limit = 20;
        int page = 0;
        int currentUser = 1;
        int otherUser = 2;

        // Act
        
        var result = await _messageRepository.GetMessages(currentUser, otherUser, limit, page);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.IsEmpty(result);
    }
    
    [TestMethod]
    public async Task GetMessages_returns_one_more_message_than_limit()
    {
        // Arrange
        int limit = 3;
        int page = 0;
        int currentUser = 1;
        int otherUser = 2;
        var message1 = new Message()
        {
            MessageId = 1,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message2 = new Message()
        {
            MessageId = 2,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 1,
            Sender = 2,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-2)
        };
        var message3 = new Message()
        {
            MessageId = 3,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-1)
        };
        var message4 = new Message()
        {
            MessageId = 4,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 1,
            Sender = 2,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(1)
        };
        var messageNotSent = new Message()
        {
            MessageId = 5,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 52,
            Sender = 252,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-52)
        };
        var messageNotSent2 = new Message()
        {
            MessageId = 6,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 252,
            Sender = 52,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-587)
        };
        await _messageCollection.InsertOneAsync(message1);
        await _messageCollection.InsertOneAsync(message2);
        await _messageCollection.InsertOneAsync(message3);
        await _messageCollection.InsertOneAsync(message4);
        await _messageCollection.InsertOneAsync(messageNotSent);
        await _messageCollection.InsertOneAsync(messageNotSent2);

        // Act
        
        var result = await _messageRepository.GetMessages(currentUser, otherUser, limit, page);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(4, result);
    }
    
    [TestMethod]
    public async Task GetMessages_paging_works()
    {
        // Arrange
        int limit = 2;
        int page = 1;
        int currentUser = 1;
        int otherUser = 2;
        var message1 = new Message()
        {
            MessageId = 1,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message2 = new Message()
        {
            MessageId = 2,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 1,
            Sender = 2,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-2)
        };
        var message3 = new Message()
        {
            MessageId = 3,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-1)
        };
        var message4 = new Message()
        {
            MessageId = 4,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 1,
            Sender = 2,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(1)
        };
        var messageNotSent = new Message()
        {
            MessageId = 5,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 52,
            Sender = 252,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-52)
        };
        var messageNotSent2 = new Message()
        {
            MessageId = 6,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 252,
            Sender = 52,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-587)
        };
        await _messageCollection.InsertOneAsync(message1);
        await _messageCollection.InsertOneAsync(message2);
        await _messageCollection.InsertOneAsync(message3);
        await _messageCollection.InsertOneAsync(message4);
        await _messageCollection.InsertOneAsync(messageNotSent);
        await _messageCollection.InsertOneAsync(messageNotSent2);

        // Act
        
        var result = await _messageRepository.GetMessages(currentUser, otherUser, limit, page);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result);
    }
    
    [TestMethod]
    public async Task GetMessages_limit_works()
    {
        // Arrange
        int limit = 2;
        int page = 0;
        int currentUser = 1;
        int otherUser = 2;
        var message1 = new Message()
        {
            MessageId = 1,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message2 = new Message()
        {
            MessageId = 2,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 1,
            Sender = 2,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-2)
        };
        var message3 = new Message()
        {
            MessageId = 3,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-1)
        };
        var message4 = new Message()
        {
            MessageId = 4,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 1,
            Sender = 2,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(1)
        };
        var messageNotSent = new Message()
        {
            MessageId = 5,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 52,
            Sender = 252,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-52)
        };
        var messageNotSent2 = new Message()
        {
            MessageId = 6,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 252,
            Sender = 52,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-587)
        };
        await _messageCollection.InsertOneAsync(message1);
        await _messageCollection.InsertOneAsync(message2);
        await _messageCollection.InsertOneAsync(message3);
        await _messageCollection.InsertOneAsync(message4);
        await _messageCollection.InsertOneAsync(messageNotSent);
        await _messageCollection.InsertOneAsync(messageNotSent2);

        // Act
        
        var result = await _messageRepository.GetMessages(currentUser, otherUser, limit, page);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(3, result);
    }
    
    [TestMethod]
    public async Task GetMessages_paging_AND_limiting_works()
    {
        // Arrange
        int limit = 1;
        int page = 1;
        int currentUser = 1;
        int otherUser = 2;
        var message1 = new Message()
        {
            MessageId = 1,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message2 = new Message()
        {
            MessageId = 2,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 1,
            Sender = 2,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-2)
        };
        var message3 = new Message()
        {
            MessageId = 3,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-1)
        };
        var message4 = new Message()
        {
            MessageId = 4,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 1,
            Sender = 2,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(1)
        };
        var messageNotSent = new Message()
        {
            MessageId = 5,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 52,
            Sender = 252,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-52)
        };
        var messageNotSent2 = new Message()
        {
            MessageId = 6,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 252,
            Sender = 52,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-587)
        };
        await _messageCollection.InsertOneAsync(message1);
        await _messageCollection.InsertOneAsync(message2);
        await _messageCollection.InsertOneAsync(message3);
        await _messageCollection.InsertOneAsync(message4);
        await _messageCollection.InsertOneAsync(messageNotSent);
        await _messageCollection.InsertOneAsync(messageNotSent2);

        // Act
        
        var result = await _messageRepository.GetMessages(currentUser, otherUser, limit, page);

        // Assert
        Assert.IsNotNull(result);
        Assert.HasCount(2, result);
    }
    
    // SendMessage tests
    //
    //

    [TestMethod]
    public async Task SendMessage_returns_id_of_created_message()
    {
        // Arrange
        var message1 = new Message()
        {
            MessageId = 0,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        
        // Act
        
        var result = await _messageRepository.SendMessage(message1);
        
        // Assert
        Assert.AreEqual(1, result);
        
    }
    
    [TestMethod]
    public async Task SendMessage_auto_increments_message_id_on_creation()
    {
        // Arrange
        var message1 = new Message()
        {
            MessageId = 0,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message2 = new Message()
        {
            MessageId = 0,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message3 = new Message()
        {
            MessageId = 0,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        
        // Act
        
        var result = await _messageRepository.SendMessage(message1);
        var result2 = await _messageRepository.SendMessage(message2);
        var result3 = await _messageRepository.SendMessage(message3);
        
        // Assert
        Assert.AreEqual(1, result);
        Assert.AreEqual(2, result2);
        Assert.AreEqual(3, result3);
    }
    
    // GetSentMessage tests
    //
    //

    [TestMethod]
    public async Task GetSentMessage_returns_message()
    {
        // Arrange
        var content = Guid.NewGuid().ToString();
        var message1 = new Message()
        {
            MessageId = 1,
            Content = content,
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        await _messageCollection.InsertOneAsync(message1);
        
        // Act

        var result = await _messageRepository.GetSentMessage(1);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.MessageId);
        Assert.AreEqual(content, result.Content);

    }
    
    [TestMethod]
    public async Task GetSentMessage_returns_null_if_message_not_found()
    {
        // Arrange
        
        // Act

        var result = await _messageRepository.GetSentMessage(1);
        
        // Assert
        Assert.IsNull(result);

    }
    
    // UpdateSeenStatus tests
    //
    //
    
    [TestMethod]
    public async Task UpdateSeenStatus_updates_seen_status_on_all_messages()
    {
        // Arrange
        var message1 = new Message()
        {
            MessageId = 1,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message2 = new Message()
        {
            MessageId = 2,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message3 = new Message()
        {
            MessageId = 3,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        List<Message> messages = new List<Message>(){message1, message2, message3};
        await _messageCollection.InsertManyAsync(messages);
        
        // Act
        var result = await _messageRepository.UpdateSeenStatus(messages);
        var messagesFromMongo = await _messageCollection.Find(Builders<Message>.Filter.Empty).ToListAsync();
        messages.ForEach(m => m.SeenByReceiver = true);
        
        // Assert
        Assert.IsTrue(result);
        CollectionAssert.AreEqual(messagesFromMongo, messages);

    }
    
    [TestMethod]
    public async Task UpdateSeenStatus_updates_seen_status_on_messages_not_seen()
    {
        // Arrange
        var message1 = new Message()
        {
            MessageId = 1,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message2 = new Message()
        {
            MessageId = 2,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = true,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message3 = new Message()
        {
            MessageId = 3,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        var message4 = new Message()
        {
            MessageId = 4,
            Content = Guid.NewGuid().ToString(),
            IsFile = false,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = true,
            Timestamp = DateTime.UtcNow.AddHours(-3)
        };
        List<Message> messages = new List<Message>(){message1, message2, message3, message4};
        await _messageCollection.InsertManyAsync(messages);
        
        // Act
        var result = await _messageRepository.UpdateSeenStatus(messages);
        var messagesFromMongo = await _messageCollection.Find(Builders<Message>.Filter.Empty).ToListAsync();
        messages.ForEach(m => m.SeenByReceiver = true);
        
        // Assert
        Assert.IsTrue(result);
        CollectionAssert.AreEqual(messagesFromMongo, messages);

    }
    
}