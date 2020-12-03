using ArchBench.PlugIns;
using HttpServer;
using HttpServer.Sessions;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;

namespace ArchBench.Plugins.Broker
{
    public class PluginBroker : IArchBenchHttpPlugIn
    {
        public string Name => "Broker Plug-in";

        public string Description => "Implentação do padrão Broker";

        public string Author => "Ricardo Lucas";

        public string Version => "1.0";

        public bool Enabled { get; set; }
        public IArchBenchPlugInHost Host { get; set; }

        public IArchBenchSettings Settings { get; } = new ArchBenchSettings();

        private List<KeyValuePair<string, int>> mServers = new List<KeyValuePair<string, int>>();
        private Dictionary<string, int> mSessionsClient = new Dictionary<string, int>();
        public TcpListener mListener { get; private set; }
        public Thread mRegisterThread { get; private set; }
        private int mNextServer { get; set; }

        /// <summary>
        /// Inicializa o Broker com um Listener em TCP na porta 900
        /// Incia uma tarefa em background
        /// </summary>
        public void Initialize()
        {
            mListener = new TcpListener(IPAddress.Any, 9000);
            mRegisterThread = new Thread(ReceiveThreadFunction) { IsBackground = true };
            mRegisterThread.Start();
        }

        public void Dispose()
        {
            //Vazio
        }

        /// <summary>
        /// Recebe um pedido de registo de um servidor
        /// Adiciona a lista de servidores
        /// </summary>
        /// <param name="aAddress"></param>
        /// <param name="aPort"></param>
        private void Regist(string aAddress, int aPort)
        {
            if (mServers.Any(p => p.Key == aAddress && p.Value == aPort)) return;
            mServers.Add(new KeyValuePair<string, int>(aAddress, aPort));
            Host.Logger.WriteLine("Added server {0}:{1}.", aAddress, aPort);
        }

        /// <summary>
        /// Recebe um pedido de desativar de um servidor
        /// Remove a lista de servidores
        /// </summary>
        /// <param name="aAddress"></param>
        /// <param name="aPort"></param>
        private void Unregist(string aAddress, int aPort)
        {
            if (mServers.Remove(new KeyValuePair<string, int>(aAddress, aPort)))
            {
                Host.Logger.WriteLine("Removed server {0}:{1}.", aAddress, aPort);
            }
            else
            {
                Host.Logger.WriteLine("The server {0}:{1} is not registered.", aAddress, aPort);
            }
        }

        /// <summary>
        /// Tarefa em background
        /// Espera que servidores pela porta 9000 realize um pedido
        /// </summary>
        private void ReceiveThreadFunction()
        {
            try
            {
                mListener.Start();
                //buffer
                byte[] bytes = new byte[256];
                while (true)
                {
                    var client = mListener.AcceptTcpClient();
                    var stream = client.GetStream();

                    int count = stream.Read(bytes, 0, bytes.Length);
                    if (count != 0)
                    {
                        string data = Encoding.ASCII.GetString(bytes, 0, count);
                        var parts = data.Split(':');

                        switch (parts[0])
                        {
                            case "+":
                                Regist(parts[1], int.Parse(parts[2]));
                                break;
                            case "-":
                                Unregist(parts[1], int.Parse(parts[2]));
                                break;
                        }
                    }
                    client.Close();
                }
            }
            catch (SocketException e)
            {
                Host.Logger.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                mListener.Stop();
            }
        }

