using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;

namespace Core;

public class Conversation
{
    [BsonId]
    public int ConversationId { get; set; }
    [Required]
    public int PersonAId { get; set; }  
    [Required]
    public int PersonBId { get; set; }
    [Required]
    public int SenderId { get; set; }
    [Required]
    public DateTime Timestamp { get; set; }
    public string? LastMessage { get; set; }
    public bool SeenByReceiver { get; set; }
}