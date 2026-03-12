using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SNIF.Core.DTOs;
using SNIF.Core.Interfaces;
using System.Security.Claims;

namespace SNIF.API.Controllers
{
    [ApiController]
    [Route("api/boosts")]
    [EnableRateLimiting("global")]
    public class BoostController : ControllerBase
    {
        private readonly IBoostService _boostService;
        private readonly IPaymentService _paymentService;

        public BoostController(IBoostService boostService, IPaymentService paymentService)
        {
            _boostService = boostService;
            _paymentService = paymentService;
        }

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        [Authorize]
        [HttpPost("purchase")]
        [ProducesResponseType(typeof(BoostPurchaseResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> PurchaseWithCredits([FromBody] PurchaseBoostWithCreditsDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var userId = GetUserId();
            var result = await _boostService.PurchaseWithCredits(userId, dto.BoostType, dto.DurationDays);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [Authorize]
        [HttpGet("active")]
        [ProducesResponseType(typeof(IReadOnlyList<BoostDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActiveBoosts()
        {
            var userId = GetUserId();
            return Ok(await _boostService.GetActiveBoosts(userId));
        }

        [Authorize]
        [HttpGet("available")]
        [ProducesResponseType(typeof(AvailableBoostsDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAvailableBoosts()
        {
            var userId = GetUserId();
            return Ok(await _boostService.GetAvailableBoosts(userId));
        }

        [Authorize]
        [HttpPost("checkout")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateDayPassCheckout([FromBody] CreateDayPassCheckoutDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var userId = GetUserId();
                var url = await _paymentService.CreateDayPassCheckoutSession(userId, dto);
                return Ok(new { url });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
