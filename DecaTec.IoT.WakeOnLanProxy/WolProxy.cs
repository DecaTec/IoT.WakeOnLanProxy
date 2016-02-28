using System;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace DecaTec.IoT.WakeOnLanProxy
{
    public sealed class WolProxy
    {
        // Socket to receive UDP packets.
        private DatagramSocket datagramSocketReceive;
        // Socket to send UDP packets.
        private DatagramSocket datagramSocketSend;

        // Ports for sending/receiving.
        private int listenPort;
        private int sendPort;

        public WolProxy() : this(9, 9)
        {

        }

        public WolProxy(int listenPort) : this(listenPort, 9)
        {
        }

        public WolProxy(int listenPort, int sendPort)
        {
            this.listenPort = listenPort;
            this.sendPort = sendPort;
        }

        public async void Start()
        {
            this.datagramSocketReceive = new DatagramSocket();
            this.datagramSocketReceive.MessageReceived += Socket_MessageReceived;

            this.datagramSocketSend = new DatagramSocket();

            var portStr = this.listenPort.ToString(CultureInfo.InvariantCulture);
            await this.datagramSocketReceive.BindServiceNameAsync(portStr);

            Log.WriteLog("Wake On LAN Proxy started (listening on port " + portStr + ")");
        }

        public void Stop()
        {
            if (this.datagramSocketSend != null)
            {
                this.datagramSocketSend.Dispose();
                this.datagramSocketSend = null;
            }

            if (this.datagramSocketReceive != null)
            {
                this.datagramSocketReceive.MessageReceived -= Socket_MessageReceived;
                this.datagramSocketReceive.Dispose();
                this.datagramSocketReceive = null;
            }

            Log.WriteLog("Wake On LAN Proxy stopped");
        }

        private async void Socket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            try
            {
                // Get the content of the UDP packet.
                var length = args.GetDataReader().UnconsumedBufferLength;
                var bArr = new byte[length];
                args.GetDataReader().ReadBytes(bArr);

                // Re-send Magic Packet only if it was not received from the local IP and it is a real MagicPacket.
                var ip = GetLocalIPAddress();
                var remoteAddress = args.RemoteAddress;
                if (ip != args.RemoteAddress.DisplayName && IsByteArrayMagicPacket(bArr))
                    await SendMagicPacket(bArr, remoteAddress);
            }
            catch (Exception ex)
            {
                Log.WriteLog("ERROR: " + ex.Message);
            }
        }

        private async Task SendMagicPacket(byte[] magicPacket, HostName remoteAddress)
        {
            // This forwards the received Magic Packet (as byte array) to the network's broadcast address.
            try
            {
                // Send Magic Packet to broadcast address (port 9).
                var portStr = this.sendPort.ToString(CultureInfo.InvariantCulture);
                Log.WriteLog(string.Format("Forwarding Magic Paket from {0} to MAC address {1} (Port {2})", remoteAddress.CanonicalName, GetMacStringFromMagicPacket(magicPacket), sendPort));

                using (var stream = await this.datagramSocketSend.GetOutputStreamAsync(new HostName("255.255.255.255"), portStr))
                {
                    using (var writer = new DataWriter(stream))
                    {
                        writer.WriteBytes(magicPacket);
                        await writer.StoreAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLog("ERROR: " + ex.Message);
            }
        }

        private string GetLocalIPAddress()
        {
            var hostNames = NetworkInformation.GetHostNames();

            foreach (var item in hostNames)
            {
                if (item.Type == HostNameType.Ipv4 && item.IPInformation != null)
                    return item.DisplayName;
            }

            return string.Empty;
        }

        private bool IsByteArrayMagicPacket(byte[] bArr)
        {
            try
            {
                // Header 6x FF.
                for (int i = 0; i < 6; i++)
                {
                    if (bArr[i] != 0xFF)
                        return false;
                }

                // Get the MAC address.
                var macArr = new byte[6];

                for (int i = 0; i < macArr.Length; i++)
                {
                    macArr[i] = bArr[i + 6];
                }

                // This MAC adress has to be the same 16 times in a row.
                for (int i = 1; i < 17; i++)
                {
                    var checkArr = new byte[6];
                    Array.Copy(bArr, 6 * i, checkArr, 0, 6);

                    for (int j = 0; j < checkArr.Length; j++)
                    {
                        if (checkArr[j] != macArr[j])
                            return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLog("ERROR: " + ex.Message);
                return false;
            }
        }

        private string GetMacStringFromMagicPacket(byte[] magicPacket)
        {
            // Get the MAC address.
            var macArr = new byte[6];

            for (int i = 0; i < macArr.Length; i++)
            {
                macArr[i] = magicPacket[i + 6];
            }

            return ConvertMacByteArrayToString(macArr);
        }

        private static string ConvertMacByteArrayToString([ReadOnlyArray]byte[] macByte)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < macByte.Length; i++)
            {
                sb.Append(macByte[i].ToString("x2"));

                if (i % 1 == 0 && i != macByte.Length - 1)
                    sb.Append(":");
            }

            return sb.ToString().ToUpper();
        }
    }
}
