using System.ComponentModel.DataAnnotations;

namespace Capstone.Validation;

public sealed class OptionalEmailAddressAttribute : ValidationAttribute
{
    private static readonly EmailAddressAttribute EmailCheck = new EmailAddressAttribute();

    public OptionalEmailAddressAttribute()
        : base("The {0} field is not a valid e-mail address.")
    {
    }

    public override bool IsValid(object value)
    {
        if (value == null)
        {
            return true;
        }

        var text = value as string;
        if (text == null)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(text) || EmailCheck.IsValid(text.Trim());
    }
}
