using System.ComponentModel.DataAnnotations;

namespace ClassLibrary1;

public class Class1
{
    
    public class TestValidator : System.ComponentModel.DataAnnotations.ValidationAttribute
    {
        // type "protected override " here and see what you get!
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    }
    
}