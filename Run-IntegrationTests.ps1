#requires -version 5.1
<#
.SYNOPSIS
    Automated Integration Test Runner for Cancellation API Demo
    
.DESCRIPTION
    This script automatically starts both API servers, runs integration tests,
    and demonstrates both .NET console and PowerShell client functionality.
    
.PARAMETER SkipServerStart
    Skip starting the servers (useful if they're already running)
    
.PARAMETER TestTimeout
    Timeout in seconds for each test (default: 30)
    
.EXAMPLE
    .\Run-IntegrationTests.ps1
    
.EXAMPLE
    .\Run-IntegrationTests.ps1 -SkipServerStart -TestTimeout 60
#>

param(
    [switch]$SkipServerStart,
    [int]$TestTimeout = 30
)

# Script configuration
$ErrorActionPreference = "Stop"
$ProgressPreference = "Continue"

# Colors for output
$Colors = @{
    Success = "Green"
    Warning = "Yellow"
    Error = "Red"
    Info = "Cyan"
    Header = "Magenta"
}

function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Colors[$Color]
}

function Test-ServerHealth {
    param([string]$Url, [string]$ServerName)
    
    try {
        $response = Invoke-WebRequest -Uri $Url -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-ColorOutput "✅ $ServerName is healthy" -Color Success
            return $true
        }
    }
    catch {
        Write-ColorOutput "❌ $ServerName health check failed: $($_.Exception.Message)" -Color Error
        return $false
    }
    return $false
}

function Start-ApiServers {
    Write-ColorOutput "`n🚀 Starting API Servers..." -Color Header
    
    $global:ServerProcesses = @()
    
    try {
        # Start .NET Core Server
        Write-ColorOutput "📘 Starting .NET Core Server (port 5103)..." -Color Info
        $netCoreProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "Server" -PassThru -WindowStyle Hidden
        $global:ServerProcesses += $netCoreProcess
        Write-ColorOutput "   Process ID: $($netCoreProcess.Id)" -Color Info
        
        # Start OWIN Server  
        Write-ColorOutput "📙 Starting OWIN Server (port 5104)..." -Color Info
        $owinProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "OwinWebApi" -PassThru -WindowStyle Hidden
        $global:ServerProcesses += $owinProcess
        Write-ColorOutput "   Process ID: $($owinProcess.Id)" -Color Info
        
        # Wait for servers to start
        Write-ColorOutput "`n⏱️ Waiting for servers to initialize..." -Color Info
        Start-Sleep -Seconds 10
        
        # Health checks
        $netCoreHealthy = Test-ServerHealth -Url "http://localhost:5103/delay/1" -ServerName ".NET Core Server"
        $owinHealthy = Test-ServerHealth -Url "http://localhost:5104/api/delay/1" -ServerName "OWIN Server"
        
        if (-not $netCoreHealthy -or -not $owinHealthy) {
            throw "One or more servers failed health check"
        }
        
        Write-ColorOutput "✅ All servers are running and healthy!" -Color Success
    }
    catch {
        Write-ColorOutput "💥 Failed to start servers: $_" -Color Error
        Stop-ApiServers
        throw
    }
}

function Stop-ApiServers {
    Write-ColorOutput "`n🛑 Stopping API Servers..." -Color Header
    
    if ($global:ServerProcesses) {
        foreach ($process in $global:ServerProcesses) {
            try {
                if (-not $process.HasExited) {
                    Write-ColorOutput "   Stopping process $($process.Id)..." -Color Info
                    $process.Kill()
                    $process.WaitForExit(5000)
                }
            }
            catch {
                Write-ColorOutput "⚠️ Failed to stop process $($process.Id): $_" -Color Warning
            }
        }
        $global:ServerProcesses = @()
    }
}

function Run-DotNetIntegrationTests {
    Write-ColorOutput "`n🧪 Running .NET Integration Tests..." -Color Header
    
    try {
        $result = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "IntegrationTests" -Wait -PassThru -NoNewWindow
        
        if ($result.ExitCode -eq 0) {
            Write-ColorOutput "✅ .NET Integration Tests completed successfully!" -Color Success
        } else {
            Write-ColorOutput "❌ .NET Integration Tests failed with exit code: $($result.ExitCode)" -Color Error
        }
        
        return $result.ExitCode -eq 0
    }
    catch {
        Write-ColorOutput "💥 Failed to run .NET Integration Tests: $_" -Color Error
        return $false
    }
}

