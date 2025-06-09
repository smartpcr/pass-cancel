# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET demonstration project showing proper cancellation token handling in HTTP requests. It contains:
- **Server**: ASP.NET Core 9.0 minimal API with a long-running `/weatherforecast` endpoint that respects cancellation tokens
- **Client**: Console application that sends HTTP requests and can cancel them via Ctrl+C
- **OwinWebApi**: Legacy .NET Framework 4.6.2 Web API 2 with OWIN self-hosting

## Essential Commands

```bash
# Build entire solution
dotnet build

# Run the server (required for client to work)
dotnet run --project Server

# Run the client (in a separate terminal)
dotnet run --project Client

# Test the server endpoint
# Use the Server/Server.http file with REST Client extension
# Or: curl http://localhost:5103/weatherforecast
```

## Architecture

The solution demonstrates cancellation token propagation:
1. **Server** (`/weatherforecast`): Simulates a long-running operation (10 iterations with 5-second delays) that checks for cancellation via `context.RequestAborted`
2. **Client**: Uses `CancellationTokenSource` with Ctrl+C handler to cancel HTTP requests
3. **Central Package Management**: Uses `Directory.Packages.props` for consistent versioning across projects

The server properly handles `OperationCanceledException` and logs cancellation events. The client catches cancellations gracefully and exits the request loop.