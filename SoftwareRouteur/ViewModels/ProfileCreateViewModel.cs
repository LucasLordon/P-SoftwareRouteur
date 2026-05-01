using System.ComponentModel.DataAnnotations;

namespace SoftwareRouteur.ViewModels;

public class ProfileCreateViewModel
{
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public string? Pin { get; set; }

    public string? ConfirmPin { get; set; }
}
