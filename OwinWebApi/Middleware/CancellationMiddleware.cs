using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;

namespace OwinWebApi.Middleware
{
    public class CancellationMiddleware : OwinMiddleware
    {
        public CancellationMiddleware(OwinMiddleware next) : base(next)
        {
        }

        public override async Task Invoke(IOwinContext context)
        {
            // Create a cancellation token source for this request
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(context.Request.CallCancelled))
            {
                // Store the cancellation token in the OWIN environment
                context.Set("CancellationToken", cts.Token);
                
                try
                {
                    // Monitor for client disconnection
                    var disconnectionTask = MonitorClientDisconnection(context, cts);
                    
                    // Execute the next middleware
                    await Next.Invoke(context);
                    
                    // Cancel the monitoring task
                    cts.Cancel();
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    Console.WriteLine("[CancellationMiddleware] Request was cancelled by client");
                    context.Response.StatusCode = 499; // Client Closed Request
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CancellationMiddleware] Error: {ex.Message}");
                    throw;
                }
            }
        }

        private async Task MonitorClientDisconnection(IOwinContext context, CancellationTokenSource cts)
        {
            try
            {
                // Monitor the original cancellation token (client disconnection)
                await Task.Delay(Timeout.Infinite, context.Request.CallCancelled);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[CancellationMiddleware] Client disconnected, cancelling request");
                cts.Cancel();
            }
        }
    }
    
    public static class CancellationMiddlewareExtensions
    {
        public static void UseCancellationMiddleware(this Owin.IAppBuilder app)
        {
            app.Use(typeof(CancellationMiddleware));
        }
        
        /// <summary>
        /// Extension method to get cancellation token from OWIN context
        /// </summary>
        public static CancellationToken GetCancellationToken(this IOwinContext context)
        {
            return context.Get<CancellationToken>("CancellationToken");
        }
    }
}