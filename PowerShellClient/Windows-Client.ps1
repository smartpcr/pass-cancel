#requires -version 5.1
<#
.SYNOPSIS
    Windows PowerShell API Client with Cancellation Support (.NET Framework 4.6.2 compatible)
    
.DESCRIPTION
    A Windows PowerShell script that can call both .NET Core and OWIN servers
    with proper cancellation token support when Ctrl+C is pressed.
    Designed for Windows PowerShell 5.1 and .NET Framework 4.6.2.
#>

# Add required assemblies for .NET Framework
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.Threading.Tasks

# Global variables
$global:httpClient = New-Object System.Net.Http.HttpClient
$global:httpClient.Timeout = [TimeSpan]::FromMinutes(5)
$global:cancellationTokenSource = $null

function Invoke-ApiCall {
    param(
        [string]$Url,
        [string]$ServerName,
        [System.Threading.CancellationToken]$CancellationToken
    )
    
    try {
        Write-Host "Calling $ServerName at: $Url" -ForegroundColor Cyan
        
        # Create the HTTP request task
        $task = $global:httpClient.GetAsync($Url, $CancellationToken)
        
        # Wait for completion with cancellation support (blocking call for .NET Framework)
        $response = $task.Result
        
        if ($response.IsSuccessStatusCode) {
            $contentTask = $response.Content.ReadAsStringAsync()
            $content = $contentTask.Result
            Write-Host "✓ Response from $ServerName`: $content" -ForegroundColor Green
        }
        else {
            Write-Host "⚠ Response status: $($response.StatusCode) ($([int]$response.StatusCode))" -ForegroundColor Yellow
        }
    }
    catch [System.OperationCanceledException] {
        Write-Host "✗ Request to $ServerName was cancelled." -ForegroundColor Red
        throw
    }
    catch [System.AggregateException] {
        $innerEx = $_.Exception.InnerException
        if ($innerEx -is [System.OperationCanceledException]) {
            Write-Host "✗ Request to $ServerName was cancelled." -ForegroundColor Red
            throw $innerEx
        }
        else {
            Write-Host "✗ Error calling $ServerName`: $($innerEx.Message)" -ForegroundColor Red
            throw
        }
    }
    catch {
        Write-Host "✗ Error calling $ServerName`: $_" -ForegroundColor Red
        throw
    }
}

function Invoke-NetCoreServer {
    param([System.Threading.CancellationToken]$CancellationToken)
    Invoke-ApiCall -Url "http://localhost:5103/delay/10" -ServerName ".NET Core Server" -CancellationToken $CancellationToken
}

function Invoke-OwinServer {
    param([System.Threading.CancellationToken]$CancellationToken)
    Invoke-ApiCall -Url "http://localhost:5104/api/delay/10" -ServerName "OWIN Server (with token)" -CancellationToken $CancellationToken
}

function Invoke-OwinServerAlt {
    param([System.Threading.CancellationToken]$CancellationToken)
    Invoke-ApiCall -Url "http://localhost:5104/api/delay-alt/10" -ServerName "OWIN Server (without token)" -CancellationToken $CancellationToken
}

function Invoke-OwinExampleWithToken {
    param([System.Threading.CancellationToken]$CancellationToken)
    Invoke-ApiCall -Url "http://localhost:5104/api/example/with-token/10" -ServerName "OWIN Example (with token)" -CancellationToken $CancellationToken
}

function Invoke-OwinExampleWithoutToken {
    param([System.Threading.CancellationToken]$CancellationToken)
    Invoke-ApiCall -Url "http://localhost:5104/api/example/without-token/10" -ServerName "OWIN Example (without token)" -CancellationToken $CancellationToken
}

function Invoke-OwinExampleOwinContext {
    param([System.Threading.CancellationToken]$CancellationToken)
    Invoke-ApiCall -Url "http://localhost:5104/api/example/owin-context/10" -ServerName "OWIN Example (OWIN context)" -CancellationToken $CancellationToken
}

function Invoke-OwinExampleNoCancellation {
    param([System.Threading.CancellationToken]$CancellationToken)
    Invoke-ApiCall -Url "http://localhost:5104/api/example/no-cancellation/10" -ServerName "OWIN Example (no cancellation)" -CancellationToken $CancellationToken
}

function Invoke-BothServersParallel {
    param([System.Threading.CancellationToken]$CancellationToken)
    
    Write-Host "Calling both servers in parallel..." -ForegroundColor Blue
    
    # Create background jobs for parallel execution (Windows PowerShell approach)
    $jobs = @()
    
    $jobs += Start-Job -ScriptBlock {
        param($Token, $ScriptPath)
        
        # Import functions from this script
        . $ScriptPath
        
        try {
            Invoke-NetCoreServer -CancellationToken $Token
        }
        catch {
            Write-Host ".NET Core Server error: $_" -ForegroundColor Red
        }
    } -ArgumentList $CancellationToken, $PSCommandPath
    
    $jobs += Start-Job -ScriptBlock {
        param($Token, $ScriptPath)
        
        # Import functions from this script
        . $ScriptPath
        
        try {
            Invoke-OwinServer -CancellationToken $Token
        }
        catch {
            Write-Host "OWIN Server error: $_" -ForegroundColor Red
        }
    } -ArgumentList $CancellationToken, $PSCommandPath
    
    # Wait for jobs with cancellation support
    try {
        $timeout = 120 # 2 minutes timeout
        $jobs | Wait-Job -Timeout $timeout | Out-Null
        
        # Get job results
        $jobs | Receive-Job
    }
    finally {
        # Cleanup jobs
        $jobs | Stop-Job -ErrorAction SilentlyContinue
        $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
    }
}

