using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Pickleball_Smash.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class AdminAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var httpContext = context.HttpContext;
            var userId = httpContext.Session.GetInt32("UserID");
            var role = httpContext.Session.GetString("VaiTro");

            if (userId.HasValue && string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                base.OnActionExecuting(context);
                return;
            }

            var returnUrl = (httpContext.Request.Path + httpContext.Request.QueryString).ToString();
            var encodedReturnUrl = Uri.EscapeDataString(returnUrl);
            context.Result = new RedirectResult($"/wp-admin?returnUrl={encodedReturnUrl}");
        }
    }
}
