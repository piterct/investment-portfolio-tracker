using InvestmentPortfolioTracker.Business.Interfaces;
using InvestmentPortfolioTracker.Business.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InvestmentPortfolioTracker.Api.Controllers
{
    public class MainController(INotifier notifier) : ControllerBase
    {
        protected bool ValidOperation()
        {
            return !notifier.HasNotification();
        }

        protected ActionResult CustomResponse(ModelStateDictionary modelState)
        {
            if (!modelState.IsValid) NotifyInvalidModelError(modelState);
            return CustomResponse();
        }

        protected void NotifyInvalidModelError(ModelStateDictionary modelState)
        {
            var errors = modelState.Values.SelectMany(e => e.Errors);
            foreach (var error in errors)
            {
                var errorMessage = error.Exception == null ? error.ErrorMessage : error.Exception.Message;
                NotifyError(errorMessage);
            }
        }
        protected void NotifyError(string message)
        {
            notifier.Handle(new Notification(message));
        }

        protected ActionResult CustomResponse(object? result = null)
        {
            if (ValidOperation())
            {
                return Ok(new
                {
                    success = true,
                    data = result
                });
            }

            return BadRequest(new
            {
                success = false,
                erros = notifier.GetNotifications().Select(n => n.Message)
            });
        }

        protected ActionResult CustomResponseCreated(object? result = null)
        {
            if (ValidOperation())
            {
                return Created(string.Empty, new
                {
                    success = true,
                    data = result
                });
            }
            return BadRequest(new
            {
                success = false,
                erros = notifier.GetNotifications().Select(n => n.Message)
            });
        }

        protected ActionResult CustomResponseNotFound()
        {
            return NotFound(new
            {
                success = false,
                erros = notifier.GetNotifications().Select(n => n.Message)
            });
        }
    }
}
