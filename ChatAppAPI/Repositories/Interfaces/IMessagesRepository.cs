using Core;

namespace ChatAppAPI.Repositories.Interfaces;

public interface IMessagesRepository
{
    Task<List<Message>?> GetMessages(int currentUserId, int otherUserId);
    Task<int> SendMessage(Message message);
    Task<Message?> GetSentMessage(int messageId);
    Task<bool> UpdateSeenStatus(List<Message> messages);
}