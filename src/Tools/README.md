# Nocturne Tools

A collection of modern C# tools built with the Spectre.Console CLI framework for managing Nocturne infrastructure, configuration, and external service integration.

## 🛠️ Available Tools

### Nocturne Connect

**Path:** `Nocturne.Tools.Connect`

A modern C# CLI tool providing secure, reliable connectivity between various diabetes management platforms and Nightscout instances. Built with Spectre.Console framework for excellent command-line experience.

### Configuration Generator

**Path:** `Nocturne.Tools.Config`

A smart configuration generator that creates comprehensive example configuration files for Nocturne in multiple formats (JSON, YAML, Environment Variables).

### MCP Server

**Path:** `Nocturne.Tools.McpServer`

A Model Context Protocol (MCP) server providing AI and automation tools for interacting with Nocturne glucose data APIs. Supports both stdio and Server-Sent Events (SSE) transports.

## 🌟 Features

### Shared Infrastructure

- **🔧 Spectre.Console Framework**: All tools built with modern Spectre.Console CLI framework for consistent user experience
- **📊 Progress Reporting**: Real-time progress tracking across all operations
- **🛡️ Type-Safe Configuration**: Comprehensive validation with helpful error messages
- **📝 Structured Logging**: Consistent logging patterns with configurable verbosity
- **⚙️ Dependency Injection**: Modern .NET patterns for maintainable, testable code

### Nocturne Connect

- **🔗 Multi-Source Support**: Connect data from various diabetes management platforms
- **🛡️ Modern Architecture**: Built with .NET 9.0 using modern C# practices and security standards
- **⚙️ Interactive Setup**: Guided configuration wizard with validation
- **🔍 Connection Testing**: Validate connections before running operations
- **📊 Status Monitoring**: Monitor sync status and health in real-time
- **🔧 Flexible Deployment**: Support for multiple deployment scenarios

### Configuration Generator

- **📋 Multiple Formats**: Generate configurations in JSON, YAML, and Environment Variables
- **🎯 Comprehensive Templates**: Full Nocturne configuration with all sections
- **💬 Smart Comments**: Helpful documentation embedded in generated configs
- **✅ Validation**: Built-in configuration validation and checking
- **🔧 Environment-Aware**: Generate configs for different deployment environments

### MCP Server

- **🤖 AI Integration**: Model Context Protocol server for AI tool integration
- **🚀 Dual Transport**: Supports both stdio and Server-Sent Events (SSE) transports
- **📊 Glucose Tools**: Comprehensive glucose data analysis and management tools
- **🔄 Backward Compatibility**: Legacy command-line argument support
- **🌐 Web Interface**: Optional web interface with health checks and status endpoints

## �📱 Supported Data Sources

| Source               | Description                        | Status       |
| -------------------- | ---------------------------------- | ------------ |
| **Glooko**           | Diabetes data platform integration | ✅ Supported |
| **MiniMed CareLink** | Medtronic diabetes device data     | ✅ Supported |
| **Dexcom Share**     | Continuous glucose monitoring data | ✅ Supported |
| **LibreLinkUp**      | FreeStyle Libre glucose data       | ✅ Supported |
| **Nightscout**       | Nightscout-to-Nightscout sync      | ✅ Supported |

## 🚀 Quick Start

### Prerequisites

