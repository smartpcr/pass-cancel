# Cancellation Token Demo with Multiple API Frameworks

This solution demonstrates how to implement proper cancellation token support across different .NET frameworks and client types, showcasing both .NET Core and OWIN Web API with various cancellation patterns.

## 🏗️ **Project Structure**

```
PassingCancel/
├── Server/                     # .NET Core 9.0 Web API
├── OwinWebApi/                 # OWIN Web API (.NET Framework 4.6.2)
├── Client/                     # .NET Console Client
├── PowerShellClient/           # PowerShell Clients
├── IntegrationTests/           # Automated Integration Tests
├── Run-IntegrationTests.ps1    # Test Automation Script
└── Directory.Packages.props    # Central Package Management
```

## 🎯 **Key Features**

### **Server Implementations**
- **.NET Core Server** (port 5103): Modern ASP.NET Core with built-in cancellation
- **OWIN Server** (port 5104): .NET Framework 4.6.2 with multiple cancellation patterns

### **Client Implementations**
- **.NET Console Client**: Cross-platform with HttpClient cancellation
- **PowerShell Clients**: Both PowerShell Core and Windows PowerShell support

### **Cancellation Patterns Demonstrated**
1. **Explicit CancellationToken Parameters**: Traditional approach
2. **Action Filter Injection**: Automatic token injection via filters
3. **OWIN Middleware**: Global cancellation handling
4. **Extension Methods**: Easy token access without explicit parameters

## 🚀 **Quick Start**

### **Option 1: Automated Testing**
```powershell
# Run comprehensive integration tests
.\Run-IntegrationTests.ps1
```

### **Option 2: Manual Testing**

1. **Start the servers:**
   ```bash
   # Terminal 1: .NET Core Server
   dotnet run --project Server
   
   # Terminal 2: OWIN Server
   dotnet run --project OwinWebApi
   ```

2. **Test with .NET Console Client:**
   ```bash
   # Terminal 3: .NET Client
   dotnet run --project Client
   ```

3. **Test with PowerShell Client:**
   ```powershell
   # Terminal 4: PowerShell Client
   cd PowerShellClient
   
   # For PowerShell Core (cross-platform)
   pwsh ./Simple-Client.ps1
   
   # For Windows PowerShell (.NET Framework)
   powershell ./Windows-Client.ps1
   ```

## 🔧 **OWIN Cancellation Patterns**

### **1. Traditional Approach**
```csharp
public async Task<IHttpActionResult> WithToken(int seconds, CancellationToken cancellationToken)
{
    await Task.Delay(seconds * 1000, cancellationToken);
    return Ok("Completed");
}
```

### **2. Extension Method Approach**
```csharp
public async Task<IHttpActionResult> WithoutToken(int seconds)
{
    var cancellationToken = Request.GetCancellationToken(); // Extension method
    await Task.Delay(seconds * 1000, cancellationToken);
    return Ok("Completed");
}
```

### **3. OWIN Context Approach**
```csharp
public async Task<IHttpActionResult> UsingContext(int seconds)
{
    var cancellationToken = owinContext.GetCancellationToken();
    await Task.Delay(seconds * 1000, cancellationToken);
    return Ok("Completed");
}
```

### **4. Middleware-Only Approach**
```csharp
public async Task<IHttpActionResult> NoExplicitHandling(int seconds)
{
    // No explicit cancellation handling
    // Middleware automatically handles client disconnection
    await Task.Delay(seconds * 1000);
    return Ok("Completed");
}
```

## 🧪 **Available API Endpoints**

### **.NET Core Server (port 5103)**
- `GET /delay/{seconds}` - Simple delay with cancellation support

### **OWIN Server (port 5104)**
- `GET /api/delay/{seconds}` - With explicit CancellationToken
- `GET /api/delay-alt/{seconds}` - Without CancellationToken (extension method)
- `GET /api/example/with-token/{seconds}` - Explicit token example
- `GET /api/example/without-token/{seconds}` - Extension method example  
- `GET /api/example/owin-context/{seconds}` - OWIN context example
- `GET /api/example/no-cancellation/{seconds}` - Middleware-only example

## 🛠️ **Development Setup**

### **Prerequisites**
- **Windows Platform** (required for integration tests)
- .NET 9.0 SDK
- .NET Framework 4.6.2 (Windows)
- Windows PowerShell 5.1+ or PowerShell Core 7+

