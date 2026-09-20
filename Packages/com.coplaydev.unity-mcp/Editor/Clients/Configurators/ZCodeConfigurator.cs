using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Models;
using UnityEngine;

namespace MCPForUnity.Editor.Clients.Configurators
{
    /// <summary>
    /// ZCode (Z.ai agentic development environment) configurator.
    /// Writes the project-scoped workspace config at {projectRoot}/.zcode/config.json
    /// using ZCode's native nested "mcp.servers" layout.
    /// See https://zcode.z.ai/cn/docs/mcp-services
    /// </summary>
    public class ZCodeConfigurator : JsonFileMcpConfigurator
    {
        private static readonly string WorkspaceConfigPath = BuildWorkspaceConfigPath();

        public ZCodeConfigurator() : base(new McpClient
        {
            name = "ZCode",
            SupportsHttpTransport = true,
            ServerContainerKey = "mcp.servers",
        })
        { }

        private static string BuildWorkspaceConfigPath()
        {
            try
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                if (!string.IsNullOrEmpty(projectRoot))
                    return Path.Combine(projectRoot, ".zcode", "config.json");
            }
            catch { }
            return Path.Combine(".zcode", "config.json");
        }

        /// <summary>Project-scoped config file instead of the OS user-profile path used by global clients.</summary>
        public override string GetConfigPath() => WorkspaceConfigPath;

        /// <summary>Detected when the ZCode app is installed (~/.zcode exists), even before the workspace file exists.</summary>
        public override bool IsInstalled
        {
            get
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return ParentDirectoryExists(Path.Combine(home, ".zcode", "config.json"));
            }
        }

        public override IList<string> GetInstallationSteps() => new List<string>
        {
            "Ensure ZCode is installed and opened this workspace at least once",
            "Click Configure to register UnityMCP in this project's .zcode/config.json",
            "Reopen the workspace (or restart ZCode) so it connects to the MCP server"
        };
    }
}
