using System.ComponentModel.DataAnnotations;
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
    public string Email { get; set; }
    [Required]
    [PasswordValidator]
    public string Password { get; set; }
    [Required]
    [Phone]
    public string PhoneNumber { get; set; }
}

public class PasswordValidator : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var password = value as string;
        if(password.Length < 8)
            return new ValidationResult("Password must be at least 8 characters.");
        
        if(!password.Any(char.IsUpper))
            return new ValidationResult("Password must contain at least one upper case letter.");
        
        if(!password.Any(char.IsDigit))
            return new ValidationResult("Password must contain at least one number.");
        
        return ValidationResult.Success;
    }
}