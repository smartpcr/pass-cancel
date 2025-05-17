// See https://aka.ms/new-console-template for more information
using var httpClient = new HttpClient();

// Create a CancellationTokenSource that we can use to cancel the request.
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    Console.WriteLine("Ctrl+C pressed. Cancelling ongoing requests...");
    cts.Cancel();  // Cancel the cancellation token
    e.Cancel = true;  // Prevent immediate termination
};

while (!cts.Token.IsCancellationRequested)
{
    try
    {
        Console.WriteLine("Sending request to the long-running endpoint...");

        // Send the GET request with the cancellation token.
        HttpResponseMessage response = await httpClient.GetAsync(
            "http://localhost:5103/weatherforecast",
            cts.Token
        );

        Console.WriteLine("Response received: " + response.StatusCode);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Request was canceled.");
        // Break the loop if cancellation is signaled.
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine("An error occurred: " + ex.Message);
    }
}

