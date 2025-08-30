using Azure;
using ChatAppAPI.Repositories.Interfaces;
using Core;
using Microsoft.AspNetCore.Connections;
using MongoDB.Driver;
using Azure.Storage.Files.Shares;

namespace ChatAppAPI.Repositories;

public class MessageRepositoryMongoDb : IMessagesRepository
{
    private readonly string _connectionString;
    private readonly IMongoClient  _mongoClient;
    private readonly IMongoDatabase _mongoDatabase;
    private readonly IMongoCollection<Message> _messagesCollection;
    private readonly ShareClient _shareClient;

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
        _shareClient = new ShareClient(Environment.GetEnvironmentVariable("AZURE_FILE_STORAGE_CONNECTION_STRING"), "userfiles");
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

        // Check if the user is getting messages they have sent to themselves
        if (currentUserId == otherUserId)
            return messages;
        
        var otherFirstFilter = Builders<Message>.Filter.Eq("Sender", otherUserId);
        var otherSecondFilter = Builders<Message>.Filter.Eq("Receiver", currentUserId);
        var secondCombinedFilter = Builders<Message>.Filter.And(otherFirstFilter, otherSecondFilter);
        
        var secondList = await _messagesCollection.Find(secondCombinedFilter).ToListAsync();
        
        if(secondList != null)
            messages.AddRange(secondList);

        if (messages.Count > 0)
        {
            messages = messages.OrderBy(o => o.Timestamp).ToList();
        }
        
        return messages;
        
    }

    public async Task<string> UploadFile(string fileName, int senderId, byte[] file)
    {
        try
        {
            // Get or create the folder of the user sending a file
            var folder = _shareClient.GetDirectoryClient($"{senderId}");
            await folder.CreateIfNotExistsAsync();
            
            // Get the file, if a file with the same name already exists, append a number to the end
            var fileClient = folder.GetFileClient($"{fileName}");
            
            if (await fileClient.ExistsAsync())
            {
                int duplicate = 1;
                
                
                // Find extension eg. .pdf, .jpg so on
                int extensionIndex = 0;
                for (int i = fileName.Length - 1; i >= 0; i--)
                {
                    if (fileName[i].Equals('.'))
                    {
                        extensionIndex = i;
                        break;
                    }
                }
                
                string extension = fileName.Substring(extensionIndex);
                string name = fileName.Substring(0, extensionIndex);
                
                while (true)
                {
                    var fileThing = folder.GetFileClient($"{name}({duplicate}){extension}");
                    if (!await fileThing.ExistsAsync())
                    {
                        await folder.CreateFileAsync($"{name}({duplicate}){extension}", file.Length);
                        var uploadedFile = folder.GetFileClient($"{name}({duplicate}){extension}");
                        using (var stream = new MemoryStream(file))
                        {
                            await uploadedFile.UploadRangeAsync(
                                Azure.Storage.Files.Shares.Models.ShareFileRangeWriteType.Update,
                                new HttpRange(0, file.Length),
                                stream);
                        }

                        return $"{name}({duplicate}){extension}";
                    }
                    duplicate++;
                }
            }
            else
            {
                await folder.CreateFileAsync($"{fileName}", file.Length);
                var uploadedFile = folder.GetFileClient($"{fileName}");
                using (var stream = new MemoryStream(file))
                {
                    await uploadedFile.UploadRangeAsync(
                        Azure.Storage.Files.Shares.Models.ShareFileRangeWriteType.Update,
                        new HttpRange(0, file.Length),
                        stream);
                }

                return fileName;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return "";
        }
        
    }

    public async Task<ByteNameContainer?> DownloadFile(int messageId)
    {
        var message = await GetSentMessage(messageId);
        if (message == null)
            return null;
        
        
        try
        {
            var folder = _shareClient.GetDirectoryClient($"{message.Sender}");
            var fileClient = folder.GetFileClient($"{message.FileURL}");
            var downloadInfo = await fileClient.DownloadAsync();

            using (var stream = new MemoryStream())
            {
                await downloadInfo.Value.Content.CopyToAsync(stream);

                return new ByteNameContainer()
                {
                    FileName = message.FileURL,
                    Bytes = stream.ToArray()
                };
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
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

    public async Task<bool> UpdateSeenStatus(List<Message> messages)
    {
        foreach (var message in messages)
        {
            if (message.SeenByReceiver)
            {
                var filter = Builders<Message>.Filter.Eq("_id", message.MessageId);
                var update = Builders<Message>.Update.Set("SeenByReceiver", true);
                
                try
                {
                    await _messagesCollection.UpdateOneAsync(filter, update);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return false;
                }
            }
        }
        return true;
    }

    private async Task<int> GetMaxId()
    {
        var filter = Builders<Message>.Filter.Empty;
        var sort = Builders<Message>.Sort.Descending("MessageId");
        
        var result = await _messagesCollection.Find(filter).Sort(sort).Limit(1).FirstOrDefaultAsync();
        return result?.MessageId ?? 0;
        
    }
    
}