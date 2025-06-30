using Core;

namespace ChatAppAPI.Repositories.Interfaces;

public interface IConversationRepository
{
    Task<List<Conversation>?> GetConversationsAsync(int userId);
    Task<Conversation> CreateConversation(Conversation conversation);
    Task<Conversation> UpdateConversation(Conversation conversation);
    Task<Conversation?> GetConversation(int userId, int otherPersonId);
}