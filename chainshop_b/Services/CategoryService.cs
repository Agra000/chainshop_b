using chainshop_b.Data;
using chainshop_b.Model;
using chainshop_b.Model.Dto.Request;
using chainshop_b.Model.Dto.Response;
using Microsoft.EntityFrameworkCore;

namespace chainshop_b.Services
{
    public class CategoryService
    {
        private readonly ApplicationDBContext _context;

        public CategoryService(ApplicationDBContext context)
        {
            _context = context;
        }

        public async Task<List<GetAllCategoriesResponse>> GetAllCategories()
        {
            try
            {
                return await _context.MsCategories.Select(x => new GetAllCategoriesResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    Slug = x.Slug,
                    UpdatedAt = x.UpdatedAt,
                    UpdatedBy = x.UpdatedBy,
                }).ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<GetAllCategoriesResponse>();
            }
        }

        public async Task<ResultMessageResponse> AddNewCategory(Guid userId, CategoryRequest req)
        {
            try
            {
                var user = await _context.MsUsers.AnyAsync(x => x.Id == userId);
                if (!user)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "User not found"
                    };
                }

                var category = await _context.MsCategories.AnyAsync(x => x.Name == req.Name);
                if (category)
                {
                    return new ResultMessageResponse
                    {
                        Status = false,
                        Message = "Category already exists"
                    };
                }

                var newItem = new MsCategories
                {
                    Name = req.Name,
                    Slug = req.Slug,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = userId
                };

                _context.MsCategories.Add(newItem);
                await _context.SaveChangesAsync();

                return new ResultMessageResponse
                {
                    Status = true,
                    Message = "Category added successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResultMessageResponse
                {
                    Status = false,
                    Message = $"Server error occurred while adding item"
                };
            }
        }
    }
}
