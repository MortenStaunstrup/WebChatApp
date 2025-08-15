using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;

namespace Core;

public class ProfileUser
{
    [BsonId]
    public int UserId { get; set; }
    [Required]
    [RegularExpression(@"^[a-zA-ZÀ-ÿ'\- ]+$", ErrorMessage = "First name contains invalid characters.")]
    public string FirstName { get; set; }
    [Required]
    [RegularExpression(@"^[a-zA-ZÀ-ÿ'\- ]+$", ErrorMessage = "Last name contains invalid characters.")]
    public string LastName { get; set; }
    [Required]
    [EmailAddress]
    [RegularExpression(@"^\S+$", ErrorMessage = "Email cannot contain spaces")]
    public string Email { get; set; }
    [Required]
    [Phone]
    [RegularExpression(@"^\S+$", ErrorMessage = "Phone number cannot contain spaces")]
    [Length(8,8, ErrorMessage = "Number must be 8 characters")]
    public string PhoneNumber { get; set; }
    public string? ProfilePicture { get; set; }
}