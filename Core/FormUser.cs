using System.ComponentModel.DataAnnotations;

namespace Core;

public class FormUser : User
{
    [Required]
    [Compare("Password", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; }
}