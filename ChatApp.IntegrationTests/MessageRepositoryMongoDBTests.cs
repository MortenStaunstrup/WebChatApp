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
        
        _blobStorageName = $"blobstoragetest-{Guid.NewGuid():N}"; 
        _blobServiceClient = new BlobServiceClient(
            "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;"
            );
        var container = _blobServiceClient.GetBlobContainerClient(_blobStorageName);
        container.CreateIfNotExists();
        _messageRepository = new MessageRepositoryMongoDb(_database, container);
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
    public async Task UploadFile_uploads_file_to_azure_blob_storage()
    {
        // Arrange
        var fileBytes = new byte[] {   3, 235, 235, 205,  18, 137, 208,  78, 196, 195  };
        var fileName = "MyFileBytesIsVeryGoodHahaPleaseClickThisTextRightNowAndDownload.txt";
        var messageText = "MyFileBytesIsVeryGoodH";
        
        // Act
        var result = await _messageRepository.UploadFile(fileName, fileBytes);
        var message = new Message()
        {
            MessageId = 0,
            Content = messageText,
            FileURL = result,
            IsFile = true,
            Receiver = 2,
            Sender = 1,
            SeenByReceiver = false,
            Timestamp = DateTime.UtcNow
        };
        var resId = await _messageRepository.SendMessage(message);
        var dbMessage = await _messageRepository.GetSentMessage(resId);

        var fileRes = await _messageRepository.DownloadFile(dbMessage.MessageId);

        // Assert
        Assert.IsNotNull(fileRes);
        Assert.IsNotNull(fileRes.Bytes);
        Assert.IsNotNull(fileRes.FileName);
        Assert.AreEqual(fileBytes.Length, fileRes.Bytes.Length);
    }
    
    // DownloadFile tests
    //
    //
    
}