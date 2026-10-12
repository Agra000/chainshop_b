using chainshop_b.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Filters
{
    // Validasi kepemilikan data berdasarkan JWT:
    // - query "userId" harus sama dengan claim "iduser" di token
    // - query "sellerId" harus toko milik user yang login
    public class UserAccessFilter : IAsyncActionFilter
    {
        private readonly ApplicationDBContext _context;

        public UserAccessFilter(ApplicationDBContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var isAnonymous = context.ActionDescriptor.EndpointMetadata.Any(x => x is IAllowAnonymous);
            if (isAnonymous)
            {
                await next();
                return;
            }

            var claim = context.HttpContext.User.FindFirst("iduser")?.Value;
            if (!Guid.TryParse(claim, out var loginUserId))
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    status = false,
                    message = "Session expired, please login again"
                });
                return;
            }

            var query = context.HttpContext.Request.Query;

            if (query.TryGetValue("userId", out var userIdValue) && !string.IsNullOrWhiteSpace(userIdValue))
            {
                if (!Guid.TryParse(userIdValue, out var userId) || userId != loginUserId)
                {
                    context.Result = Forbidden();
                    return;
                }
            }

            if (query.TryGetValue("sellerId", out var sellerIdValue) && !string.IsNullOrWhiteSpace(sellerIdValue))
            {
                if (!Guid.TryParse(sellerIdValue, out var sellerId) ||
                    !await _context.MsSellers.AnyAsync(x => x.Id == sellerId && x.UserId == loginUserId))
                {
                    context.Result = Forbidden();
                    return;
                }
            }

            await next();
        }

        private static ObjectResult Forbidden()
        {
            return new ObjectResult(new
            {
                status = false,
                message = "You are not allowed to access this data"
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
