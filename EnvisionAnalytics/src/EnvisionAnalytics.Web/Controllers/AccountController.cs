using EnvisionAnalytics.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EnvisionAnalytics.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _um;
        private readonly SignInManager<ApplicationUser> _sm;
        private readonly Microsoft.AspNetCore.Identity.UI.Services.IEmailSender _emails;

        public AccountController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, Microsoft.AspNetCore.Identity.UI.Services.IEmailSender emails)
        {
            _um = um;
            _sm = sm;
            _emails = emails;
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
            var user = await _um.FindByEmailAsync(email);
            if (user == null) { ModelState.AddModelError("", "Invalid login"); return View(); }
            var res = await _sm.PasswordSignInAsync(user.UserName, password, isPersistent: false, lockoutOnFailure: false);
            if (res.Succeeded) return Redirect(returnUrl ?? "/");
            ModelState.AddModelError("", "Invalid login");
            return View();
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(string userName, string email, string password)
        {
            var u = new ApplicationUser { UserName = userName, Email = email, EmailConfirmed = false };
            var res = await _um.CreateAsync(u, password);
            if (!res.Succeeded)
            {
                foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
                return View();
            }

            var token = await _um.GenerateEmailConfirmationTokenAsync(u);
            var confirmUrl = Url.Action("ConfirmEmail", "Account", new { userId = u.Id, token = System.Net.WebUtility.UrlEncode(token) }, Request.Scheme);
            var html = $"<p>Confirme seu e-mail clicando <a href=\"{confirmUrl}\">aqui</a>.</p>";
            await _emails.SendEmailAsync(email, "Confirm your email", html);

            return RedirectToAction("RegisterConfirmation");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _sm.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult RegisterConfirmation() => View();

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
            var user = await _um.FindByEmailAsync(email);
            if (user == null) { return View(); }
            var token = await _um.GeneratePasswordResetTokenAsync(user);
            var url = Url.Action("ResetPassword", "Account", new { userId = user.Id, token = System.Net.WebUtility.UrlEncode(token) }, Request.Scheme);
            var html = $"<p>Please reset your password by clicking <a href=\"{url}\">here</a></p>";
            await _emails.SendEmailAsync(email, "Reset your password", html);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ResetPassword(string userId, string token)
        {
            ViewData["userId"] = userId;
            ViewData["token"] = token;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string userId, string token, string newPassword)
        {
            var user = await _um.FindByIdAsync(userId);
            if (user == null) return RedirectToAction("Login");
            var decoded = System.Net.WebUtility.UrlDecode(token);
            var res = await _um.ResetPasswordAsync(user, decoded, newPassword);
            if (res.Succeeded) return RedirectToAction("Login");
            foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
            return View();
        }
    }
}
