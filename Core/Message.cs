using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;

namespace Core;

public class Message
{
    [BsonId]
    public int MessageId  { get; set; }
    [Required]
    public int Sender {get; set;}
    [Required]
    public int Receiver {get; set;}
    [Required]
    public bool SeenByReceiver{ get; set; }
    [Required]
    public bool IsFile {get; set;}
    public string FileURL {get; set;}
    [Required]
    public string Content { get; set; }
    [Required]
    public DateTime Timestamp { get; set; }
}