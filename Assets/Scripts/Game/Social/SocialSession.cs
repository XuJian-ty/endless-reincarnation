using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Game.GameFlow;
using Game.Online;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectBase;

namespace Game.Social
{
    public class SocialSession : BaseManager<SocialSession>
    {
        private const int DiscoveryPort = 5087;
        private const int DiscoveryTimeoutMilliseconds = 700;

        private SocialUserInfo _currentUser;
        private string _serverBaseUrl;

        public event Action SessionChanged;

        public SocialUserInfo CurrentUser => _currentUser;
        public bool IsLoggedIn => _currentUser != null && !string.IsNullOrWhiteSpace(_currentUser.userId);
        public string ServerBaseUrl => _serverBaseUrl;

        public SocialSession()
        {
            _serverBaseUrl = string.Empty;
            TryApplyDiscoveredServerBaseUrl();
        }

        public void Login(SocialUserInfo userInfo)
        {
            if (userInfo == null || string.IsNullOrWhiteSpace(userInfo.userId))
                return;

            _currentUser = new SocialUserInfo
            {
                userId = userInfo.userId?.Trim(),
                username = userInfo.username?.Trim(),
            };

            SocialAidRequestNotifier.GetInstance();
            SocialSessionQuitHandler.EnsureExists();
            SessionChanged?.Invoke();
        }

        public void Logout()
        {
            if (_currentUser == null)
                return;

            _currentUser = null;
            SessionChanged?.Invoke();
        }

        private static string NormalizeServerBaseUrl(string serverBaseUrl)
        {
            string trimmed = string.IsNullOrWhiteSpace(serverBaseUrl)
                ? string.Empty
                : serverBaseUrl.Trim();

            return trimmed.TrimEnd('/');
        }

        private void TryApplyDiscoveredServerBaseUrl()
        {
            if (!TryDiscoverServerBaseUrl(out string discoveredUrl))
                return;

            _serverBaseUrl = NormalizeServerBaseUrl(discoveredUrl);
        }

        private static bool TryDiscoverServerBaseUrl(out string serverBaseUrl)
        {
            serverBaseUrl = string.Empty;
            try
            {
                using UdpClient client = new UdpClient(0)
                {
                    EnableBroadcast = true,
                    Client =
                    {
                        ReceiveTimeout = DiscoveryTimeoutMilliseconds,
                    },
                };

                byte[] requestBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new RealtimeDiscoveryEnvelope
                {
                    version = 3,
                    type = "serverDiscovery",
                    channel = "state",
                    sequence = DateTime.UtcNow.Ticks,
                    payload = new JObject(),
                }));
                foreach (IPEndPoint endPoint in BuildDiscoveryEndPoints())
                {
                    try
                    {
                        client.Send(requestBytes, requestBytes.Length, endPoint);
                    }
                    catch (Exception e)
                    {
                        Debug.Log($"[SocialSession] 联机服务器发现请求发送失败：{endPoint.Address}:{endPoint.Port} {e.Message}");
                    }
                }

                DateTime deadline = DateTime.UtcNow.AddMilliseconds(DiscoveryTimeoutMilliseconds);
                while (DateTime.UtcNow < deadline)
                {
                    int remainingMilliseconds = Mathf.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                    client.Client.ReceiveTimeout = remainingMilliseconds;

                    IPEndPoint remoteEndPoint = null;
                    byte[] responseBytes = client.Receive(ref remoteEndPoint);
                    if (remoteEndPoint == null || responseBytes == null || responseBytes.Length <= 0)
                        continue;

                    if (!IsUsableDiscoveredAddress(remoteEndPoint.Address))
                    {
                        Debug.Log($"[SocialSession] 忽略不可用的联机服务器发现地址：{remoteEndPoint.Address}");
                        continue;
                    }

                    RealtimeDiscoveryEnvelope response = JsonConvert.DeserializeObject<RealtimeDiscoveryEnvelope>(Encoding.UTF8.GetString(responseBytes));
                    if (response == null || !string.Equals(response.type, "serverDiscoveryAck", StringComparison.Ordinal))
                        continue;

                    int port = response.payload != null && response.payload.TryGetValue("socialServerPort", out JToken portToken)
                        ? Mathf.Max(1, portToken.Value<int>())
                        : 5076;
                    serverBaseUrl = $"http://{remoteEndPoint.Address}:{port}";
                    Debug.Log($"[SocialSession] 自动发现联机服务器：{serverBaseUrl}");
                    return true;
                }
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut || e.SocketErrorCode == SocketError.WouldBlock)
            {
                Debug.Log($"[SocialSession] 自动发现联机服务器失败：{e.Message}");
                return false;
            }
            catch (Exception e)
            {
                Debug.Log($"[SocialSession] 自动发现联机服务器失败：{e.Message}");
                return false;
            }

