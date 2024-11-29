using Microsoft.AspNetCore.Mvc;
using SNIF.Infrastructure.Data;

namespace SNIF.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly SNIFContext _context;

        public HealthController(SNIFContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var dbConnectionWorking = await _context.Database.CanConnectAsync();
            
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                database = dbConnectionWorking ? "connected" : "disconnected"
            });
        }
    }
}