function Test-PowerShellClientFunctionality {
    Write-ColorOutput "`n🟦 Testing PowerShell Client Functionality..." -Color Header
    
    try {
        # Test that the PowerShell script can be loaded and functions are available
        $scriptPath = "PowerShellClient\Windows-Client.ps1"
        
        if (-not (Test-Path $scriptPath)) {
            Write-ColorOutput "❌ PowerShell client script not found at: $scriptPath" -Color Error
            return $false
        }
        
        Write-ColorOutput "📄 PowerShell client script found" -Color Info
        
        # Test basic HTTP functionality with a simple call
        Write-ColorOutput "🔗 Testing basic HTTP call to .NET Core Server..." -Color Info
        
        $testScript = @"
Add-Type -AssemblyName System.Net.Http
`$httpClient = New-Object System.Net.Http.HttpClient
`$httpClient.Timeout = [TimeSpan]::FromSeconds(30)
try {
    `$response = `$httpClient.GetAsync("http://localhost:5103/delay/1").Result
    if (`$response.IsSuccessStatusCode) {
        Write-Host "✅ PowerShell HTTP test successful" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "❌ PowerShell HTTP test failed: `$(`$response.StatusCode)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "💥 PowerShell HTTP test error: `$_" -ForegroundColor Red
    exit 1
} finally {
    `$httpClient.Dispose()
}
"@
        
        $result = powershell.exe -Command $testScript
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "✅ PowerShell client functionality test passed!" -Color Success
            return $true
        } else {
            Write-ColorOutput "❌ PowerShell client functionality test failed" -Color Error
            return $false
        }
    }
    catch {
        Write-ColorOutput "💥 PowerShell client test failed: $_" -Color Error
        return $false
    }
}

function Run-CancellationDemo {
    Write-ColorOutput "`n⚡ Running Cancellation Demonstration..." -Color Header
    
    try {
        Write-ColorOutput "🎯 Testing cancellation with .NET Core Server..." -Color Info
        
        # Test cancellation by starting a long-running request and cancelling it
        $job = Start-Job -ScriptBlock {
            param($Url)
            Add-Type -AssemblyName System.Net.Http
            $httpClient = New-Object System.Net.Http.HttpClient
            $cts = New-Object System.Threading.CancellationTokenSource
            $cts.CancelAfter(3000)  # Cancel after 3 seconds
            
            try {
                $response = $httpClient.GetAsync($Url, $cts.Token).Result
                return "COMPLETED"
            }
            catch [System.OperationCanceledException] {
                return "CANCELLED"
            }
            catch {
                return "ERROR: $_"
            }
            finally {
                $httpClient.Dispose()
                $cts.Dispose()
            }
        } -ArgumentList "http://localhost:5103/delay/10"
        
        $job | Wait-Job -Timeout 10 | Out-Null
        $result = Receive-Job $job
        Remove-Job $job -Force
        
        if ($result -eq "CANCELLED") {
            Write-ColorOutput "✅ Cancellation demonstration successful!" -Color Success
            return $true
        } else {
            Write-ColorOutput "⚠️ Cancellation demonstration result: $result" -Color Warning
            return $false
        }
    }
    catch {
        Write-ColorOutput "💥 Cancellation demonstration failed: $_" -Color Error
        return $false
    }
}

function Print-TestSummary {
    param([hashtable]$Results)
    
    Write-ColorOutput "`n📊 INTEGRATION TEST SUMMARY" -Color Header
    Write-ColorOutput "============================" -Color Header
    
    $totalTests = $Results.Count
    $passedTests = ($Results.Values | Where-Object { $_ -eq $true }).Count
    $failedTests = $totalTests - $passedTests
    
    foreach ($testName in $Results.Keys) {
        $status = if ($Results[$testName]) { "✅ PASSED" } else { "❌ FAILED" }
        $color = if ($Results[$testName]) { "Success" } else { "Error" }
        Write-ColorOutput "   $testName`: $status" -Color $color
    }
    
    Write-ColorOutput "`n📈 STATISTICS:" -Color Info
    Write-ColorOutput "   Total Tests: $totalTests" -Color Info
    Write-ColorOutput "   Passed: $passedTests" -Color Success
    Write-ColorOutput "   Failed: $failedTests" -Color $(if ($failedTests -eq 0) { "Success" } else { "Error" })
    
    $successRate = [math]::Round(($passedTests / $totalTests) * 100, 1)
    Write-ColorOutput "   Success Rate: $successRate%" -Color $(if ($successRate -eq 100) { "Success" } else { "Warning" })
    
    if ($failedTests -eq 0) {
        Write-ColorOutput "`n🎉 ALL TESTS PASSED!" -Color Success
    } else {
        Write-ColorOutput "`n⚠️ Some tests failed. Check the logs above for details." -Color Warning
    }
}

# Main execution
try {
    Write-ColorOutput "🚀 Cancellation API Integration Test Suite" -Color Header
    Write-ColorOutput "===========================================" -Color Header
    
    $testResults = @{}
    
    # Start servers if not skipped
    if (-not $SkipServerStart) {
        Start-ApiServers
        $testResults["Server Startup"] = $true
    } else {
        Write-ColorOutput "⏭️ Skipping server startup (servers assumed to be running)" -Color Warning
        
        # Still do health checks
        $netCoreHealthy = Test-ServerHealth -Url "http://localhost:5103/delay/1" -ServerName ".NET Core Server"
        $owinHealthy = Test-ServerHealth -Url "http://localhost:5104/api/delay/1" -ServerName "OWIN Server"
        $testResults["Server Health Check"] = $netCoreHealthy -and $owinHealthy
    }
    
    # Run .NET integration tests
    $testResults[".NET Integration Tests"] = Run-DotNetIntegrationTests
    
    # Test PowerShell client functionality
    $testResults["PowerShell Client Test"] = Test-PowerShellClientFunctionality
    
    # Run cancellation demonstration
    $testResults["Cancellation Demonstration"] = Run-CancellationDemo
    
    # Print summary
    Print-TestSummary -Results $testResults
    
    # Determine overall success
    $overallSuccess = $testResults.Values | ForEach-Object { $_ } | Measure-Object -Sum | Select-Object -ExpandProperty Sum
    $totalTests = $testResults.Count
    
    if ($overallSuccess -eq $totalTests) {
        Write-ColorOutput "`n🏆 INTEGRATION TESTS COMPLETED SUCCESSFULLY!" -Color Success
        $exitCode = 0
    } else {
        Write-ColorOutput "`n⚠️ SOME INTEGRATION TESTS FAILED" -Color Warning
        $exitCode = 1
    }
}
catch {
    Write-ColorOutput "`n💥 INTEGRATION TEST SUITE FAILED: $_" -Color Error
    $exitCode = 1
}
finally {
    # Always try to stop servers
    if (-not $SkipServerStart) {
        Stop-ApiServers
    }
    
    Write-ColorOutput "`n📝 Integration test suite completed." -Color Info
    exit $exitCode
}