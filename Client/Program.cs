using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5); // Set a long timeout to allow for testing

        // Create a CancellationTokenSource that we can use to cancel the request.
        using var cts = new CancellationTokenSource();
        
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("\nCtrl+C pressed. Cancelling ongoing requests...");
            cts.Cancel();  // Cancel the cancellation token
            e.Cancel = true;  // Prevent immediate termination
        };

        while (!cts.Token.IsCancellationRequested)
        {
            Console.WriteLine("\nSelect an option:");
            Console.WriteLine("1. Call .NET Core Server (http://localhost:5103/delay/10)");
            Console.WriteLine("2. Call OWIN Server (http://localhost:5104/api/delay/10)");
            Console.WriteLine("3. Call both servers in parallel");
            Console.WriteLine("4. Exit");
            Console.Write("Enter choice (1-4): ");

            var choice = Console.ReadLine();
            
            if (choice == "4" || cts.Token.IsCancellationRequested)
                break;

            Console.WriteLine("\nPress Ctrl+C during the request to test cancellation...\n");

            try
            {
                switch (choice)
                {
                    case "1":
                        await CallNetCoreServer(httpClient, cts.Token);
                        break;
                    case "2":
                        await CallOwinServer(httpClient, cts.Token);
                        break;
                    case "3":
                        await CallBothServers(httpClient, cts.Token);
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nOperation was cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nAn error occurred: {ex.Message}");
            }
        }

        Console.WriteLine("\nExiting...");
    }

    static async Task CallNetCoreServer(HttpClient httpClient, CancellationToken cancellationToken)
    {
        Console.WriteLine("Calling .NET Core Server...");
        var url = "http://localhost:5103/delay/10";
        
        var response = await httpClient.GetAsync(url, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response from .NET Core Server: {content}");
        }
        else
        {
            Console.WriteLine($"Response status: {response.StatusCode} ({(int)response.StatusCode})");
        }
    }

    static async Task CallOwinServer(HttpClient httpClient, CancellationToken cancellationToken)
    {
        Console.WriteLine("Calling OWIN Server...");
        var url = "http://localhost:5104/api/delay/10";
        
        var response = await httpClient.GetAsync(url, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response from OWIN Server: {content}");
        }
        else
        {
            Console.WriteLine($"Response status: {response.StatusCode} ({(int)response.StatusCode})");
        }
    }

    static async Task CallBothServers(HttpClient httpClient, CancellationToken cancellationToken)
    {
        Console.WriteLine("Calling both servers in parallel...");
        
        var netCoreTask = Task.Run(async () =>
        {
            try
            {
                await CallNetCoreServer(httpClient, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($".NET Core Server error: {ex.Message}");
            }
        });

        var owinTask = Task.Run(async () =>
        {
            try
            {
                await CallOwinServer(httpClient, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OWIN Server error: {ex.Message}");
            }
        });

        await Task.WhenAll(netCoreTask, owinTask);
    }
}