using Fundoo.Notification.Application.Abstractions;
using Fundoo.Notification.Application.DTO;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notification.Api.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    public sealed class NotificationController(INotificationService notificationService) : ControllerBase
    {
        private readonly INotificationService _notificationService = notificationService;

        [HttpPost("password-reset")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendPasswordReset(
    [FromBody] PasswordResetNotificationRequest request,
    CancellationToken cancellationToken)
        {

            await _notificationService.SendPasswordResetNotificationAsync(
                request,
                cancellationToken);

            return Accepted(new
            {
                isSuccess = true,
                message = "Password-reset notification accepted."
            });
        }

        [HttpPost("welcome")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendWelcome([FromBody] WelcomeNotificationRequest request,CancellationToken cancellationToken)
        {
            await _notificationService.SendWelcomeNotificationAsync(request,cancellationToken);

            return Accepted(new
            {
                isSuccess = true,
                message = "Welcome notification accepted."
            });
        }
    }
}
