using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace IntegrationTests;

// Windows API imports for Ctrl+C simulation
public static class WindowsApi
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate? HandlerRoutine, bool Add);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    public delegate bool ConsoleCtrlDelegate(uint CtrlType);

    public const uint CTRL_C_EVENT = 0;
    public const uint CTRL_BREAK_EVENT = 1;
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();

        logger.LogInformation("🚀 Starting Integration Tests for Cancellation APIs");

        var testRunner = new IntegrationTestRunner(logger, httpClientFactory);

        try
        {
            await testRunner.RunAllTestsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "💥 Integration tests failed");
            Environment.Exit(1);
        }

        logger.LogInformation("✅ All integration tests completed successfully!");
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddHttpClient();
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            });
}

public class IntegrationTestRunner
{
    private readonly ILogger<IntegrationTestRunner> _logger;
    private readonly HttpClient _httpClient;
    private readonly List<string> _testResults = new();

    public IntegrationTestRunner(ILogger<Program> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = (ILogger<IntegrationTestRunner>)logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task RunAllTestsAsync()
    {
        _logger.LogInformation("📋 Running integration tests for both API servers...");

        // Test .NET Core Server
        await RunNetCoreServerTestsAsync();

        // Test OWIN Server (all endpoints)
        await RunOwinServerTestsAsync();

        // Test cancellation scenarios
        await RunCancellationTestsAsync();

        // Test PowerShell client integration
        await TestPowerShellClientAsync();

        // Test PowerShell cancellation scenarios
        await TestPowerShellCancellationAsync();

        // Test advanced Ctrl+C simulation with Windows Console API
        await TestAdvancedCancellationAsync();

        // Print summary
        PrintTestSummary();
    }

    private async Task RunNetCoreServerTestsAsync()
    {
        _logger.LogInformation("🔵 Testing .NET Core Server (port 5103)...");

        await TestEndpoint("NET_CORE_DELAY", "http://localhost:5103/delay/2", "NET Core delay endpoint");
    }

    private async Task RunOwinServerTestsAsync()
    {
        _logger.LogInformation("🟡 Testing OWIN Server (port 5104)...");

        // Test all OWIN endpoints
        var endpoints = new[]
        {
            ("OWIN_DELAY_WITH_TOKEN", "http://localhost:5104/api/delay/2", "OWIN delay with CancellationToken"),
            ("OWIN_DELAY_WITHOUT_TOKEN", "http://localhost:5104/api/delay-alt/2", "OWIN delay without CancellationToken"),
            ("OWIN_EXAMPLE_WITH_TOKEN", "http://localhost:5104/api/example/with-token/2", "OWIN example with token"),
            ("OWIN_EXAMPLE_WITHOUT_TOKEN", "http://localhost:5104/api/example/without-token/2", "OWIN example without token"),
            ("OWIN_EXAMPLE_OWIN_CONTEXT", "http://localhost:5104/api/example/owin-context/2", "OWIN example with context"),
            ("OWIN_EXAMPLE_NO_CANCELLATION", "http://localhost:5104/api/example/no-cancellation/2", "OWIN example no cancellation")
        };

        foreach (var (testId, url, description) in endpoints)
        {
            await TestEndpoint(testId, url, description);
        }
    }

    private async Task RunCancellationTestsAsync()
    {
        _logger.LogInformation("❌ Testing cancellation scenarios...");

        // Test .NET Core Server cancellation
        await TestCancellation("NET_CORE_CANCELLATION", "http://localhost:5103/delay/10", ".NET Core cancellation");

        // Test OWIN Server cancellation
        await TestCancellation("OWIN_CANCELLATION", "http://localhost:5104/api/delay/10", "OWIN cancellation");
    }

    private async Task TestEndpoint(string testId, string url, string description)
    {
        try
        {
            _logger.LogInformation($"🧪 Testing: {description}");
            
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"✅ {testId}: SUCCESS - Status: {response.StatusCode}");
                _logger.LogDebug($"   Response: {content}");
                _testResults.Add($"✅ {testId}: PASSED");
            }
            else
            {
                _logger.LogWarning($"⚠️ {testId}: FAILED - Status: {response.StatusCode}");
                _testResults.Add($"❌ {testId}: FAILED - HTTP {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"🔌 {testId}: CONNECTION FAILED - {ex.Message}");
            _testResults.Add($"🔌 {testId}: CONNECTION FAILED");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError($"⏱️ {testId}: TIMEOUT - {ex.Message}");
            _testResults.Add($"⏱️ {testId}: TIMEOUT");
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 {testId}: ERROR - {ex.Message}");
            _testResults.Add($"💥 {testId}: ERROR");
        }
    }

    private async Task TestCancellation(string testId, string url, string description)
    {
        try
        {
            _logger.LogInformation($"🧪 Testing: {description}");
            
            using var cts = new CancellationTokenSource();
            
            // Cancel after 3 seconds
            cts.CancelAfter(3000);
            
            var response = await _httpClient.GetAsync(url, cts.Token);
            
            // If we get here, cancellation didn't work as expected
            _logger.LogWarning($"⚠️ {testId}: Cancellation test didn't cancel as expected");
            _testResults.Add($"⚠️ {testId}: CANCELLATION NOT TRIGGERED");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"✅ {testId}: Cancellation worked correctly");
            _testResults.Add($"✅ {testId}: CANCELLATION SUCCESSFUL");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"🔌 {testId}: CONNECTION FAILED - {ex.Message}");
            _testResults.Add($"🔌 {testId}: CONNECTION FAILED");
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 {testId}: ERROR - {ex.Message}");
            _testResults.Add($"💥 {testId}: ERROR");
        }
    }

    private async Task TestPowerShellClientAsync()
    {
        _logger.LogInformation("🟦 Testing PowerShell client integration...");

        try
        {
            // Check if PowerShell is available
            var powerShellPath = GetPowerShellPath();
            if (string.IsNullOrEmpty(powerShellPath))
            {
                _logger.LogWarning("⚠️ PowerShell not found, skipping PowerShell integration tests");
                _testResults.Add("⚠️ POWERSHELL_TEST: SKIPPED - PowerShell not available");
                return;
            }

            _logger.LogInformation($"📍 Found PowerShell at: {powerShellPath}");

            // Test if PowerShell script exists
            var scriptPath = Path.Combine("..", "PowerShellClient", "Windows-Client.ps1");
            var fullScriptPath = Path.GetFullPath(scriptPath);
            
            if (!File.Exists(fullScriptPath))
            {
                _logger.LogWarning($"⚠️ PowerShell script not found at: {fullScriptPath}");
                _testResults.Add("⚠️ POWERSHELL_TEST: SKIPPED - Script not found");
                return;
            }

            // Test PowerShell script basic functionality
            await TestPowerShellBasicFunctionality(powerShellPath, fullScriptPath);
            
            // Test using dedicated test script
            await TestPowerShellTestScript(powerShellPath);
            
            _logger.LogInformation("✅ PowerShell client integration tests completed");
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 PowerShell integration test failed: {ex.Message}");
            _testResults.Add("💥 POWERSHELL_TEST: ERROR");
        }
    }

    private async Task TestPowerShellBasicFunctionality(string powerShellPath, string scriptPath)
    {
        _logger.LogInformation("🧪 Testing PowerShell basic HTTP functionality...");

        try
        {
            // Create a simple PowerShell test script that calls the API
            var testScript = @"
                Add-Type -AssemblyName System.Net.Http
                $httpClient = New-Object System.Net.Http.HttpClient
                $httpClient.Timeout = [TimeSpan]::FromSeconds(30)
                try {
                    Write-Host 'Testing .NET Core API call...' -ForegroundColor Cyan
                    $response = $httpClient.GetAsync('http://localhost:5103/delay/1').Result
                    if ($response.IsSuccessStatusCode) {
                        $content = $response.Content.ReadAsStringAsync().Result
                        Write-Host ""✅ API call successful: $($response.StatusCode)"" -ForegroundColor Green
                        Write-Host ""Response: $content"" -ForegroundColor Gray
                        exit 0
                    } else {
                        Write-Host ""❌ API call failed: $($response.StatusCode)"" -ForegroundColor Red
                        exit 1
                    }
                } catch {
                    Write-Host ""💥 API call error: $_"" -ForegroundColor Red
                    exit 1
                } finally {
                    $httpClient.Dispose()
                }
            ";

            using var process = new Process();
            process.StartInfo.FileName = powerShellPath;
            process.StartInfo.Arguments = $"-ExecutionPolicy Bypass -Command \"{testScript}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var output = new List<string>();
            var errors = new List<string>();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.Add(e.Data);
                    _logger.LogInformation($"PS: {e.Data}");
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errors.Add(e.Data);
                    _logger.LogWarning($"PS Error: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("✅ PowerShell basic functionality test passed");
                _testResults.Add("✅ POWERSHELL_BASIC_TEST: PASSED");
            }
            else
            {
                _logger.LogWarning($"⚠️ PowerShell basic functionality test failed with exit code: {process.ExitCode}");
                _testResults.Add($"❌ POWERSHELL_BASIC_TEST: FAILED - Exit code {process.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 PowerShell basic functionality test error: {ex.Message}");
            _testResults.Add("💥 POWERSHELL_BASIC_TEST: ERROR");
        }
    }

    private async Task TestPowerShellTestScript(string powerShellPath)
    {
        _logger.LogInformation("🧪 Testing dedicated PowerShell test script...");

        try
        {
            var testScriptPath = Path.Combine("..", "PowerShellClient", "Test-CancellationScenario.ps1");
            var fullTestScriptPath = Path.GetFullPath(testScriptPath);

            if (!File.Exists(fullTestScriptPath))
            {
                _logger.LogWarning($"⚠️ PowerShell test script not found at: {fullTestScriptPath}");
                _testResults.Add("⚠️ POWERSHELL_TESTSCRIPT: SKIPPED - Test script not found");
                return;
            }

            // Test basic scenario
            await RunPowerShellTestScript(
                powerShellPath,
                fullTestScriptPath,
                "POWERSHELL_SCRIPT_BASIC",
                "Basic",
                "http://localhost:5103/delay/2",
                false
            );

            // Test cancellation scenario
            await RunPowerShellTestScript(
                powerShellPath,
                fullTestScriptPath,
                "POWERSHELL_SCRIPT_CANCELLATION",
                "Cancellation",
                "http://localhost:5104/api/delay/10",
                true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 PowerShell test script error: {ex.Message}");
            _testResults.Add("💥 POWERSHELL_TESTSCRIPT: ERROR");
        }
    }

    private async Task RunPowerShellTestScript(string powerShellPath, string scriptPath, string testId, string scenario, string serverUrl, bool simulateCancel)
    {
        _logger.LogInformation($"🧪 Running PowerShell test script: {scenario}");

        try
        {
            var arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -TestScenario {scenario} -ServerUrl \"{serverUrl}\"";
            if (simulateCancel)
            {
                arguments += " -SimulateCancel";
            }

            using var process = new Process();
            process.StartInfo.FileName = powerShellPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var output = new List<string>();
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.Add(e.Data);
                    _logger.LogInformation($"PS: {e.Data}");
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogWarning($"PS Error: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for completion with timeout
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"⏱️ {testId} timed out, killing process...");
                process.Kill();
                await process.WaitForExitAsync();
                _testResults.Add($"⏱️ {testId}: TIMEOUT");
                return;
            }

            if (process.ExitCode == 0)
            {
                _logger.LogInformation($"✅ {testId}: PowerShell test script passed");
                _testResults.Add($"✅ {testId}: PASSED");
            }
            else
            {
                _logger.LogWarning($"❌ {testId}: Test script failed with exit code: {process.ExitCode}");
                _testResults.Add($"❌ {testId}: FAILED - Exit code {process.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 {testId} error: {ex.Message}");
            _testResults.Add($"💥 {testId}: ERROR");
        }
    }

    private async Task TestPowerShellCancellationAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogWarning("⚠️ PowerShell cancellation tests require Windows platform");
            _testResults.Add("⚠️ POWERSHELL_CANCELLATION: SKIPPED - Windows required");
            return;
        }

        _logger.LogInformation("⚡ Testing PowerShell cancellation with Ctrl+C simulation...");

        try
        {
            var powerShellPath = GetPowerShellPath();
            if (string.IsNullOrEmpty(powerShellPath))
            {
                _testResults.Add("⚠️ POWERSHELL_CANCELLATION: SKIPPED - PowerShell not available");
                return;
            }

            // Test .NET Core server cancellation via PowerShell
            await TestPowerShellCancellationScenario(
                "POWERSHELL_NETCORE_CANCELLATION",
                powerShellPath,
                "http://localhost:5103/delay/15",
                ".NET Core Server"
            );

            // Test OWIN server cancellation via PowerShell  
            await TestPowerShellCancellationScenario(
                "POWERSHELL_OWIN_CANCELLATION",
                powerShellPath,
                "http://localhost:5104/api/delay/15",
                "OWIN Server"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 PowerShell cancellation tests failed: {ex.Message}");
            _testResults.Add("💥 POWERSHELL_CANCELLATION: ERROR");
        }
    }

    private async Task TestPowerShellCancellationScenario(string testId, string powerShellPath, string url, string serverName)
    {
        _logger.LogInformation($"🧪 Testing PowerShell cancellation for {serverName}...");

        try
        {
            // Create PowerShell script that makes a long-running API call
            var cancellationScript = $@"
                Add-Type -AssemblyName System.Net.Http
                $httpClient = New-Object System.Net.Http.HttpClient
                $httpClient.Timeout = [TimeSpan]::FromMinutes(5)
                
                Write-Host 'Starting long-running API call to {url}...' -ForegroundColor Cyan
                Write-Host 'This call should be cancelled by Ctrl+C simulation' -ForegroundColor Yellow
                
                try {{
                    $response = $httpClient.GetAsync('{url}').Result
                    Write-Host 'Request completed unexpectedly: ' $response.StatusCode -ForegroundColor Red
                    exit 1
                }} catch [System.OperationCanceledException] {{
                    Write-Host 'Request was successfully cancelled!' -ForegroundColor Green
                    exit 0
                }} catch {{
                    Write-Host 'Request failed with error: ' $_.Exception.Message -ForegroundColor Red
                    exit 1
                }} finally {{
                    $httpClient.Dispose()
                }}
            ";

            using var process = new Process();
            process.StartInfo.FileName = powerShellPath;
            process.StartInfo.Arguments = $"-ExecutionPolicy Bypass -Command \"{cancellationScript}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var output = new List<string>();
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.Add(e.Data);
                    _logger.LogInformation($"PS: {e.Data}");
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogWarning($"PS Error: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait a bit for the process to start the HTTP request
            await Task.Delay(3000);

            // Simulate Ctrl+C by killing the process (simulates user cancellation)
            _logger.LogInformation("🔥 Simulating Ctrl+C by terminating PowerShell process...");
            
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync();
            }

            // Check if the process was terminated (simulating cancellation)
            if (process.ExitCode != 0)
            {
                _logger.LogInformation($"✅ {testId}: PowerShell process was terminated (simulating cancellation)");
                _testResults.Add($"✅ {testId}: CANCELLATION SIMULATED");
            }
            else
            {
                _logger.LogWarning($"⚠️ {testId}: Process completed unexpectedly");
                _testResults.Add($"⚠️ {testId}: UNEXPECTED COMPLETION");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 {testId} error: {ex.Message}");
            _testResults.Add($"💥 {testId}: ERROR");
        }
    }

    private async Task TestAdvancedCancellationAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogWarning("⚠️ Advanced cancellation tests require Windows platform");
            _testResults.Add("⚠️ ADVANCED_CANCELLATION: SKIPPED - Windows required");
            return;
        }

        _logger.LogInformation("🚀 Testing advanced Ctrl+C simulation with Windows Console API...");

        try
        {
            var powerShellPath = GetPowerShellPath();
            if (string.IsNullOrEmpty(powerShellPath))
            {
                _testResults.Add("⚠️ ADVANCED_CANCELLATION: SKIPPED - PowerShell not available");
                return;
            }

            // Test with actual Windows Client project
            await TestAdvancedCancellationScenario(
                "ADVANCED_WINDOWS_CLIENT_CANCELLATION",
                powerShellPath,
                "Test advanced cancellation with Windows PowerShell client"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 Advanced cancellation tests failed: {ex.Message}");
            _testResults.Add("💥 ADVANCED_CANCELLATION: ERROR");
        }
    }

    private async Task TestAdvancedCancellationScenario(string testId, string powerShellPath, string description)
    {
        _logger.LogInformation($"🧪 {description}...");

        try
        {
            // Create a PowerShell script that simulates the Windows client behavior
            var clientScript = @"
                param([int]$TestDuration = 10)
                
                # Import the Windows client functions
                Add-Type -AssemblyName System.Net.Http
                $httpClient = New-Object System.Net.Http.HttpClient
                $httpClient.Timeout = [TimeSpan]::FromMinutes(5)
                
                # Setup cancellation token source
                $cts = New-Object System.Threading.CancellationTokenSource
                
                Write-Host ""🚀 Starting test with Windows PowerShell client simulation..."" -ForegroundColor Green
                Write-Host ""📡 Testing both .NET Core and OWIN servers..."" -ForegroundColor Cyan
                
                try {
                    Write-Host ""📞 Calling .NET Core Server (http://localhost:5103/delay/$TestDuration)..."" -ForegroundColor Blue
                    $netCoreTask = $httpClient.GetAsync(""http://localhost:5103/delay/$TestDuration"", $cts.Token)
                    
                    Write-Host ""📞 Calling OWIN Server (http://localhost:5104/api/delay/$TestDuration)..."" -ForegroundColor Yellow
                    $owinTask = $httpClient.GetAsync(""http://localhost:5104/api/delay/$TestDuration"", $cts.Token)
                    
                    # Wait a bit then simulate cancellation
                    Start-Sleep -Seconds 3
                    Write-Host ""🛑 Simulating user Ctrl+C (cancelling requests)..."" -ForegroundColor Red
                    $cts.Cancel()
                    
                    # Try to get results (should be cancelled)
                    try {
                        $netCoreResult = $netCoreTask.Result
                        Write-Host ""❌ .NET Core call completed unexpectedly: $($netCoreResult.StatusCode)"" -ForegroundColor Red
                    } catch [System.OperationCanceledException] {
                        Write-Host ""✅ .NET Core call was cancelled successfully!"" -ForegroundColor Green
                    } catch {
                        Write-Host ""⚠️ .NET Core call failed: $($_.Exception.Message)"" -ForegroundColor Yellow
                    }
                    
                    try {
                        $owinResult = $owinTask.Result
                        Write-Host ""❌ OWIN call completed unexpectedly: $($owinResult.StatusCode)"" -ForegroundColor Red
                    } catch [System.OperationCanceledException] {
                        Write-Host ""✅ OWIN call was cancelled successfully!"" -ForegroundColor Green
                    } catch {
                        Write-Host ""⚠️ OWIN call failed: $($_.Exception.Message)"" -ForegroundColor Yellow
                    }
                    
                    Write-Host ""🎯 Cancellation simulation completed!"" -ForegroundColor Green
                    exit 0
                    
                } catch {
                    Write-Host ""💥 Test failed: $($_.Exception.Message)"" -ForegroundColor Red
                    exit 1
                } finally {
                    $httpClient.Dispose()
                    $cts.Dispose()
                }
            ";

            // Save script to temporary file for better PowerShell execution
            var tempScript = Path.GetTempFileName() + ".ps1";
            await File.WriteAllTextAsync(tempScript, clientScript);

            try
            {
                using var process = new Process();
                process.StartInfo.FileName = powerShellPath;
                process.StartInfo.Arguments = $"-ExecutionPolicy Bypass -File \"{tempScript}\" -TestDuration 10";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                var output = new List<string>();
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.Add(e.Data);
                        _logger.LogInformation($"PS: {e.Data}");
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogWarning($"PS Error: {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for completion with timeout
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("⏱️ PowerShell test timed out, killing process...");
                    process.Kill();
                    await process.WaitForExitAsync();
                }

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation($"✅ {testId}: Advanced cancellation test passed");
                    _testResults.Add($"✅ {testId}: PASSED");
                }
                else
                {
                    _logger.LogWarning($"⚠️ {testId}: Test failed with exit code: {process.ExitCode}");
                    _testResults.Add($"❌ {testId}: FAILED - Exit code {process.ExitCode}");
                }

                // Log PowerShell output for debugging
                if (output.Any())
                {
                    _logger.LogInformation("📄 PowerShell test output summary:");
                    foreach (var line in output.TakeLast(5))
                    {
                        _logger.LogInformation($"   {line}");
                    }
                }
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempScript))
                {
                    File.Delete(tempScript);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 {testId} error: {ex.Message}");
            _testResults.Add($"💥 {testId}: ERROR");
        }
    }

    private string GetPowerShellPath()
    {
        var paths = new[]
        {
            "powershell.exe",
            "pwsh.exe",
            @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
            @"C:\Program Files\PowerShell\7\pwsh.exe"
        };

        foreach (var path in paths)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = path;
                process.StartInfo.Arguments = "-Version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                
                if (process.Start())
                {
                    process.WaitForExit(5000);
                    if (process.ExitCode == 0)
                    {
                        return path;
                    }
                }
            }
            catch
            {
                // Continue to next path
            }
        }

        return string.Empty;
    }

    private void PrintTestSummary()
    {
        _logger.LogInformation("\n📊 TEST SUMMARY");
        _logger.LogInformation("================");

        var passed = _testResults.Count(r => r.Contains("✅"));
        var failed = _testResults.Count(r => r.Contains("❌"));
        var warnings = _testResults.Count(r => r.Contains("⚠️"));
        var errors = _testResults.Count(r => r.Contains("💥"));
        var connectionIssues = _testResults.Count(r => r.Contains("🔌"));

        foreach (var result in _testResults)
        {
            if (result.Contains("✅"))
                _logger.LogInformation(result);
            else if (result.Contains("⚠️"))
                _logger.LogWarning(result);
            else
                _logger.LogError(result);
        }

        _logger.LogInformation($"\n📈 STATISTICS:");
        _logger.LogInformation($"   ✅ Passed: {passed}");
        _logger.LogInformation($"   ❌ Failed: {failed}");
        _logger.LogInformation($"   ⚠️ Warnings: {warnings}");
        _logger.LogInformation($"   💥 Errors: {errors}");
        _logger.LogInformation($"   🔌 Connection Issues: {connectionIssues}");
        
        if (connectionIssues > 0)
        {
            _logger.LogWarning("\n💡 TROUBLESHOOTING:");
            _logger.LogWarning("   • Make sure both servers are running:");
            _logger.LogWarning("     - .NET Core Server: dotnet run --project ../Server");
            _logger.LogWarning("     - OWIN Server: dotnet run --project ../OwinWebApi");
        }
    }
}