        /// <summary>
        /// Recebe pedidos do cliente e pede aos servidores registados para processar 
        /// Verifica o content-tpe na resposta
        /// Envia ao cliente da melhor forma
        /// </summary>
        /// <param name="aRequest"></param>
        /// <param name="aResponse"></param>
        /// <param name="aSession"></param>
        /// <returns> SUCESSO OU FALHA </returns>
        public bool Process(IHttpRequest aRequest, IHttpResponse aResponse, IHttpSession aSession)
        {
            //IP do broker
            string host = $"{aRequest.Uri.Host}:{aRequest.Uri.Port}";


            //Procura um servidor para o cliente

            int index = getServer(aSession.Id);

            if (index == -1) return false;

            //target IP (servidor)
            string target = $"http://{mServers[index].Key}:{mServers[index].Value}{aRequest.UriPath}";

            //Identificador de recursos
            Uri uri = new Uri(target);

            Host.Logger.WriteLine($"Sending request from broker server {host} to service server {mServers[index].Key}:{mServers[index].Value}");

            WebClient client = new WebClient();
            try
            {
                byte[] bytes = null;

                //envia para o servidor as cookies do browser
                if (aRequest.Headers["Cookie"] != null)
                {
                    client.Headers.Add("Cookie", aRequest.Headers["Cookie"]);
                }

                //Verifica metodo e envia para o servidor
                switch (aRequest.Method)
                {
                    case Method.Post:
                        NameValueCollection form = new NameValueCollection();
                        foreach (HttpInputItem item in aRequest.Form)
                        {
                            form.Add(item.Name, item.Value);
                        }
                        bytes = client.UploadValues(uri, form);  //Form
                        break;
                    case Method.Get:
                        bytes = client.DownloadData(uri); // recebe
                        break;
                    default:
                        return false;
                }

                aResponse.ContentType = client.ResponseHeaders[HttpResponseHeader.ContentType];

                //Guarda uma cookie no boewser apartir de outro plugin
                if (client.ResponseHeaders["Set-Cookie"] != null)
                {
                    aResponse.AddHeader("Set-Cookie", client.ResponseHeaders["Set-Cookie"]);
                }

                //Responde da melhor forma
                if (aResponse.ContentType.StartsWith("image/"))
                {
                    string data = client.Encoding.GetString(bytes);
                    SendRequest(aResponse, client, data);

                    Host.Logger.WriteLine($"Receiving service from service server {mServers[index].Key}:{mServers[index].Value} back to  broker server {host} ");
                    Host.Logger.WriteLine(" ");
                }
                else if (aResponse.ContentType.StartsWith("video/"))
                {
                    string data = client.Encoding.GetString(bytes);
                    SendRequest(aResponse, client, data);

                    Host.Logger.WriteLine($"Receiving service from service server {mServers[index].Key}:{mServers[index].Value} back to  broker server {host} ");
                    Host.Logger.WriteLine(" ");
                }
                else if (aResponse.ContentType.Equals("text/html"))
                {
                    string data = client.Encoding.GetString(bytes);
                    data = data.Replace($"http://{mServers[index].Key}:{mServers[index].Value}/", "/");

                    SendRequest(aResponse, client, data);
                    Host.Logger.WriteLine($"Receiving service from service server {mServers[index].Key}:{mServers[index].Value} back to  broker server {host} ");
                    Host.Logger.WriteLine(" ");
                }
                else
                {
                    aResponse.Body.Write(bytes, 0, bytes.Length);
                }
                return true;

            }
            catch (Exception e)
            {
                Host.Logger.WriteLine($"Error plugin Broker : {e.Message}");
                return false;
            }

        }

        /// <summary>
        /// procura se o utilizado ja utilizou um servidor
        /// </summary>
        /// <param name="aSession"></param>
        /// <returns> index da lista de servidores </returns>
        private int getServer(string aId)
        {
            if (mSessionsClient.ContainsKey(aId))
            {
                return mSessionsClient[aId];
            }
            else
            {
                return getServerNewClient(aId);
            }
        }

        /// <summary>
        /// Encontra um novo servidor e adiciona o cliente á lista
        /// </summary>
        /// <param name="id"></param>
        /// <returns> </returns>
        private int getServerNewClient(string id)
        {
            int index = GetNextServer();
            if (index != -1)
            {
                mSessionsClient.Add(id, index);
            }
            return index;
        }

        /// <summary>
        /// Escolha do servidor
        /// </summary>
        /// <returns>index da lista de servidores </returns>
        private int GetNextServer()
        {
            if (mServers.Count == 0) return -1;
            mNextServer = (mNextServer + 1) % mServers.Count;
            return mNextServer;
        }

        /// <summary>
        /// Responde ao cliente pelo body
        /// </summary>
        /// <param name="aResponse"></param>
        /// <param name="aClient"></param>
        /// <param name="aData"></param>
        private void SendRequest(IHttpResponse aResponse, WebClient aClient, string aData)
        {
            StreamWriter writer = new StreamWriter(aResponse.Body, aClient.Encoding);
            writer.Write(aData);
            writer.Flush();
        }
    }
}