- **.NET 9.0 or higher** - [Download here](https://dotnet.microsoft.com/download)
- **Nocturne instance** - Your target Nocturne API (for Connect and MCP tools)
- **Data source credentials** - Account for your chosen diabetes platform (Connect tool)

### Installation & Setup

1. **Clone and build the project:**

   ```bash
   git clone <repository-url>
   cd nocturne/src/Tools
   dotnet build
   ```

2. **Run any tool with help to see available commands:**

   ```bash
   # Connect tool
   dotnet run --project Nocturne.Tools.Connect --help

   # Config generator
   dotnet run --project Nocturne.Tools.Config --help

   # MCP server
   dotnet run --project Nocturne.Tools.McpServer --help
   ```

### Tool-Specific Quick Starts

#### Nocturne Connect

```bash
# Interactive setup
dotnet run --project Nocturne.Tools.Connect init --interactive

# Test connections
dotnet run --project Nocturne.Tools.Connect test

# Start syncing
dotnet run --project Nocturne.Tools.Connect run --daemon
```

#### Configuration Generator

```bash
# Generate JSON configuration
dotnet run --project Nocturne.Tools.Config generate --format json

# Generate with comments and custom path
dotnet run --project Nocturne.Tools.Config generate \
  --output "my-config.json" \
  --comments true

# Validate existing configuration
dotnet run --project Nocturne.Tools.Config validate \
  --config "appsettings.json"
```

#### MCP Server

```bash
# Start with stdio transport (default)
dotnet run --project Nocturne.Tools.McpServer server

# Start with web/SSE transport
dotnet run --project Nocturne.Tools.McpServer server --web --port 5000

# With custom API URL
dotnet run --project Nocturne.Tools.McpServer server \
  --api-url "http://localhost:1612" \
  --verbose
```

## 📋 Command Reference

All tools are built with the Spectre.Console CLI framework and provide consistent help and command structure.

### Nocturne Connect Commands

| Command   | Description                | Examples                      |
| --------- | -------------------------- | ----------------------------- |
| `init`    | Initialize configuration   | `init --interactive`          |
| `run`     | Start data synchronization | `run --daemon`, `run --once`  |
| `test`    | Test connections           | `test --all`, `test --source` |
| `config`  | Manage configuration       | `config --validate`           |
| `status`  | Show sync status           | `status --watch`              |
| `version` | Show version information   | `version --detailed`          |

### Configuration Generator Commands

| Command    | Description                  | Examples                              |
| ---------- | ---------------------------- | ------------------------------------- |
| `generate` | Generate configuration files | `generate --format json --output ...` |
| `validate` | Validate configuration files | `validate --config appsettings.json`  |
| `version`  | Show version information     | `version --detailed`                  |

### MCP Server Commands

| Command   | Description              | Examples                   |
| --------- | ------------------------ | -------------------------- |
| `server`  | Start MCP server         | `server --web --port 5000` |
| `version` | Show version information | `version --detailed`       |

### Available MCP Tools (when server is running)

| Tool                    | Description                               |
| ----------------------- | ----------------------------------------- |
| `GetCurrentEntry`       | Get the most recent glucose reading       |
| `GetRecentEntries`      | Get recent glucose entries with filtering |
| `GetEntriesByDateRange` | Get entries within a specific date range  |
| `GetEntryById`          | Get a specific entry by ID                |
| `CreateEntry`           | Create a new glucose entry                |
| `GetGlucoseStatistics`  | Get glucose statistics and time in range  |
| `GetEntryCount`         | Get entry count statistics                |

### Detailed Command Usage

#### Nocturne Connect Examples

```bash
# Interactive setup wizard (recommended for first-time users)
dotnet run --project Nocturne.Tools.Connect init --interactive

# Test all connections
dotnet run --project Nocturne.Tools.Connect test --all

# Run with custom configuration
dotnet run --project Nocturne.Tools.Connect run --config "production.env" --daemon

# Dry run (test without uploading)
dotnet run --project Nocturne.Tools.Connect run --dry-run --verbose
```

#### Configuration Generator Examples

```bash
# Generate JSON configuration with comments
dotnet run --project Nocturne.Tools.Config generate \
  --format json \
  --output "appsettings.example.json" \
  --comments true

# Generate environment variables format
dotnet run --project Nocturne.Tools.Config generate \
  --format env \
  --output ".env.example" \
  --environment "Production"

# Generate YAML configuration
dotnet run --project Nocturne.Tools.Config generate \
  --format yaml \
  --output "config.yml" \
  --overwrite

# Validate existing configuration
dotnet run --project Nocturne.Tools.Config validate \
  --config "appsettings.json" \
  --verbose
```

#### MCP Server Examples

```bash
# Start with stdio transport (for console-based MCP clients)
dotnet run --project Nocturne.Tools.McpServer server

# Start with SSE transport (for web-based MCP clients)
dotnet run --project Nocturne.Tools.McpServer server \
  --web \
  --port 5000 \
  --api-url "http://localhost:1612" \
  --verbose

# Start with custom configuration
dotnet run --project Nocturne.Tools.McpServer server \
  --config "mcp-config.json" \
  --timeout 60

# Get version and capabilities
dotnet run --project Nocturne.Tools.McpServer version --detailed
```

## ⚙️ Configuration

Each tool uses a modern configuration system with type-safe validation and helpful error messages.

### Configuration Methods

All tools support multiple configuration methods:

1. **Command-line arguments** (highest priority)
2. **Configuration files** (JSON, YAML, Environment Variables)
3. **Environment variables**
4. **Default values** (lowest priority)

### Tool-Specific Configuration

#### Nocturne Connect Configuration

The Connect tool uses environment variables stored in a `.env` file. Use the interactive setup for guided configuration:

```bash
dotnet run --project Nocturne.Tools.Connect init --interactive
```

Example configuration:

```bash
# Nocturne API Configuration
NOCTURNE_API_URL=http://localhost:1612
NOCTURNE_API_SECRET=your-api-secret

# Data Source Configuration
CONNECT_SOURCE=glooko  # or minimedcarelink, dexcomshare, linkup, nightscout
CONNECT_GLOOKO_EMAIL=your-email@example.com
CONNECT_GLOOKO_PASSWORD=your-password
CONNECT_GLOOKO_SERVER=eu.api.glooko.com
```

#### Configuration Generator

Generate example configurations for any Nocturne component:

```bash
# Generate comprehensive JSON configuration
dotnet run --project Nocturne.Tools.Config generate \
  --format json \
  --output "appsettings.example.json" \
  --comments true
```

#### MCP Server Configuration

The MCP server supports both command-line and configuration file options:

```bash
# Command-line configuration
dotnet run --project Nocturne.Tools.McpServer server \
  --api-url "http://localhost:1612" \
  --port 5000 \
  --verbose

# Or use a configuration file
dotnet run --project Nocturne.Tools.McpServer server \
  --config "mcp-config.json"
```

### Global Options

All tools support these common options:

- `--help` - Show detailed help information
- `--version` - Display version information
- `--verbose` - Enable detailed logging
- `--config <path>` - Use custom configuration file

## 🔧 Troubleshooting

### Common Issues

**Configuration Problems:**

```bash
# Validate configuration (Connect tool)
dotnet run --project Nocturne.Tools.Connect config --validate

# Validate configuration file (Config tool)
dotnet run --project Nocturne.Tools.Config validate --config "appsettings.json"

# Test connections
dotnet run --project Nocturne.Tools.Connect test --all
```

**Authentication Failures:**

- Use configuration validation commands to check credentials
- Verify API endpoints are accessible
- Check API secrets and connection strings
**Connection Issues:**

- Use connection testing commands before running operations
- Verify network connectivity and firewall settings
- Check that all required services are running

**MCP Server Issues:**

- Check that the Nocturne API is running and accessible
- Verify port availability for SSE transport mode
- Use `--verbose` for detailed MCP protocol logging
- Test API connectivity before starting the server

### Getting Help

1. **Built-in Help:** All tools support `--help` with detailed usage information
2. **Version Information:** Use `version --detailed` for comprehensive system info
3. **Verbose Logging:** Add `--verbose` to any command for detailed output
4. **Configuration Validation:** Each tool has built-in validation commands
5. **Connection Testing:** Test connections before running operations
6. **Progress Reporting:** All tools provide real-time progress information

## 🔄 Deployment Options

### Systemd Service (Linux)

Example service for Nocturne Connect:

```ini
[Unit]
Description=Nocturne Connect Daemon
After=network.target

[Service]
Type=simple
User=nocturne
WorkingDirectory=/opt/nocturne/src/Tools
ExecStart=/usr/bin/dotnet run --project Nocturne.Tools.Connect run --daemon
Restart=always
RestartSec=10
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable nocturne-connect
sudo systemctl start nocturne-connect
```

### Docker Deployment

Example Dockerfile for any tool:

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY src/Tools ./Tools
COPY *.sln ./
RUN dotnet publish Tools/Nocturne.Tools.Connect -c Release -o out

ENTRYPOINT ["dotnet", "out/Nocturne.Tools.Connect.dll", "run", "--daemon"]
```

### MCP Server Deployment

The MCP server supports both console and web deployment modes:

```bash
# Console mode (stdio transport)
dotnet run --project Nocturne.Tools.McpServer server

# Web mode (SSE transport)
dotnet run --project Nocturne.Tools.McpServer server --web --port 5000
```

### Scheduled Operations

Use cron for periodic operations:

```bash
# Run Connect sync every 15 minutes
*/15 * * * * cd /opt/nocturne/src/Tools && dotnet run --project Nocturne.Tools.Connect run --once

# Generate fresh configuration daily
0 2 * * * cd /opt/nocturne/src/Tools && dotnet run --project Nocturne.Tools.Config generate --overwrite
```

## 🏗️ Development

### Building from Source

```bash
# Clone repository
git clone <repository-url>
cd nocturne/src/Tools

# Restore dependencies
dotnet restore

# Build all tools
dotnet build

# Run specific tool
dotnet run --project Nocturne.Tools.Connect --help
```

### Project Architecture

The tools follow a modern, layered architecture:

```
src/Tools/
├── Nocturne.Tools.Abstractions/    # Shared interfaces and contracts
│   ├── Commands/                   # Command interfaces
│   ├── Configuration/              # Configuration interfaces
│   └── Services/                   # Service interfaces
├── Nocturne.Tools.Core/           # Shared implementation
│   ├── Commands/                  # Base command classes
│   ├── Services/                  # Common services
│   └── SpectreApplicationBuilder.cs # Spectre.Console extensions
├── Nocturne.Tools.Connect/        # Connect tool
│   ├── Commands/                  # Tool-specific commands
│   ├── Configuration/             # Tool configuration
│   └── Services/                  # Tool services
├── Nocturne.Tools.Config/         # Configuration generator
└── Nocturne.Tools.McpServer/      # MCP server
```

## 📄 License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

For major changes, please open an issue first to discuss what you would like to change.

---

**Note:** This is a community project and is not affiliated with Abbott, Medtronic, Dexcom, Glooko, or Nightscout. Use at your own risk and always verify data accuracy.
