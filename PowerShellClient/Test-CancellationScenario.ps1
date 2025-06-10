#requires -version 5.1
<#
.SYNOPSIS
    PowerShell test script for cancellation scenarios (called by integration tests)
    
.DESCRIPTION
    This script is designed to be called by the .NET integration test suite
    to validate PowerShell client cancellation functionality on Windows.
    
.PARAMETER TestScenario
    The test scenario to execute (Basic, Cancellation, Advanced)
    
.PARAMETER ServerUrl
    The URL of the server to test against
    
.PARAMETER DelaySeconds
    Number of seconds for the delay endpoint (default: 10)
    
.PARAMETER SimulateCancel
    Whether to simulate cancellation after a delay
    
.EXAMPLE
    .\Test-CancellationScenario.ps1 -TestScenario Basic -ServerUrl "http://localhost:5103/delay/2"
    
.EXAMPLE
    .\Test-CancellationScenario.ps1 -TestScenario Cancellation -ServerUrl "http://localhost:5104/api/delay/10" -SimulateCancel
#>

param(
    [Parameter(Mandatory)]
    [ValidateSet("Basic", "Cancellation", "Advanced")]
    [string]$TestScenario,
    
    [Parameter(Mandatory)]
    [string]$ServerUrl,
    
    [int]$DelaySeconds = 10,
    
    [switch]$SimulateCancel
)

# Add required assemblies
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.Threading.Tasks

# Global variables
$global:httpClient = $null
$global:cancellationTokenSource = $null

function Initialize-HttpClient {
    $global:httpClient = New-Object System.Net.Http.HttpClient
    $global:httpClient.Timeout = [TimeSpan]::FromMinutes(5)
}

function Initialize-CancellationTokenSource {
    $global:cancellationTokenSource = New-Object System.Threading.CancellationTokenSource
}