            return false;
        }

        private static List<IPEndPoint> BuildDiscoveryEndPoints()
        {
            var endPoints = new List<IPEndPoint>();
            var added = new HashSet<string>(StringComparer.Ordinal);

            foreach (IPAddress address in GetLocalIPv4Addresses())
            {
                AddDiscoveryEndPoint(endPoints, added, address);
            }

            foreach (IPAddress address in GetSubnetBroadcastAddresses())
            {
                AddDiscoveryEndPoint(endPoints, added, address);
            }

            AddDiscoveryEndPoint(endPoints, added, IPAddress.Broadcast);
            return endPoints;
        }

        private static List<IPAddress> GetLocalIPv4Addresses()
        {
            var addresses = new List<IPAddress>();
            var added = new HashSet<string>(StringComparer.Ordinal);
            IPHostEntry hostEntry;
            try
            {
                hostEntry = Dns.GetHostEntry(Dns.GetHostName());

                foreach (IPAddress address in hostEntry.AddressList)
                {
                    AddLocalIPv4Address(addresses, added, address);
                }
            }
            catch
            {
            }

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                IPInterfaceProperties properties;
                try
                {
                    properties = networkInterface.GetIPProperties();
                }
                catch
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation addressInfo in properties.UnicastAddresses)
                {
                    AddLocalIPv4Address(addresses, added, addressInfo.Address);
                }
            }

            return addresses;
        }

        private static void AddLocalIPv4Address(List<IPAddress> addresses, HashSet<string> added, IPAddress address)
        {
            if (!IsUsableDiscoveredAddress(address))
                return;

            string key = address.ToString();
            if (added.Add(key))
                addresses.Add(address);
        }

        private static IEnumerable<IPAddress> GetSubnetBroadcastAddresses()
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                IPInterfaceProperties properties;
                try
                {
                    properties = networkInterface.GetIPProperties();
                }
                catch
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation addressInfo in properties.UnicastAddresses)
                {
                    if (addressInfo.IPv4Mask == null || !IsUsableDiscoveredAddress(addressInfo.Address))
                        continue;

                    byte[] addressBytes = addressInfo.Address.GetAddressBytes();
                    byte[] maskBytes = addressInfo.IPv4Mask.GetAddressBytes();
                    if (addressBytes.Length != 4 || maskBytes.Length != 4)
                        continue;

                    var broadcastBytes = new byte[4];
                    for (int i = 0; i < broadcastBytes.Length; i++)
                    {
                        broadcastBytes[i] = (byte)(addressBytes[i] | ~maskBytes[i]);
                    }

                    yield return new IPAddress(broadcastBytes);
                }
            }
        }

        private static void AddDiscoveryEndPoint(List<IPEndPoint> endPoints, HashSet<string> added, IPAddress address)
        {
            string key = $"{address}:{DiscoveryPort}";
            if (added.Add(key))
                endPoints.Add(new IPEndPoint(address, DiscoveryPort));
        }

        private static bool IsUsableDiscoveredAddress(IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            byte[] bytes = address.GetAddressBytes();
            if (bytes.Length != 4)
                return false;

            if (bytes[0] == 127)
                return false;

            if (bytes[0] == 198 && bytes[1] >= 18 && bytes[1] <= 19)
                return false;

            if (bytes[0] == 169 && bytes[1] == 254)
                return false;

            return true;
        }

        private sealed class RealtimeDiscoveryEnvelope
        {
            public int version;
            public string type;
            public string channel;
            public string instanceId = string.Empty;
            public string userId = string.Empty;
            public string joinToken = string.Empty;
            public long sequence;
            public JObject payload;
        }
    }

    internal sealed class SocialSessionQuitHandler : MonoBehaviour
    {
        private static SocialSessionQuitHandler _instance;

        public static void EnsureExists()
        {
            if (_instance != null)
                return;

            GameObject root = new GameObject("SocialSessionQuitHandler");
            UnityEngine.Object.DontDestroyOnLoad(root);
            _instance = root.AddComponent<SocialSessionQuitHandler>();
        }

        private void OnApplicationQuit()
        {
            OnlineDungeonSessionCoordinator onlineCoordinator = OnlineDungeonSessionCoordinator.GetInstance();
            bool hadOnlineSession = onlineCoordinator.HasActiveSession;
            SocialAidSessionInfo session = onlineCoordinator.ActiveAidSession ?? SocialAidSessionCoordinator.GetInstance().ActiveSession;
            bool onlineSessionPrepared = onlineCoordinator.TryPrepareSaveAndQuit();
            if (onlineSessionPrepared)
                GameStateMachine.GetInstance().SaveCurrent(false);

            if (!hadOnlineSession && session != null && !string.IsNullOrWhiteSpace(session.sessionId))
            {
                SocialService.GetInstance().CloseAidSession(
                    session.sessionId,
                    (_, _) => { },
                    _ => { });
                onlineCoordinator.StopSession();
                SocialAidSessionCoordinator.GetInstance().StopSession();
            }

            if (SocialSession.GetInstance().IsLoggedIn)
            {
                SocialService.GetInstance().ClearActiveSaveContext(
                    (_, _) => { },
                    _ => { });
            }
        }
    }
}
