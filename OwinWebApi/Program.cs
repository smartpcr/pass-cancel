using System;
using Microsoft.Owin.Hosting;

namespace OwinWebApi
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseAddress = "http://localhost:5104/";

            // Start OWIN host 
            using (WebApp.Start<Startup>(url: baseAddress))
            {
                Console.WriteLine($"OWIN Server running at {baseAddress}");
                Console.WriteLine("Press any key to quit...");
                Console.ReadKey();
            }
        }
    }
}