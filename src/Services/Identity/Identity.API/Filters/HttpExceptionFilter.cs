using Bizcore.BuildingBlocks.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Identity.API.Filters
{
    public class HttpExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            int statusCode;
            string error;

            switch (context.Exception)
            {
                case DomainException domainEx:
                    statusCode = 400;
                    error = domainEx.Message;
                    break;
                case UnauthorizedException unauthorizedEx:
                    statusCode = 401;
                    error = unauthorizedEx.Message;
                    break;
                case NotFoundException notFoundEx:
                    statusCode = 404;
                    error = notFoundEx.Message;
                    break;
                default:
                    return; // Let GlobalExceptionMiddleware handle the rest
            }

            context.Result = new ObjectResult(new { error }) { StatusCode = statusCode };
            context.ExceptionHandled = true;
        }
    }
}
