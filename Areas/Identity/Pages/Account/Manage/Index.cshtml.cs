#nullable disable

using System.ComponentModel.DataAnnotations;
using Blogger_website.Areas.Identity.Data;
using CareerCracker.S3Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Blogger_website.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public string Username { get; set; }
    public string ProfileImageUrl { get; set; }
    public string Initials { get; set; } = "A";

    [TempData]
    public string StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }

    public class InputModel
    {
        [Required]
        [StringLength(120)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Phone]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Profile Photo")]
        public IFormFile ProfilePhoto { get; set; }
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        Username = await _userManager.GetUserNameAsync(user);
        ProfileImageUrl = user.ProfileImageUrl ?? "";
        Initials = user.FullName?.Length > 0
            ? user.FullName[..1].ToUpper()
            : (user.Email?[..1].ToUpper() ?? "A");

        Input = new InputModel
        {
            FullName = user.FullName ?? "",
            PhoneNumber = await _userManager.GetPhoneNumberAsync(user)
        };
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");

        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        user.FullName = Input.FullName.Trim();

        if (Input.ProfilePhoto is { Length: > 0 })
        {
            if (!string.IsNullOrWhiteSpace(user.ProfileImageUrl))
                await S3StorageHelper.DeleteStoredMediaAsync(user.ProfileImageUrl);

            var url = await S3StorageHelper.UploadFileAsync(Input.ProfilePhoto, "profiles");
            if (!string.IsNullOrWhiteSpace(url))
                user.ProfileImageUrl = url;
        }

        var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
        if (Input.PhoneNumber != phoneNumber)
        {
            var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            if (!setPhoneResult.Succeeded)
            {
                StatusMessage = "Unexpected error when trying to set phone number.";
                return RedirectToPage();
            }
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            StatusMessage = "Could not update profile.";
            return RedirectToPage();
        }

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Your profile has been updated";
        return RedirectToPage();
    }
}
