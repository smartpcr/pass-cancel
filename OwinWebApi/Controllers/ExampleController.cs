using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Owin;
using Microsoft.Owin;
using OwinWebApi.Filters;
using OwinWebApi.Middleware;

namespace OwinWebApi.Controllers
{
    public class ExampleController : ApiController
    {
        /// <summary>
        /// Method WITH CancellationToken parameter - traditional approach
        /// </summary>
        [HttpGet]
        [Route("api/example/with-token/{seconds}")]
        public async Task<IHttpActionResult> WithCancellationToken(int seconds, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"[WithToken] Starting {seconds}s delay with explicit CancellationToken");
                await Task.Delay(seconds * 1000, cancellationToken);
                Console.WriteLine($"[WithToken] Completed {seconds}s delay");
                
                return Ok(new { 
                    message = $"Completed after {seconds} seconds", 
                    method = "WithCancellationToken",
                    server = "OWIN Server (.NET 4.6.2)"
                });
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[WithToken] Request was cancelled");
                return StatusCode((System.Net.HttpStatusCode)499);
            }
        }

        /// <summary>
        /// Method WITHOUT CancellationToken parameter - uses extension method to get token from request
        /// </summary>
        [HttpGet]
        [Route("api/example/without-token/{seconds}")]
        public async Task<IHttpActionResult> WithoutCancellationToken(int seconds)
        {
            try
            {
                // Get cancellation token from the request using extension method
                var cancellationToken = Request.GetCancellationToken();
                
                Console.WriteLine($"[WithoutToken] Starting {seconds}s delay using extracted CancellationToken");
                await Task.Delay(seconds * 1000, cancellationToken);
                Console.WriteLine($"[WithoutToken] Completed {seconds}s delay");
                
                return Ok(new { 
                    message = $"Completed after {seconds} seconds", 
                    method = "WithoutCancellationToken (using extension)",
                    server = "OWIN Server (.NET 4.6.2)"
                });
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[WithoutToken] Request was cancelled");
                return StatusCode((System.Net.HttpStatusCode)499);
            }
        }

        /// <summary>
        /// Method WITHOUT CancellationToken parameter - uses OWIN environment to get token
        /// </summary>
        [HttpGet]
        [Route("api/example/owin-context/{seconds}")]
        public async Task<IHttpActionResult> UsingOwinContext(int seconds)
        {
            try
            {
                // Get cancellation token from OWIN environment via request properties
                var cancellationToken = CancellationToken.None;
                if (Request.Properties.TryGetValue("MS_OwinEnvironment", out var environment) && 
                    environment is System.Collections.Generic.IDictionary<string, object> owinEnv &&
                    owinEnv.TryGetValue("CancellationToken", out var token) && 
                    token is CancellationToken ct)
                {
                    cancellationToken = ct;
                }
                else
                {
                    // Fallback to the request cancellation token
                    cancellationToken = Request.GetCancellationToken();
                }
                
                Console.WriteLine($"[OwinContext] Starting {seconds}s delay using OWIN environment CancellationToken");
                await Task.Delay(seconds * 1000, cancellationToken);
                Console.WriteLine($"[OwinContext] Completed {seconds}s delay");
                
                return Ok(new { 
                    message = $"Completed after {seconds} seconds", 
                    method = "UsingOwinContext",
                    server = "OWIN Server (.NET 4.6.2)"
                });
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[OwinContext] Request was cancelled");
                return StatusCode((System.Net.HttpStatusCode)499);
            }
        }

        /// <summary>
        /// Method WITHOUT any cancellation handling - relies on middleware/filter
        /// </summary>
        [HttpGet]
        [Route("api/example/no-cancellation/{seconds}")]
        public async Task<IHttpActionResult> NoCancellationHandling(int seconds)
        {
            // This method doesn't handle cancellation explicitly
            // The middleware will still detect client disconnection and cancel the request
            
            Console.WriteLine($"[NoCancellation] Starting {seconds}s delay WITHOUT explicit cancellation handling");
            
            // Simulate work without cancellation token
            for (int i = 0; i < seconds; i++)
            {
                await Task.Delay(1000); // No cancellation token passed
                Console.WriteLine($"[NoCancellation] Progress: {i + 1}/{seconds} seconds");
            }
            
            Console.WriteLine($"[NoCancellation] Completed {seconds}s delay");
            
            return Ok(new { 
                message = $"Completed after {seconds} seconds", 
                method = "NoCancellationHandling (middleware will handle)",
                server = "OWIN Server (.NET 4.6.2)"
            });
        }
    }
}