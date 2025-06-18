using System.ComponentModel.DataAnnotations;
using System.Text;
using MongoDB.Bson.Serialization.Attributes;

namespace Core;

public class User
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
    [PasswordValidator]
    [RegularExpression(@"^\S+$", ErrorMessage = "Password cannot contain spaces")]
    public string Password { get; set; }
    [Required]
    [Phone]
    [RegularExpression(@"^\S+$", ErrorMessage = "Phone number cannot contain spaces")]
    public string PhoneNumber { get; set; }
    public string? ProfilePicture { get; set; }
}


public class PasswordValidator : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var password = value as string;
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return new ValidationResult("Password must be at least 8 characters.");
        if(!password.Any(char.IsUpper))
            return new ValidationResult("Password must contain at least one upper case letter.");
        if(!password.Any(char.IsDigit))
            return new ValidationResult("Password must contain at least one number.");
        
        return ValidationResult.Success;
    }
}