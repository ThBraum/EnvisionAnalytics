using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EnvisionAnalytics.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EnvisionAnalytics.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _um;
        private readonly SignInManager<ApplicationUser> _sm;
        private readonly RoleManager<IdentityRole> _rm;
        private readonly Microsoft.AspNetCore.Identity.UI.Services.IEmailSender _emails;
        private readonly ILogger<AccountController> _logger;
        private const string DefaultRole = "Viewer";
        private const string OtpProvider = "EA-OTP";
        private const string PasswordResetPurpose = "PasswordReset";
        private const string EmailConfirmationPurpose = "EmailConfirmation";
        private static readonly TimeSpan PasswordResetCodeLifetime = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan EmailConfirmationCodeLifetime = TimeSpan.FromMinutes(30);

        public AccountController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, Microsoft.AspNetCore.Identity.UI.Services.IEmailSender emails, RoleManager<IdentityRole> rm, ILogger<AccountController> logger)
        {
            _um = um;
            _sm = sm;
            _emails = emails;
            _rm = rm;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                SetSnackbar("Email and password are required.", success: false);
                return View();
            }

            var user = await _um.FindByEmailAsync(email);
            if (user == null)
            {
                SetSnackbar("Invalid email or password.", success: false);
                return View();
            }

            if (!user.EmailConfirmed)
            {
                SetSnackbar("Please confirm your email before logging in.", success: false);
                return View();
            }

            var username = user.UserName ?? user.Email ?? email;
            var res = await _sm.PasswordSignInAsync(username, password, isPersistent: false, lockoutOnFailure: false);
            if (res.Succeeded)
            {
                await _um.ResetAccessFailedCountAsync(user);
                await _um.RemoveAuthenticationTokenAsync(user, OtpProvider, PasswordResetPurpose);
                SetSnackbar("Logged in successfully.", success: true, persist: true);
                return RedirectToLocal(returnUrl);
            }

            if (res.IsLockedOut)
            {
                SetSnackbar("Account locked. Please try again later.", success: false);
                return View();
            }

            await _um.AccessFailedAsync(user);
            await TrySendPasswordResetCodeAsync(user);
            SetSnackbar("Invalid email or password.", success: false);
            return View();
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(string userName, string email, string password, string confirmPassword)
        {
            var trimmedUserName = userName?.Trim();
            var trimmedEmail = email?.Trim();

            if (string.IsNullOrWhiteSpace(trimmedUserName) || string.IsNullOrWhiteSpace(trimmedEmail) || string.IsNullOrWhiteSpace(password))
            {
                SetSnackbar("All fields are required.", success: false);
                return View();
            }

            if (password != confirmPassword)
            {
                SetSnackbar("Password confirmation does not match.", success: false);
                return View();
            }

            var existingByName = await _um.FindByNameAsync(trimmedUserName);
            if (existingByName != null)
            {
                SetSnackbar("Username is already taken.", success: false);
                ModelState.AddModelError("userName", "Username is already taken.");
                return View();
            }

            var existingByEmail = await _um.FindByEmailAsync(trimmedEmail);
            if (existingByEmail != null)
            {
                if (!existingByEmail.EmailConfirmed)
                {
                    await SendRegistrationCodeAsync(existingByEmail, force: true);
                    SetSnackbar("This email is already registered but still waiting for confirmation. We just sent you a new code.", success: true, persist: true);
                    return RedirectToAction("RegisterConfirmation", new { email = trimmedEmail });
                }

                SetSnackbar("Email is already registered.", success: false);
                ModelState.AddModelError("email", "Email is already registered.");
                return View();
            }

            var u = new ApplicationUser { UserName = trimmedUserName, Email = trimmedEmail, EmailConfirmed = false };
            var res = await _um.CreateAsync(u, password);
            if (!res.Succeeded)
            {
                var message = res.Errors.FirstOrDefault()?.Description ?? "Unable to create user.";
                SetSnackbar(message, success: false);
                foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
                return View();
            }

            if (!await EnsureRoleExistsAsync(DefaultRole))
            {
                SetSnackbar("Unable to provision the default role. Please contact support.", success: false);
                return View();
            }

            var roleResult = await _um.AddToRoleAsync(u, DefaultRole);
            if (!roleResult.Succeeded)
            {
                var message = roleResult.Errors.FirstOrDefault()?.Description ?? "Unable to assign default role.";
                SetSnackbar(message, success: false);
                foreach (var e in roleResult.Errors) ModelState.AddModelError("", e.Description);
                return View();
            }

            await SendRegistrationCodeAsync(u, force: true);

            SetSnackbar("Account created! Please confirm your email.", success: true, persist: true);
            return RedirectToAction("RegisterConfirmation", new { email });
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _sm.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult RegisterConfirmation(string? email)
        {
            ViewData["Email"] = email;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyRegistrationCode(string email, string code)
        {
            ViewData["Email"] = email;
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            {
                SetSnackbar("Invalid verification code.", success: false);
                return View("RegisterConfirmation");
            }

            var user = await _um.FindByEmailAsync(email);
            if (user == null)
            {
                SetSnackbar("Invalid verification code.", success: false);
                return View("RegisterConfirmation");
            }

            var codeValid = await ValidateVerificationCodeAsync(user, EmailConfirmationPurpose, code.Trim());
            if (!codeValid)
            {
                SetSnackbar("Invalid or expired verification code.", success: false);
                return View("RegisterConfirmation");
            }

            var token = await _um.GenerateEmailConfirmationTokenAsync(user);
            var res = await _um.ConfirmEmailAsync(user, token);
            if (res.Succeeded)
            {
                await _um.RemoveAuthenticationTokenAsync(user, OtpProvider, EmailConfirmationPurpose);
                SetSnackbar("Email confirmed! You can now log in.", success: true, persist: true);
                return RedirectToAction("Login");
            }

            foreach (var error in res.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            SetSnackbar("Unable to confirm email. Please try again.", success: false);
            return View("RegisterConfirmation");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _um.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var vm = new EditProfileViewModel
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(EditProfileViewModel model)
        {
            var user = await _um.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var trimmedUserName = (model.UserName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmedUserName))
            {
                ModelState.AddModelError("UserName", "Username is required.");
                model.UserName = user.UserName;
                model.Email = user.Email;
                return View(model);
            }

            if (!string.Equals(trimmedUserName, user.UserName, StringComparison.OrdinalIgnoreCase))
            {
                var userWithSameName = await _um.FindByNameAsync(trimmedUserName);
                if (userWithSameName != null)
                {
                    ModelState.AddModelError("UserName", "This username is already in use.");
                    model.Email = user.Email;
                    return View(model);
                }

                user.UserName = trimmedUserName;
                user.NormalizedUserName = trimmedUserName.ToUpperInvariant();
                var updateUserResult = await _um.UpdateAsync(user);
                if (!updateUserResult.Succeeded)
                {
                    foreach (var error in updateUserResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    model.Email = user.Email;
                    return View(model);
                }
            }

            var wantsPasswordChange = !string.IsNullOrWhiteSpace(model.NewPassword) ||
                                        !string.IsNullOrWhiteSpace(model.ConfirmPassword) ||
                                        !string.IsNullOrWhiteSpace(model.CurrentPassword);

            if (wantsPasswordChange)
            {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword))
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is required to update your password.");
                    model.Email = user.Email;
                    return View(model);
                }

                if (string.IsNullOrWhiteSpace(model.NewPassword) || string.IsNullOrWhiteSpace(model.ConfirmPassword))
                {
                    ModelState.AddModelError("NewPassword", "New password and confirmation are required.");
                    model.Email = user.Email;
                    return View(model);
                }

                if (!string.Equals(model.NewPassword, model.ConfirmPassword, StringComparison.Ordinal))
                {
                    ModelState.AddModelError("ConfirmPassword", "Password confirmation does not match.");
                    model.Email = user.Email;
                    return View(model);
                }

                var changePasswordResult = await _um.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                if (!changePasswordResult.Succeeded)
                {
                    foreach (var error in changePasswordResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    model.Email = user.Email;
                    return View(model);
                }
            }

            SetSnackbar("Profile updated successfully.", success: true, persist: true);
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        public async Task<IActionResult> ResendRegistrationCode(string email)
        {
            ViewData["Email"] = email;
            if (!string.IsNullOrWhiteSpace(email))
            {
                var user = await _um.FindByEmailAsync(email);
                if (user != null && !user.EmailConfirmed)
                {
                    await SendRegistrationCodeAsync(user, force: true);
                    SetSnackbar("A new verification code has been sent if the email exists.", success: true);
                    return View("RegisterConfirmation");
                }
            }

            SetSnackbar("A new verification code has been sent if the email exists.", success: true);
            return View("RegisterConfirmation");
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token)) return RedirectToAction("Index", "Home");
            var user = await _um.FindByIdAsync(userId);
            if (user == null) return RedirectToAction("Index", "Home");
            var decoded = System.Net.WebUtility.UrlDecode(token);
            var res = await _um.ConfirmEmailAsync(user, decoded);
            return View(res.Succeeded ? "ConfirmEmailSuccess" : "ConfirmEmailFailed");
        }

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                var user = await _um.FindByEmailAsync(email);
                if (user != null)
                {
                    await SendPasswordResetCodeAsync(user, force: true);
                }
            }

            SetSnackbar("If the email exists, we sent a reset code.", success: true);
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string code, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(newPassword))
            {
                SetSnackbar("All fields are required.", success: false);
                return View();
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                SetSnackbar("Password confirmation does not match.", success: false);
                return View();
            }

            var user = await _um.FindByEmailAsync(email);
            if (user == null)
            {
                SetSnackbar("Invalid reset code.", success: false);
                return View();
            }

            var codeValid = await ValidateVerificationCodeAsync(user, PasswordResetPurpose, code.Trim(), consume: true);
            if (!codeValid)
            {
                SetSnackbar("Invalid or expired reset code.", success: false);
                return View();
            }

            var token = await _um.GeneratePasswordResetTokenAsync(user);
            var res = await _um.ResetPasswordAsync(user, token, newPassword);
            if (res.Succeeded)
            {
                await _um.ResetAccessFailedCountAsync(user);
                SetSnackbar("Password updated. You can now log in.", success: true, persist: true);
                return RedirectToAction("Login");
            }

            foreach (var e in res.Errors)
            {
                ModelState.AddModelError("", e.Description);
            }

            SetSnackbar("Unable to reset password.", success: false);
            return View();
        }

        private void SetSnackbar(string message, bool success, bool persist = false)
        {
            var key = success ? "SnackbarSuccess" : "SnackbarError";
            if (persist)
            {
                TempData[key] = message;
            }
            else
            {
                ViewData[key] = message;
            }
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        private async Task<bool> EnsureRoleExistsAsync(string roleName)
        {
            if (await _rm.RoleExistsAsync(roleName)) return true;

            var result = await _rm.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return result.Succeeded;
        }

        private async Task TrySendPasswordResetCodeAsync(ApplicationUser user)
        {
            var currentFailures = await _um.GetAccessFailedCountAsync(user);
            if (currentFailures < 2)
            {
                return;
            }

            await SendPasswordResetCodeAsync(user);
        }

        private async Task SendPasswordResetCodeAsync(ApplicationUser user, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(user.Email)) return;

            if (!force)
            {
                var existing = await _um.GetAuthenticationTokenAsync(user, OtpProvider, PasswordResetPurpose);
                if (TryParseToken(existing, out _, out var expires) && expires > DateTimeOffset.UtcNow)
                {
                    return; // there is already a valid code in flight
                }
            }

            var code = await GenerateVerificationCodeAsync(user, PasswordResetPurpose, PasswordResetCodeLifetime);
            var html = new StringBuilder()
                .Append("<p>Here is your password reset code:</p>")
                .Append($"<p style='font-size:24px;font-weight:bold;letter-spacing:4px'>{code}</p>")
                .Append("<p>The code expires in 15 minutes. Enter it on the reset password page.</p>")
                .ToString();
            await _emails.SendEmailAsync(user.Email, "Reset your password", html);
            _logger.LogInformation("Password reset code sent to {Email}", user.Email);
        }

        private async Task SendRegistrationCodeAsync(ApplicationUser user, bool force = false)
        {
            if (string.IsNullOrWhiteSpace(user.Email)) return;
            if (user.EmailConfirmed) return;

            if (!force)
            {
                var existing = await _um.GetAuthenticationTokenAsync(user, OtpProvider, EmailConfirmationPurpose);
                if (TryParseToken(existing, out _, out var expires) && expires > DateTimeOffset.UtcNow)
                {
                    return;
                }
            }

            var code = await GenerateVerificationCodeAsync(user, EmailConfirmationPurpose, EmailConfirmationCodeLifetime);
            var html = new StringBuilder()
                .Append("<p>Use the verification code below to confirm your email:</p>")
                .Append($"<p style='font-size:24px;font-weight:bold;letter-spacing:4px'>{code}</p>")
                .Append("<p>The code expires in 30 minutes.</p>")
                .ToString();
            await _emails.SendEmailAsync(user.Email, "Confirm your email", html);
            _logger.LogInformation("Registration code sent to {Email}", user.Email);
        }

        private async Task<string> GenerateVerificationCodeAsync(ApplicationUser user, string purpose, TimeSpan lifetime)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var hash = Hash(code);
            var expires = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
            var payload = $"{hash}|{expires}";
            await _um.SetAuthenticationTokenAsync(user, OtpProvider, purpose, payload);
            return code;
        }

        private async Task<bool> ValidateVerificationCodeAsync(ApplicationUser user, string purpose, string code, bool consume = false)
        {
            var stored = await _um.GetAuthenticationTokenAsync(user, OtpProvider, purpose);
            if (!TryParseToken(stored, out var storedHash, out var expires))
            {
                return false;
            }

            if (expires < DateTimeOffset.UtcNow)
            {
                await _um.RemoveAuthenticationTokenAsync(user, OtpProvider, purpose);
                return false;
            }

            var incomingHash = Hash(code);
            if (!SlowEquals(storedHash, incomingHash))
            {
                return false;
            }

            if (consume)
            {
                await _um.RemoveAuthenticationTokenAsync(user, OtpProvider, purpose);
            }

            return true;
        }

        private static string Hash(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }

        private static bool SlowEquals(string leftHex, string rightHex)
        {
            var leftBytes = Convert.FromHexString(leftHex);
            var rightBytes = Convert.FromHexString(rightHex);
            if (leftBytes.Length != rightBytes.Length) return false;
            return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }

        private static bool TryParseToken(string? payload, out string hash, out DateTimeOffset expires)
        {
            hash = string.Empty;
            expires = DateTimeOffset.MinValue;
            if (string.IsNullOrWhiteSpace(payload)) return false;
            var parts = payload.Split('|');
            if (parts.Length != 2) return false;
            if (!long.TryParse(parts[1], out var epoch)) return false;
            hash = parts[0];
            expires = DateTimeOffset.FromUnixTimeSeconds(epoch);
            return true;
        }
    }
}