function Test-BasicScenario {
    param([string]$Url)
    
    Write-Host "🧪 Testing basic HTTP call to: $Url" -ForegroundColor Cyan
    
    try {
        Initialize-HttpClient
        
        $response = $global:httpClient.GetAsync($Url).Result
        
        if ($response.IsSuccessStatusCode) {
            $content = $response.Content.ReadAsStringAsync().Result
            Write-Host "✅ Basic test successful: $($response.StatusCode)" -ForegroundColor Green
            Write-Host "Response: $content" -ForegroundColor Gray
            return $true
        } else {
            Write-Host "❌ Basic test failed: $($response.StatusCode)" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "💥 Basic test error: $_" -ForegroundColor Red
        return $false
    }
    finally {
        if ($global:httpClient) {
            $global:httpClient.Dispose()
        }
    }
}

function Test-CancellationScenario {
    param([string]$Url, [bool]$ShouldCancel)
    
    Write-Host "🧪 Testing cancellation scenario for: $Url" -ForegroundColor Cyan
    Write-Host "   Simulate Cancel: $ShouldCancel" -ForegroundColor Yellow
    
    try {
        Initialize-HttpClient
        Initialize-CancellationTokenSource
        
        if ($ShouldCancel) {
            # Schedule cancellation after 3 seconds
            $cancelTimer = [System.Threading.Timer]::new({
                Write-Host "🛑 Triggering cancellation after delay..." -ForegroundColor Red
                $global:cancellationTokenSource.Cancel()
            }, $null, 3000, [System.Threading.Timeout]::Infinite)
        }
        
        Write-Host "📡 Making HTTP request..." -ForegroundColor Blue
        $task = $global:httpClient.GetAsync($Url, $global:cancellationTokenSource.Token)
        
        try {
            $response = $task.Result
            Write-Host "✅ Request completed: $($response.StatusCode)" -ForegroundColor Green
            
            if ($ShouldCancel) {
                Write-Host "⚠️ Warning: Request completed despite cancellation request" -ForegroundColor Yellow
                return $false
            } else {
                return $true
            }
        }
        catch [System.OperationCanceledException] {
            Write-Host "✅ Request was successfully cancelled!" -ForegroundColor Green
            return $ShouldCancel
        }
        catch [System.AggregateException] {
            $innerEx = $_.Exception.InnerException
            if ($innerEx -is [System.OperationCanceledException]) {
                Write-Host "✅ Request was successfully cancelled (AggregateException)!" -ForegroundColor Green
                return $ShouldCancel
            } else {
                Write-Host "❌ Request failed with unexpected error: $($innerEx.Message)" -ForegroundColor Red
                return $false
            }
        }
        catch {
            Write-Host "❌ Request failed: $_" -ForegroundColor Red
            return $false
        }
        finally {
            if ($ShouldCancel -and $cancelTimer) {
                $cancelTimer.Dispose()
            }
        }
    }
    finally {
        if ($global:httpClient) {
            $global:httpClient.Dispose()
        }
        if ($global:cancellationTokenSource) {
            $global:cancellationTokenSource.Dispose()
        }
    }
}

function Test-AdvancedScenario {
    Write-Host "🧪 Testing advanced scenario with multiple servers..." -ForegroundColor Cyan
    
    try {
        Initialize-HttpClient
        Initialize-CancellationTokenSource
        
        # Test multiple endpoints
        $endpoints = @(
            "http://localhost:5103/delay/8",
            "http://localhost:5104/api/delay/8",
            "http://localhost:5104/api/delay-alt/8",
            "http://localhost:5104/api/example/with-token/8",
            "http://localhost:5104/api/example/without-token/8"
        )
        
        Write-Host "📡 Starting requests to multiple endpoints..." -ForegroundColor Blue
        $tasks = @()
        
        foreach ($endpoint in $endpoints) {
            Write-Host "   -> $endpoint" -ForegroundColor Gray
            $task = $global:httpClient.GetAsync($endpoint, $global:cancellationTokenSource.Token)
            $tasks += $task
        }
        
        # Cancel after 3 seconds
        Start-Sleep -Seconds 3
        Write-Host "🛑 Cancelling all requests..." -ForegroundColor Red
        $global:cancellationTokenSource.Cancel()
        
        # Check results
        $cancelledCount = 0
        $completedCount = 0
        
        foreach ($task in $tasks) {
            try {
                $result = $task.Result
                Write-Host "⚠️ Task completed unexpectedly: $($result.StatusCode)" -ForegroundColor Yellow
                $completedCount++
            }
            catch [System.OperationCanceledException] {
                Write-Host "✅ Task was cancelled successfully" -ForegroundColor Green
                $cancelledCount++
            }
            catch [System.AggregateException] {
                $innerEx = $_.Exception.InnerException
                if ($innerEx -is [System.OperationCanceledException]) {
                    Write-Host "✅ Task was cancelled successfully (AggregateException)" -ForegroundColor Green
                    $cancelledCount++
                } else {
                    Write-Host "❌ Task failed: $($innerEx.Message)" -ForegroundColor Red
                }
            }
            catch {
                Write-Host "❌ Task failed: $_" -ForegroundColor Red
            }
        }
        
        Write-Host "📊 Results: $cancelledCount cancelled, $completedCount completed" -ForegroundColor Cyan
        
        # Success if most requests were cancelled
        return $cancelledCount -ge ($tasks.Count / 2)
    }
    finally {
        if ($global:httpClient) {
            $global:httpClient.Dispose()
        }
        if ($global:cancellationTokenSource) {
            $global:cancellationTokenSource.Dispose()
        }
    }
}

# Main execution
try {
    Write-Host "🚀 PowerShell Cancellation Test Script" -ForegroundColor Green
    Write-Host "======================================" -ForegroundColor Green
    Write-Host "Scenario: $TestScenario" -ForegroundColor Yellow
    Write-Host "Server URL: $ServerUrl" -ForegroundColor Yellow
    Write-Host "Delay: $DelaySeconds seconds" -ForegroundColor Yellow
    Write-Host "Simulate Cancel: $SimulateCancel" -ForegroundColor Yellow
    Write-Host ""
    
    $success = $false
    
    switch ($TestScenario) {
        "Basic" {
            $success = Test-BasicScenario -Url $ServerUrl
        }
        "Cancellation" {
            $success = Test-CancellationScenario -Url $ServerUrl -ShouldCancel $SimulateCancel
        }
        "Advanced" {
            $success = Test-AdvancedScenario
        }
    }
    
    if ($success) {
        Write-Host "🎉 Test scenario '$TestScenario' completed successfully!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "❌ Test scenario '$TestScenario' failed!" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "💥 Test execution failed: $_" -ForegroundColor Red
    Write-Host "Stack trace: $($_.ScriptStackTrace)" -ForegroundColor Gray
    exit 1
}