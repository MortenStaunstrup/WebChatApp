using Core;
using Microsoft.AspNetCore.SignalR;

namespace HubServer;

public class UserHub : Hub<IChatClient>
{
    private static readonly object _lockUsers = new object();
    private static List<ConnectedUser> _connectedUsers = new List<ConnectedUser>();
    
    public override async Task OnConnectedAsync()
    {
        var userIdResponse = string.Empty;
        
        userIdResponse = Context.GetHttpContext().Request.Query["userid"].ToString();
        
        var userId = int.Parse(userIdResponse);

        lock (_lockUsers)
        {
            _connectedUsers.Add(new ConnectedUser()
            {
                UserId = userId,
                ConnectionId = Context.ConnectionId
            });
        }
        
        Console.WriteLine($"User with id {userId} has connected");
        Console.WriteLine($"With connectionId {Context.ConnectionId}");
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        ConnectedUser? user;

        lock (_lockUsers)
        {
            user = _connectedUsers.FirstOrDefault(z => z.ConnectionId == Context.ConnectionId);
            if(user != null)
            {
                _connectedUsers.Remove( user );
            }
        }

        if (user != null)
        {
            Console.WriteLine($"User with id {user.UserId} has disconnected");
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task UpdateReceiverMessages(int receiverId, int senderId)
    {
        ConnectedUser receiver;
        
        lock (_lockUsers)
        {
            receiver = _connectedUsers.FirstOrDefault(u => u.UserId == receiverId);
        }
        
        if (receiver != null)
        {
            await Clients.Client(receiver.ConnectionId).UpdateMessages(senderId); 
            Console.WriteLine($"User with id {receiver.UserId} is online and has received the message");
        }
        else 
        { 
            Console.WriteLine($"User with id {receiverId} is offline, and will get the message when they log on");
        }
        
    }

    public async Task ShowSeen(int receiverId, int senderId)
    {
        ConnectedUser sender;

        lock (_lockUsers)
        {
            sender = _connectedUsers.FirstOrDefault(u => u.UserId == senderId);
        }
        
        if (sender != null)
        {
            await Clients.Client(sender.ConnectionId).UpdateCurrentConvo(receiverId); 
            Console.WriteLine($"User with id {sender.UserId} is online and has been notified that the message is seen by the receiver");
        }
        else 
        { 
            Console.WriteLine($"User with id {receiverId} is offline, and will see receiver has seen their message when they get back online");
        }
        
    }
    
}