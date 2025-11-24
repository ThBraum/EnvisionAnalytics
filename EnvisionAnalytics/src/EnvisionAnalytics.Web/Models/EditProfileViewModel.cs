using System.ComponentModel.DataAnnotations;

namespace EnvisionAnalytics.Models
{
    public class EditProfileViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string? UserName { get; set; }

        public string? Email { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        public string? ConfirmPassword { get; set; }

        public string AvatarLetter => string.IsNullOrWhiteSpace(UserName)
            ? "?"
            : char.ToUpperInvariant(UserName.Trim()[0]).ToString();
    }
}
