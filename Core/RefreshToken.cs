using System.ComponentModel.DataAnnotations;
using Core;
using MongoDB.Bson.Serialization.Attributes;

namespace Core;

public class RefreshToken
{
    [BsonId]
    public int Id { get; set; }
    [Required]
    public required string Token { get; set; }
    [Required]
    public int UserId { get; set; }
    [Required]
    public DateTime ExpiresOnUTC { get; set; }
}