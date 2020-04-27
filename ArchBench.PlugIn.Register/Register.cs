using ArchBench.PlugIns;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ArchBench.PlugIn.Register
{
    public class Register : IArchBenchPlugIn
    {
        public string Name => "Register Server Plug-in";
        public string Description => "Regista um servidor ao broker, pela porta 9000";
        public string Author => "Ricardo Lucas";
        public string Version => "1.0";

        public bool OnService { get; set; }

        public bool Enabled
        {
            get => OnService;
            set => Registration(value);
        }

        public IArchBenchPlugInHost Host { get; set; }

        public IArchBenchSettings Settings { get; } = new ArchBenchSettings();

        /// <summary>
        /// VALORES POR DEFEITO AO INICIAR
        /// </summary>
        public void Initialize()
        {
            Settings["ServerAddress"] = "127.0.0.1:9000";
            Settings["ServerPort"] = "8081";
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// REGISTA OU DESATIVA O REGISTO DO SERVIDOR AO BROKER 
        /// </summary>
        /// <param name="aOnService"></param>
        private void Registration(bool aOnService)
        {
            if (aOnService == OnService) return;
            OnService = aOnService;

            try
            {
                if (string.IsNullOrEmpty(Settings["ServerAddress"]))
                {
                    Host.Logger.WriteLine("The Server's Address is not defined.");
                    return;
                }

                var parts = Settings["ServerAddress"].Split(':');
                if (parts.Length != 2)
                {
                    Host.Logger.WriteLine($"The Server Address format is not well defined (must be <ip>:<port>): { Settings["ServerAddress"] }");
                    return;
                }

                if (!int.TryParse(parts[1], out int port))
                {
                    Host.Logger.WriteLine($"The Server Address format is not well defined (must be <ip>:<port>). A number is expected on <port> : { parts[1] }");

                }

                var client = new TcpClient(parts[0], port);

                var operation = OnService ? '+' : '-';
                var data = Encoding.ASCII.GetBytes(
                    $"{ operation }:{ GetIP() }:{ Settings["ServerPort"] }");

                var stream = client.GetStream();
                stream.Write(data, 0, data.Length);
                stream.Close();

                client.Close();
            }
            catch (SocketException e)
            {
                Host.Logger.WriteLine("SocketException: {0}", e);
            }
        }

        //DEVOLVE O IP DO HOST
        private static string GetIP()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) return ip.ToString();
            }
            return "0.0.0.0";
        }
    }
}
