using System.Web.Http;
using Microsoft.Owin;
using Owin;
using OwinWebApi.Filters;
using OwinWebApi.Middleware;

[assembly: OwinStartup(typeof(OwinWebApi.Startup))]

namespace OwinWebApi
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Add cancellation middleware first
            app.UseCancellationMiddleware();
            
            HttpConfiguration config = new HttpConfiguration();
            
            // Add global cancellation filter
            config.Filters.Add(new CancellationActionFilter());
            
            // Enable attribute routing
            config.MapHttpAttributeRoutes();
            
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.Formatters.JsonFormatter.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
            
            app.UseWebApi(config);
        }
    }
}