### **Build Instructions**
```bash
# Build entire solution
dotnet build

# Build specific projects
dotnet build Server/Server.csproj
dotnet build OwinWebApi/OwinWebApi.csproj
dotnet build IntegrationTests/IntegrationTests.csproj
```

### **Package Management**
This solution uses Central Package Management with `Directory.Packages.props`:
- All package versions are centrally managed
- Consistent versioning across projects
- Easy dependency updates

## 🧪 **Testing**

### **Integration Test Suite**
The `IntegrationTests` project provides comprehensive Windows-targeted testing:

```bash
# Run integration tests manually (Windows only)
dotnet run --project IntegrationTests
```

**Test Coverage:**
- ✅ All API endpoints (success scenarios)
- ✅ Cancellation scenarios (timeout testing)
- ✅ PowerShell client validation
- ✅ PowerShell script invocation with process control
- ✅ Ctrl+C simulation via process termination
- ✅ Advanced cancellation scenarios with multiple endpoints
- ✅ Server health checks
- ✅ Windows-specific Console API integration

### **Automated Test Runner**
The `Run-IntegrationTests.ps1` script provides full automation:

```powershell
# Full automated test suite
.\Run-IntegrationTests.ps1

# Skip server startup (if already running)
.\Run-IntegrationTests.ps1 -SkipServerStart

# Custom timeout
.\Run-IntegrationTests.ps1 -TestTimeout 60
```

## 🔍 **How Cancellation Works**

### **Client Side**
1. **HttpClient**: Pass `CancellationToken` to `GetAsync()`
2. **PowerShell**: Use `CancellationTokenSource` with timeout or manual cancellation
3. **Console Apps**: Register Ctrl+C handlers to trigger cancellation

### **Server Side**
1. **Request Receives Token**: HTTP layer provides cancellation token
2. **Middleware Processing**: Custom middleware monitors client disconnection
3. **Action Filter**: Injects tokens into request properties
4. **Controller Access**: Multiple ways to access the cancellation token
5. **Task Cancellation**: All async operations respect the token

### **Network Level**
- **Client Disconnection**: When client cancels, TCP connection closes
- **Server Detection**: Server detects broken connection and cancels operations
- **HTTP 499**: Server returns "Client Closed Request" status code

## 📋 **Solution Benefits**

- **✅ Backwards Compatibility**: Works with existing OWIN applications
- **✅ Multiple Patterns**: Choose the approach that fits your architecture
- **✅ Global Coverage**: Middleware handles cancellation even without explicit support
- **✅ Easy Migration**: Gradually add cancellation support to existing methods
- **✅ Cross-Platform**: PowerShell Core and Windows PowerShell support
- **✅ Comprehensive Testing**: Automated integration test suite

## 🔧 **Configuration**

### **Ports**
- .NET Core Server: `5103`
- OWIN Server: `5104`

### **Timeouts**
- Default HTTP timeout: 5 minutes
- Integration test timeout: 30 seconds (configurable)
- Cancellation demonstration: 3 seconds

## 🚨 **Troubleshooting**

### **Common Issues**

1. **Port Conflicts**
   ```bash
   # Check if ports are in use
   netstat -an | findstr ":5103"
   netstat -an | findstr ":5104"
   ```

2. **Server Not Starting**
   ```bash
   # Check .NET versions
   dotnet --list-sdks
   dotnet --list-runtimes
   ```

3. **PowerShell Issues**
   ```powershell
   # Check PowerShell version
   $PSVersionTable.PSVersion
   
   # Check execution policy
   Get-ExecutionPolicy
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
   ```

### **Integration Test Failures**
- Ensure both servers are running before tests
- Check firewall settings for localhost connections
- Verify .NET Framework 4.6.2 is installed (Windows)

## 📚 **Learning Objectives**

This solution demonstrates:
- **Cancellation Token Patterns**: Multiple approaches to handle cancellation
- **Cross-Framework Compatibility**: .NET Core + .NET Framework integration
- **Client-Server Communication**: Proper cancellation propagation
- **Middleware Architecture**: Global request processing patterns
- **Testing Strategies**: Comprehensive integration testing approaches

## 🤝 **Contributing**

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all integration tests pass
5. Submit a pull request

---

**🎯 This solution provides a complete reference for implementing robust cancellation token support across different .NET frameworks and client types.**