using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SimioUnrealEngineLiveLinkConnector.Utils
{
    /// <summary>
    /// Network utilities for LiveLink connection management
    /// </summary>
    public static class NetworkUtils
    {
        /// <summary>
        /// Tests if a host is reachable (basic connectivity test)
        /// </summary>
        /// <param name="host">Host name or IP address</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if host is reachable</returns>
        public static async Task<bool> IsHostReachableAsync(string host, int timeoutMs = 5000)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            try
            {
                // For localhost, always return true
                if (IsLocalhostAddress(host))
                {
                    return true;
                }

                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(host, timeoutMs);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                // If ping fails, try TCP connection test as fallback
                return false;
            }
        }

        /// <summary>
        /// Tests if a specific port is open on a host
        /// </summary>
        /// <param name="host">Host name or IP address</param>
        /// <param name="port">Port number</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if port is open</returns>
        public static async Task<bool> IsPortOpenAsync(string host, int port, int timeoutMs = 5000)
        {
            if (string.IsNullOrWhiteSpace(host) || port < 1 || port > 65535)
            {
                return false;
            }

            try
            {
                using (var client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(host, port);
                    var timeoutTask = Task.Delay(timeoutMs);
                    
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    
                    if (completedTask == connectTask && client.Connected)
                    {
                        return true;
                    }
                    
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the given address is a localhost address
        /// </summary>
        /// <param name="host">Host name or IP address</param>
        /// <returns>True if localhost</returns>
        public static bool IsLocalhostAddress(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates if a port number is in valid range
        /// </summary>
        /// <param name="port">Port number to validate</param>
        /// <returns>True if port is valid</returns>
        public static bool IsValidPort(int port)
        {
            return port >= 1 && port <= 65535;
        }

        /// <summary>
        /// Gets the default LiveLink port
        /// </summary>
        /// <returns>Default port number for LiveLink</returns>
        public static int GetDefaultLiveLinkPort()
        {
            return 11111;
        }

        /// <summary>
        /// Formats an endpoint for display
        /// </summary>
        /// <param name="host">Host name or IP</param>
        /// <param name="port">Port number</param>
        /// <returns>Formatted endpoint string</returns>
        public static string FormatEndpoint(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return $"[invalid]:{port}";
            }

            // Handle IPv6 addresses
            if (host.Contains(":") && !host.StartsWith("["))
            {
                return $"[{host}]:{port}";
            }

            return $"{host}:{port}";
        }

        /// <summary>
        /// Suggests alternative ports if the specified port might be in use
        /// </summary>
        /// <param name="basePort">Base port number</param>
        /// <returns>Array of alternative port suggestions</returns>
        public static int[] SuggestAlternatePorts(int basePort)
        {
            return new int[]
            {
                basePort + 1,
                basePort + 10,
                basePort + 100,
                basePort == 11111 ? 11112 : 11111, // LiveLink default
                basePort == 11111 ? 11113 : 11111 + 1
            };
        }
    }
}