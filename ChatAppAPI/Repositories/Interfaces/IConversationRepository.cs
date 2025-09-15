using Core;

namespace ChatAppAPI.Repositories.Interfaces;

public interface IConversationRepository
{
    Task<List<Conversation>?> GetConversationsAsync(int userId, int limit, int page);
    Task<Conversation> CreateConversation(Conversation conversation);
    Task<Conversation> UpdateConversation(Conversation conversation);
    Task<Conversation?> GetConversation(int userId, int otherPersonId);
    Task<Conversation> UpdateConversationSeenStatus(Conversation conversation);
}