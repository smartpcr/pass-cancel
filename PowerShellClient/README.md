# PowerShell API Client with Cancellation Support

This PowerShell client demonstrates how to call Web APIs with proper cancellation token support when Ctrl+C is pressed.

## Features

- ✅ Call .NET Core Server (port 5103)
- ✅ Call OWIN Server (.NET Framework 4.6.2, port 5104)
- ✅ Call both servers in parallel
- ✅ Proper cancellation token propagation
- ✅ Ctrl+C handling for request cancellation
- ✅ Visual feedback with colored output

## Files

- **Simple-Client.ps1** - Simple standalone script (recommended)
- **PowerShellClient.psm1** - PowerShell module version
- **PowerShellClient.psd1** - Module manifest
- **Start-Client.ps1** - Module launcher script

## Usage

### Option 1: Run the Simple Client (Recommended)

```powershell
# Navigate to the PowerShellClient directory
cd PowerShellClient

# Run the simple client
pwsh ./Simple-Client.ps1
# or on Windows:
.\Simple-Client.ps1
```

### Option 2: Use the Module

```powershell
# Import the module
Import-Module .\PowerShellClient.psd1

# Start the client
Start-ApiClient
```

### Option 3: Use the Launcher

```powershell
pwsh ./Start-Client.ps1
```

## Prerequisites

1. **PowerShell Core (pwsh)** - Recommended for cross-platform support
2. **Both servers must be running:**
   - .NET Core Server: `dotnet run --project ../Server` (port 5103)
   - OWIN Server: `dotnet run --project ../OwinWebApi` (port 5104)

## How Cancellation Works

1. When you press **Ctrl+C** during a request, the PowerShell script:
   - Catches the cancellation signal
   - Cancels the `CancellationTokenSource`
   - The `HttpClient.GetAsync()` method receives the cancelled token
   - The HTTP connection is terminated immediately

2. On the server side:
   - The cancellation is detected via the `CancellationToken` parameter
   - The server stops processing and returns HTTP status code 499 (Client Closed Request)

## Testing

1. Start both servers in separate terminals
2. Run the PowerShell client
3. Choose an option (1, 2, or 3)
4. **Press Ctrl+C during the 10-second delay** to test cancellation
5. Observe that both client and server detect the cancellation

## Example Output

```
🚀 PowerShell API Client with Cancellation Support
===================================================
📝 Instructions:
   • Press Ctrl+C during a request to cancel it
   • Both servers must be running on ports 5103 and 5104

Select an option:
1️⃣  Call .NET Core Server (http://localhost:5103/delay/10)
2️⃣  Call OWIN Server (http://localhost:5104/api/delay/10)
3️⃣  Call both servers in parallel
4️⃣  Exit
Enter choice (1-4): 1

⏱️  Press Ctrl+C during the request to test cancellation...

Calling .NET Core Server at: http://localhost:5103/delay/10
^C
✗ Request to .NET Core Server was cancelled.

🛑 Operation was cancelled by user.
```