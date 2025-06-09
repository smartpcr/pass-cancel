#!/usr/bin/env pwsh
<#
.SYNOPSIS
    PowerShell API Client Launcher
    
.DESCRIPTION
    Launches the PowerShell API client that can call both .NET Core and OWIN servers
    with proper cancellation token support when Ctrl+C is pressed.
    
.EXAMPLE
    .\Start-Client.ps1
    
.NOTES
    Make sure both servers are running:
    - .NET Core Server: dotnet run --project ../Server
    - OWIN Server: dotnet run --project ../OwinWebApi
#>

param()

# Import the module
Import-Module $PSScriptRoot\PowerShellClient.psd1 -Force

# Start the client
Start-ApiClient