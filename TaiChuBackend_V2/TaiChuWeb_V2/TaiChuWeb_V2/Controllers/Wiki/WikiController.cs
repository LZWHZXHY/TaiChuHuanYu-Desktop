using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.Wiki
{
    [ApiController]
    [Route("api/[controller]")]
    public class WikiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WikiController(AppDbContext context)
        {
            _context = context;
        }
    }
}
