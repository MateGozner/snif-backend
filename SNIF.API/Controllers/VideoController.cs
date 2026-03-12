using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace SNIF.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/video")]
    [EnableRateLimiting("global")]
    public class VideoController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public VideoController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("ice-servers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetIceServers()
        {
            var stunUrl = _configuration["IceServers:StunUrl"] ?? "stun:stun.l.google.com:19302";
            var turnUrl = _configuration["IceServers:TurnUrl"];
            var turnUsername = _configuration["IceServers:TurnUsername"];
            var turnCredential = _configuration["IceServers:TurnCredential"];

            var iceServers = new List<object>
            {
                new { urls = stunUrl }
            };

            if (!string.IsNullOrEmpty(turnUrl))
            {
                iceServers.Add(new
                {
                    urls = turnUrl,
                    username = turnUsername,
                    credential = turnCredential
                });
            }

            return Ok(new { iceServers });
        }
    }
}
