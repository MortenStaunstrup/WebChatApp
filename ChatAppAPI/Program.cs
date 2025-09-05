using ChatAppAPI.Repositories;
using ChatAppAPI.Repositories.Interfaces;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

Env.Load();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi


builder.Services.AddSingleton<IUserRepository, UserRepositoryMongoDb>();
builder.Services.AddSingleton<IMessagesRepository, MessageRepositoryMongoDb>();
builder.Services.AddSingleton<IConversationRepository, ConversationRepositoryMongoDb>();

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

app.UseAuthorization();

app.MapControllers();

app.Run();