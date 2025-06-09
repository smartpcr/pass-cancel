using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace OwinWebApi.Filters
{
    public class CancellationActionFilter : ActionFilterAttribute
    {
        public override async Task OnActionExecutingAsync(HttpActionContext actionContext, CancellationToken cancellationToken)
        {
            // Store the cancellation token in the request properties so controllers can access it
            actionContext.Request.Properties["CancellationToken"] = cancellationToken;
            
            Console.WriteLine($"[CancellationFilter] Request started: {actionContext.Request.RequestUri}");
            
            await base.OnActionExecutingAsync(actionContext, cancellationToken);
        }

        public override async Task OnActionExecutedAsync(HttpActionExecutedContext actionContext, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[CancellationFilter] Request completed: {actionContext.Request.RequestUri}");
            
            await base.OnActionExecutedAsync(actionContext, cancellationToken);
        }
    }
    
    public static class HttpRequestMessageExtensions
    {
        /// <summary>
        /// Extension method to easily get the cancellation token from any controller
        /// </summary>
        public static CancellationToken GetCancellationToken(this System.Net.Http.HttpRequestMessage request)
        {
            if (request.Properties.TryGetValue("CancellationToken", out var token) && token is CancellationToken cancellationToken)
            {
                return cancellationToken;
            }
            return CancellationToken.None;
        }
    }
}