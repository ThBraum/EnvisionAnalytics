using EnvisionAnalytics.Data;
using EnvisionAnalytics.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnvisionAnalytics.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _um;
        private readonly RoleManager<IdentityRole> _rm;

        public AdminController(UserManager<ApplicationUser> um, RoleManager<IdentityRole> rm)
        {
            _um = um;
            _rm = rm;
        }

        public async Task<IActionResult> Index()
        {
            var users = _um.Users.ToList();
            var vm = new List<object>();
            foreach(var u in users)
            {
                var roles = await _um.GetRolesAsync(u);
                vm.Add(new { u.Id, u.UserName, u.Email, Roles = roles });
            }
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> AddRole(string role)
        {
            if (!await _rm.RoleExistsAsync(role))
                await _rm.CreateAsync(new IdentityRole(role));
            return RedirectToAction("Index");
        }
    }
}
