using Bizcore.BuildingBlocks.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Invoice.API.Filters
{
    public class HttpExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is DomainException domainException)
            {
                var result = new ObjectResult(new { error = domainException.Message })
                {
                    StatusCode = 400
                };
                context.Result = result;
                context.ExceptionHandled = true;
            }
        }
    }
}
