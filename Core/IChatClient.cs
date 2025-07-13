namespace Core;

public interface IChatClient
{
    Task UpdateMessages(int senderId);
    Task UpdateCurrentConvo(int receiverId);
}