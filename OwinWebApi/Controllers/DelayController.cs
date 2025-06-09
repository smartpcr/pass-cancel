using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using OwinWebApi.Filters;

namespace OwinWebApi.Controllers
{
    public class DelayController : ApiController
    {
        /// <summary>
        /// Original method with explicit CancellationToken parameter
        /// </summary>
        [HttpGet]
        [Route("api/delay/{seconds}")]
        public async Task<HttpResponseMessage> Get(int seconds, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"[OWIN-DelayController] Starting delay of {seconds} seconds with explicit token...");
                await Task.Delay(seconds * 1000, cancellationToken);
                Console.WriteLine($"[OWIN-DelayController] Completed delay of {seconds} seconds");
                
                var response = new
                {
                    message = $"Completed after {seconds} seconds",
                    server = "OWIN Server (.NET 4.6.2)",
                    method = "DelayController with explicit CancellationToken"
                };
                
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[OWIN-DelayController] Request was cancelled by the client.");
                return Request.CreateResponse((HttpStatusCode)499); // Client Closed Request
            }
        }

        /// <summary>
        /// Alternative method WITHOUT CancellationToken parameter - uses extension method
        /// </summary>
        [HttpGet]
        [Route("api/delay-alt/{seconds}")]
        public async Task<HttpResponseMessage> GetAlternative(int seconds)
        {
            try
            {
                // Use extension method to get cancellation token from request
                var cancellationToken = Request.GetCancellationToken();
                
                Console.WriteLine($"[OWIN-DelayController-Alt] Starting delay of {seconds} seconds with extracted token...");
                await Task.Delay(seconds * 1000, cancellationToken);
                Console.WriteLine($"[OWIN-DelayController-Alt] Completed delay of {seconds} seconds");
                
                var response = new
                {
                    message = $"Completed after {seconds} seconds",
                    server = "OWIN Server (.NET 4.6.2)",
                    method = "DelayController with extracted CancellationToken"
                };
                
                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[OWIN-DelayController-Alt] Request was cancelled by the client.");
                return Request.CreateResponse((HttpStatusCode)499); // Client Closed Request
            }
        }
    }
}