# Register Ctrl+C handler
$null = Register-EngineEvent PowerShell.Exiting -Action {
    if ($null -ne $global:cancellationTokenSource) {
        Write-Host "`nShutdown signal received. Cancelling ongoing requests..." -ForegroundColor Red
        $global:cancellationTokenSource.Cancel()
    }
}

Write-Host "`n🚀 Windows PowerShell API Client (.NET Framework 4.6.2)" -ForegroundColor Green
Write-Host "=======================================================" -ForegroundColor Green
Write-Host "📝 Instructions:" -ForegroundColor Yellow
Write-Host "   • Press Ctrl+C during a request to cancel it" -ForegroundColor Gray
Write-Host "   • Both servers must be running on ports 5103 and 5104" -ForegroundColor Gray
Write-Host "   • Designed for Windows PowerShell 5.1" -ForegroundColor Gray
Write-Host ""

try {
    while ($true) {
        Write-Host "`nSelect an option:" -ForegroundColor Yellow
        Write-Host "1️⃣  Call .NET Core Server (http://localhost:5103/delay/10)"
        Write-Host "2️⃣  Call OWIN Server - with CancellationToken (http://localhost:5104/api/delay/10)"
        Write-Host "3️⃣  Call OWIN Server - without CancellationToken (http://localhost:5104/api/delay-alt/10)"
        Write-Host "4️⃣  Call OWIN Example - with CancellationToken (http://localhost:5104/api/example/with-token/10)"
        Write-Host "5️⃣  Call OWIN Example - without CancellationToken (http://localhost:5104/api/example/without-token/10)"
        Write-Host "6️⃣  Call OWIN Example - using OWIN context (http://localhost:5104/api/example/owin-context/10)"
        Write-Host "7️⃣  Call OWIN Example - no cancellation handling (http://localhost:5104/api/example/no-cancellation/10)"
        Write-Host "8️⃣  Call both servers in parallel"
        Write-Host "9️⃣  Exit"
        Write-Host -NoNewline "Enter choice (1-9): "
        
        $choice = Read-Host
        
        if ($choice -eq '9') {
            break
        }
        
        # Create new cancellation token source for each request
        $global:cancellationTokenSource = New-Object System.Threading.CancellationTokenSource
        
        Write-Host "`n⏱️  Press Ctrl+C during the request to test cancellation...`n" -ForegroundColor Cyan
        
        try {
            switch ($choice) {
                '1' { 
                    Invoke-NetCoreServer -CancellationToken $global:cancellationTokenSource.Token 
                }
                '2' { 
                    Invoke-OwinServer -CancellationToken $global:cancellationTokenSource.Token 
                }
                '3' { 
                    Invoke-OwinServerAlt -CancellationToken $global:cancellationTokenSource.Token 
                }
                '4' { 
                    Invoke-OwinExampleWithToken -CancellationToken $global:cancellationTokenSource.Token 
                }
                '5' { 
                    Invoke-OwinExampleWithoutToken -CancellationToken $global:cancellationTokenSource.Token 
                }
                '6' { 
                    Invoke-OwinExampleOwinContext -CancellationToken $global:cancellationTokenSource.Token 
                }
                '7' { 
                    Invoke-OwinExampleNoCancellation -CancellationToken $global:cancellationTokenSource.Token 
                }
                '8' { 
                    Invoke-BothServersParallel -CancellationToken $global:cancellationTokenSource.Token 
                }
                default { 
                    Write-Host "❌ Invalid choice. Please try again." -ForegroundColor Red 
                }
            }
        }
        catch [System.OperationCanceledException] {
            Write-Host "`n🛑 Operation was cancelled by user." -ForegroundColor Red
        }
        catch {
            Write-Host "`n💥 An error occurred: $_" -ForegroundColor Red
        }
        finally {
            # Cleanup cancellation token source
            if ($null -ne $global:cancellationTokenSource) {
                $global:cancellationTokenSource.Dispose()
                $global:cancellationTokenSource = $null
            }
        }
    }
}
finally {
    Write-Host "`n👋 Exiting Windows PowerShell client..." -ForegroundColor Yellow
    
    # Cleanup
    if ($null -ne $global:httpClient) {
        $global:httpClient.Dispose()
    }
    
    # Unregister event handler
    Unregister-Event -SourceIdentifier PowerShell.Exiting -ErrorAction SilentlyContinue
}