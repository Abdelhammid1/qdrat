using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;

namespace QdratNew.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
            [EmailAddress(ErrorMessage = "صيغة البريد غير صحيحة")]
            [Display(Name = "البريد الإلكتروني")]
            public string Email { get; set; }

            [Required(ErrorMessage = "رقم الهوية مطلوب")]
            [StringLength(20, ErrorMessage = "رقم الهوية غير صالح")]
            [Display(Name = "رقم الهوية الوطنية")]
            public string NationalID { get; set; }

            [StringLength(100, ErrorMessage = "كلمة المرور يجب أن تكون على الأقل {2} أحرف.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "كلمة المرور")]
            public string? Password { get; set; } // ✅ اختيارية

            [DataType(DataType.Password)]
            [Display(Name = "تأكيد كلمة المرور")]
            [Compare("Password", ErrorMessage = "كلمتا المرور غير متطابقتين.")]
            public string? ConfirmPassword { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                // ✅ اجعل كلمة المرور = رقم الهوية إذا لم تُدخل يدويًا
                var password = string.IsNullOrWhiteSpace(Input.Password) ? Input.NationalID : Input.Password;

                var result = await _userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("تم إنشاء حساب جديد للطالب.");

                    // ✅ إنشاء سجل الطالب وربطه بالمستخدم الجديد مع الجنس الافتراضي
                    //var student = new Student
                    //{
                    //    FullName = "طالب جديد",
                    //    NationalID = Input.NationalID,
                    //    Email = Input.Email,
                    //    UserId = user.Id,
                    //    Gender = "ذكر" // ✅ قيمة نصية مطابقة لمتطلبات الكيان

                    //};

                    //_context.Students.Add(student);
                    //await _context.SaveChangesAsync();

                    // 🔹 إرسال بريد التفعيل (اختياري)
                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        null,
                        new { area = "Identity", userId, code, returnUrl },
                        Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "تأكيد البريد الإلكتروني",
                        $"يرجى تأكيد حسابك عبر <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>الضغط هنا</a>.");

                    TempData["SuccessMessage"] = "✅ تم إنشاء الحساب بنجاح، يمكنك تسجيل الدخول الآن.";
                    return RedirectToPage("./Login");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return Page();
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
                throw new NotSupportedException("يجب أن يدعم موفر المستخدم البريد الإلكتروني.");

            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
