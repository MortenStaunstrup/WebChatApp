using System.Text;
using Azure.Storage;
using Azure.Storage.Blobs;
using ChatAppAPI.Repositories;
using ChatAppAPI.Repositories.Interfaces;
using ChatAppAPI.Token;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

Env.Load();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi


builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false;
        o.TokenValidationParameters = new TokenValidationParameters()
        {
            IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_SECRET"))),
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("MONGO_CONNECTION_STRING environment variable is not set");
    return new MongoClient(connectionString);
});

builder.Services.AddScoped<IMongoDatabase>(sp =>
{
    var mongoClient = sp.GetRequiredService<IMongoClient>();
    
    var databaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME");
    
    if (string.IsNullOrWhiteSpace(databaseName))
        throw new InvalidOperationException("MONGO_DATABASE_NAME environment variable is not set");
    
    return mongoClient.GetDatabase(databaseName);
});

builder.Services.AddScoped<BlobServiceClient>(sp =>
{
    var azureBlobStorageConnectionString = 
        Environment.GetEnvironmentVariable("AZURE_BLOBS_STORAGE_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(azureBlobStorageConnectionString))
        throw new InvalidOperationException("Azure blob storage environment variable is not set");
    
    var azureFileStorageName = Environment.GetEnvironmentVariable("AZURE_FILESTORAGE_NAME");
    var azureFileStorageKey = Environment.GetEnvironmentVariable("AZURE_FILESTORAGE_KEY");
    
    if (string.IsNullOrWhiteSpace(azureFileStorageKey))
        throw new InvalidOperationException("AZURE_FILESTORAGE_KEY environment variable is not set");
    if (string.IsNullOrWhiteSpace(azureFileStorageName))
        throw new InvalidOperationException("Azure file storage name environment variable is not set");
    
    var storageSharedKeyCrendential = new StorageSharedKeyCredential(azureFileStorageName, azureFileStorageKey);
    
    return new BlobServiceClient(
        new Uri(azureBlobStorageConnectionString),
        storageSharedKeyCrendential);
    
});

builder.Services.AddScoped<BlobContainerClient>(sp =>
{
    var serviceClient = sp.GetRequiredService<BlobServiceClient>();
    var containerName = "chatapp";
    if (string.IsNullOrWhiteSpace(containerName))
        throw new InvalidOperationException("AZURE_BLOB_CONTAINER_NAME_environment variable is not set");
    return serviceClient.GetBlobContainerClient(containerName);
});

builder.Services.AddScoped<IMessagesRepository, MessageRepositoryMongoDb>();
builder.Services.AddScoped<IConversationRepository, ConversationRepositoryMongoDb>();
builder.Services.AddSingleton<TokenProvider>();
builder.Services.AddScoped<IUserRepository, UserRepositoryMongoDb>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
        policy.AllowAnyOrigin();
    });
});
builder.Services.AddOpenApi();

var app = builder.Build();


app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();