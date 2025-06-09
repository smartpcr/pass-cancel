# PowerShell module for calling APIs with cancellation support

# Global variables
$script:httpClient = $null
$script:cancellationTokenSource = $null

function Initialize-HttpClient {
    if ($null -eq $script:httpClient) {
        Add-Type -AssemblyName System.Net.Http
        $script:httpClient = New-Object System.Net.Http.HttpClient
        $script:httpClient.Timeout = [TimeSpan]::FromMinutes(5)
    }
}

function New-CancellationTokenSource {
    Add-Type -AssemblyName System.Threading
    $script:cancellationTokenSource = New-Object System.Threading.CancellationTokenSource
    return $script:cancellationTokenSource
}

function Invoke-ApiWithCancellation {
    param(
        [string]$Url,
        [System.Threading.CancellationToken]$CancellationToken
    )
    
    try {
        Write-Host "Calling: $Url" -ForegroundColor Cyan
        $task = $script:httpClient.GetAsync($Url, $CancellationToken)
        $response = $task.GetAwaiter().GetResult()
        
        if ($response.IsSuccessStatusCode) {
            $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            Write-Host "Response: $content" -ForegroundColor Green
        }
        else {
            Write-Host "Response status: $($response.StatusCode) ($([int]$response.StatusCode))" -ForegroundColor Yellow
        }
    }
    catch [System.OperationCanceledException] {
        Write-Host "Request was cancelled." -ForegroundColor Red
        throw
    }
    catch {
        Write-Host "Error occurred: $_" -ForegroundColor Red
        throw
    }
}

function Invoke-NetCoreServer {
    param(
        [System.Threading.CancellationToken]$CancellationToken
    )
    
    Write-Host "Calling .NET Core Server..." -ForegroundColor Blue
    Invoke-ApiWithCancellation -Url "http://localhost:5103/delay/10" -CancellationToken $CancellationToken
}

function Invoke-OwinServer {
    param(
        [System.Threading.CancellationToken]$CancellationToken
    )
    
    Write-Host "Calling OWIN Server..." -ForegroundColor Blue
    Invoke-ApiWithCancellation -Url "http://localhost:5104/api/delay/10" -CancellationToken $CancellationToken
}

function Invoke-BothServers {
    param(
        [System.Threading.CancellationToken]$CancellationToken
    )
    
    Write-Host "Calling both servers in parallel..." -ForegroundColor Blue
    
    $jobs = @()
    
    # Start jobs for parallel execution
    $jobs += Start-Job -ScriptBlock {
        param($Token)
        Import-Module $using:PSScriptRoot\PowerShellClient.psd1
        Initialize-HttpClient
        try {
            Invoke-NetCoreServer -CancellationToken $Token
        }
        catch {
            Write-Host ".NET Core Server error: $_" -ForegroundColor Red
        }
    } -ArgumentList $CancellationToken
    
    $jobs += Start-Job -ScriptBlock {
        param($Token)
        Import-Module $using:PSScriptRoot\PowerShellClient.psd1
        Initialize-HttpClient
        try {
            Invoke-OwinServer -CancellationToken $Token
        }
        catch {
            Write-Host "OWIN Server error: $_" -ForegroundColor Red
        }
    } -ArgumentList $CancellationToken
    
    # Wait for jobs with cancellation support
    while ($jobs | Where-Object { $_.State -eq 'Running' }) {
        if ($CancellationToken.IsCancellationRequested) {
            $jobs | Stop-Job
            break
        }
        Start-Sleep -Milliseconds 100
    }
    
    # Get job results
    $jobs | Receive-Job
    $jobs | Remove-Job
}

function Start-ApiClient {
    [CmdletBinding()]
    param()
    
    Initialize-HttpClient
    
    # Set up Ctrl+C handler
    $null = [Console]::TreatControlCAsInput = $false
    
    Write-Host "`nPowerShell API Client with Cancellation Support" -ForegroundColor Green
    Write-Host "================================================" -ForegroundColor Green
    
    while ($true) {
        Write-Host "`nSelect an option:" -ForegroundColor Yellow
        Write-Host "1. Call .NET Core Server (http://localhost:5103/delay/10)"
        Write-Host "2. Call OWIN Server (http://localhost:5104/api/delay/10)"
        Write-Host "3. Call both servers in parallel"
        Write-Host "4. Exit"
        Write-Host -NoNewline "Enter choice (1-4): "
        
        $choice = Read-Host
        
        if ($choice -eq '4') {
            break
        }
        
        # Create new cancellation token source for each request
        $cts = New-CancellationTokenSource
        
        # Register Ctrl+C handler
        $null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action {
            if ($null -ne $script:cancellationTokenSource) {
                Write-Host "`nCtrl+C pressed. Cancelling ongoing requests..." -ForegroundColor Red
                $script:cancellationTokenSource.Cancel()
            }
        }
        
        Write-Host "`nPress Ctrl+C during the request to test cancellation..." -ForegroundColor Cyan
        
        try {
            # Create a runspace to handle the API call
            $runspace = [RunspaceFactory]::CreateRunspace()
            $runspace.Open()
            $runspace.SessionStateProxy.SetVariable('cts', $cts)
            $runspace.SessionStateProxy.SetVariable('choice', $choice)
            $runspace.SessionStateProxy.SetVariable('PSScriptRoot', $PSScriptRoot)
            
            $powershell = [PowerShell]::Create()
            $powershell.Runspace = $runspace
            
            $scriptBlock = {
                param($Choice, $CancellationToken, $ScriptRoot)
                
                Import-Module "$ScriptRoot\PowerShellClient.psd1"
                Initialize-HttpClient
                
                switch ($Choice) {
                    '1' { Invoke-NetCoreServer -CancellationToken $CancellationToken }
                    '2' { Invoke-OwinServer -CancellationToken $CancellationToken }
                    '3' { Invoke-BothServers -CancellationToken $CancellationToken }
                    default { Write-Host "Invalid choice. Please try again." -ForegroundColor Red }
                }
            }
            
            $null = $powershell.AddScript($scriptBlock).AddArgument($choice).AddArgument($cts.Token).AddArgument($PSScriptRoot)
            
            # Start async execution
            $handle = $powershell.BeginInvoke()
            
            # Wait for completion or cancellation
            while (-not $handle.IsCompleted) {
                if ([Console]::KeyAvailable) {
                    $key = [Console]::ReadKey($true)
                    if ($key.Key -eq [ConsoleKey]::C -and $key.Modifiers -eq [ConsoleModifiers]::Control) {
                        Write-Host "`nCtrl+C pressed. Cancelling ongoing requests..." -ForegroundColor Red
                        $cts.Cancel()
                    }
                }
                Start-Sleep -Milliseconds 100
            }
            
            # Get results
            try {
                $powershell.EndInvoke($handle)
            }
            catch [System.OperationCanceledException] {
                Write-Host "`nOperation was cancelled." -ForegroundColor Red
            }
            
            $powershell.Dispose()
            $runspace.Close()
        }
        catch {
            Write-Host "`nAn error occurred: $_" -ForegroundColor Red
        }
        finally {
            # Cleanup
            if ($null -ne $cts) {
                $cts.Dispose()
            }
            Unregister-Event -SourceIdentifier PowerShell.Exiting -ErrorAction SilentlyContinue
        }
    }
    
    Write-Host "`nExiting..." -ForegroundColor Yellow
    
    # Cleanup
    if ($null -ne $script:httpClient) {
        $script:httpClient.Dispose()
        $script:httpClient = $null
    }
}

# Export functions
Export-ModuleMember -Function Start-ApiClient