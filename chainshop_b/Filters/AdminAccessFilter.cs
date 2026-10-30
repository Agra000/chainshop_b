using chainshop_b.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Filters
{
    // Endpoint khusus admin (= arbiter refund). Role dibaca dari database (msusers.role = 'admin'),
    // jadi cukup ubah role di database lalu login ulang pakai wallet admin.
    public class AdminAccessFilter : IAsyncActionFilter
    {
        private readonly ApplicationDBContext _context;

        public AdminAccessFilter(ApplicationDBContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var claim = context.HttpContext.User.FindFirst("iduser")?.Value;
            if (!Guid.TryParse(claim, out var loginUserId) ||
                !await _context.MsUsers.AnyAsync(x => x.Id == loginUserId && x.Role == "admin"))
            {
                context.Result = new ObjectResult(new
                {
                    status = false,
                    message = "Admin access only"
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            await next();
        }
    }
}
