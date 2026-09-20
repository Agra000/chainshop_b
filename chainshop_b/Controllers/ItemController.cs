//using chainshop_b.Services;
//using Microsoft.AspNetCore.Mvc;
//using chainshop_b.Model.Dto.Request;

//namespace chainshop_b.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    //[Authorize]
//    public class ItemController : ControllerBase
//    {
//        private readonly ItemService _itemService;

//        public ItemController(ItemService itemService)
//        {
//            _itemService = itemService;
//        }

//        [HttpGet("get-all")]
//        public async Task<IActionResult> GetAllProducts()
//        {
//            var res = await _itemService.GetAllProducts();
//            return Ok(new
//            {
//                data = res,
//                message = "Success"
//            });
//        }

//        [HttpPost("add")]
//        public async Task<IActionResult> AddNewItem([FromBody] FormItemsRequest req)
//        {
//            //userId = Guid.Parse(User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value ?? Guid.Empty.ToString());
//            Guid userId = Guid.NewGuid();

//            var res = await _itemService.AddNewItem(userId, req);

//            if (!res.Status)
//            {
//                return BadRequest(new
//                {
//                    status = res.Status,
//                    message = res.Message
//                });
//            }
//            else
//            {
//                return Ok(new
//                {
//                    status = res.Status,
//                    message = res.Message
//                });
//            }
//        }

//        //[HttpGet("get-edit")]
//        //public async Task<IActionResult> GetEdit([FromQuery] Guid taskId)
//        //{
//        //    var res = await _taskService.GetEditTask(taskId);

//        //    if (res == null)
//        //    {
//        //        return Ok(new
//        //        {
//        //            data = new List<object>(),
//        //            message = "Tasks not found !"
//        //        });
//        //    }

//        //    return Ok(new
//        //    {
//        //        data = res,
//        //        message = "Success"
//        //    });
//        //}

//        //[HttpPut("update")]
//        //public async Task<IActionResult> Update([FromQuery] Guid userId, [FromQuery] Guid taskId, [FromBody] TaskRequest req)
//        //{
//        //    var res = await _taskService.EditTask(userId, taskId, req);

//        //    if (!res.Status)
//        //    {
//        //        return BadRequest(new
//        //        {
//        //            status = res.Status,
//        //            message = res.Message
//        //        });
//        //    }
//        //    else
//        //    {
//        //        return Ok(new
//        //        {
//        //            status = res.Status,
//        //            message = res.Message
//        //        });
//        //    }
//        //}
//    }
//}
