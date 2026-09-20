using chainshop_b.Model.Dto.Request;
using chainshop_b.Services;
using Microsoft.AspNetCore.Mvc;

namespace chainshop_b.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class CategoryController : ControllerBase
    {
        private readonly CategoryService _categoryService;

        public CategoryController(CategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllCategories()
        {
            var res = await _categoryService.GetAllCategories();
            return Ok(new
            {
                data = res,
                message = "Success"
            });
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddNewCategory([FromQuery] Guid userId, [FromBody] CategoryRequest req)
        {
            var res = await _categoryService.AddNewCategory(userId, req);
            return Ok(new
            {
                status = res.Status,
                message = res.Message
            });
        }

        //[HttpGet("get-edit")]
        //public async Task<IActionResult> GetEdit([FromQuery] Guid taskId)
        //{
        //    var res = await _taskService.GetEditTask(taskId);

        //    if (res == null)
        //    {
        //        return Ok(new
        //        {
        //            data = new List<object>(),
        //            message = "Tasks not found !"
        //        });
        //    }

        //    return Ok(new
        //    {
        //        data = res,
        //        message = "Success"
        //    });
        //}

        //[HttpPut("update")]
        //public async Task<IActionResult> Update([FromQuery] Guid userId, [FromQuery] Guid taskId, [FromBody] TaskRequest req)
        //{
        //    var res = await _taskService.EditTask(userId, taskId, req);

        //    if (!res.Status)
        //    {
        //        return BadRequest(new
        //        {
        //            status = res.Status,
        //            message = res.Message
        //        });
        //    }
        //    else
        //    {
        //        return Ok(new
        //        {
        //            status = res.Status,
        //            message = res.Message
        //        });
        //    }
        //}
    }
}
