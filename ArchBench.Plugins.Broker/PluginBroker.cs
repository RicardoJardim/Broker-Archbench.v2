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
        //Cenas do prof.
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
        /// INICIALIZA O BROKER COM UM LISTENER EM TCP NA PORTA 9000
        /// INICIA UMA TAREFA (THREAD) QUE IRA CORRER DURANTE TODO O PROCESSO EM BACKGROUND
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
        /// RECEBE UM PEDIDO DE REGISTO DE UM SERVIDOR 
        /// ADICIONA A LISTA DOS SERVIDORES
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
        /// RECEBE UM PEDIDO DE DESATIVAR O REGISTO DE UM SERVIDOR 
        /// REMOVE DA LISTA DE REGISTOS SE ESTE ESTIVER LA
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
        /// TAREFA (THREAD) QUE IRA CORRER EM BACKGROUND
        /// ESPERA QUE SERVIDORES PELA PORTA 9000 REALIZEM UM PEDIDO DE SE REGISTAR OU DESATIVAR O REGISTO
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
        /// PROCESSO PRINCIPAL DO BOKER
        /// RECEBE PEDIDOS DO CLIENTE E PEDE AOS SERVIDORES REGISTADOS PARA PROCESSAR ESSE PEDIDO
        /// AO REBER RESPOSTA DO SERVIDOR VERIFICA O SEU CONTENT-TYPE
        /// DE SEGUIDA RESPONDE AO CLIENTE DA MELHOR FORMA
        /// </summary>
        /// <param name="aRequest"></param>
        /// <param name="aResponse"></param>
        /// <param name="aSession"></param>
        /// <returns> SUCESSO OU FALHA </returns>
        public bool Process(IHttpRequest aRequest, IHttpResponse aResponse, IHttpSession aSession)
        {
            //IDENTIFICA O HOST (IP DO BROKER)
            string host = $"{aRequest.Uri.Host}:{aRequest.Uri.Port}";

          
            //PROCURA UM SERVIDOR PARA O CLIENTE
            int index = getServer(aSession, aRequest); 

            if (index == -1) return false;

            //IDENTIFICA O TARGET (IP E PORTA DO SERVIDOR)
            string target = $"http://{mServers[index].Key}:{mServers[index].Value}{aRequest.UriPath}"; 

            //INDENTIFICADOR DE RECURSOS ATRAVES DO TARGET
            Uri uri = new Uri(target);

            Host.Logger.WriteLine($"Sending request from broker server {host} to service server {mServers[index].Key}:{mServers[index].Value}");

            WebClient client = new WebClient();
            try
            {
                byte[] bytes = null;

                //GUARDA A COOKIE NO BROWSER DO CLIENTE
                if (aRequest.Cookies["session_id"] == null)
                {
                    var cookie = "session_id =" + aSession.Id;
                    aResponse.AddHeader("Set-Cookie", cookie);
                }

                //ADICIONA AS COOKIES AO CLIENT HEARDER CASO OS PLUGINS A NECESSITEM
                if (aRequest.Cookies["session_id"] != null)
                {
                    client.Headers.Add("Cookie", aRequest.Cookies["session_id"].Value);                 
                }              

                //VERIFICA O TIPO DO METODO E ENVIA AOS SERVIDORES DISPONIVEIS
                switch (aRequest.Method)
                {
                    case Method.Post:
                        NameValueCollection form = new NameValueCollection(); //COLEÇÃO NOME E VALOR
                        foreach (HttpInputItem item in aRequest.Form)
                        {
                            form.Add(item.Name, item.Value);
                        }
                        bytes = client.UploadValues(uri, form);  //ENVIA OS VALORES DO FORM
                        break;
                    case Method.Get:
                        bytes = client.DownloadData(uri); // RECEBE OS DADOS
                        break;
                    default:
                        return false;
                }

                //VERIFICA QUAL É O CONTENT TYPE DO PEDIDO
                aResponse.ContentType = client.ResponseHeaders[HttpResponseHeader.ContentType];         

                //RESPONDE AO CLIENTE DA MELHOR FORMA
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
                else if (aResponse.ContentType.StartsWith("text/html"))
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
            }
            catch (Exception e)
            {
                Host.Logger.WriteLine($"Error plugin Broker : {e.Message}");
            }
            return true;
        }

        /// <summary>
        /// ATRAVES DO ID DA SESSAO DO CLIENTE PROCURA SE ESTE JA UTILIZOU UM SERVIDOR
        /// CASO TENHA, O PEDIDO É FEITO SEMPRE PARA O MESMO SERVIDOR
        /// CASO NAO ESTEJA, UTILIZA A FUNÇAO GetNextServer() E ADICIONA A LISTA DE SESSOES
        /// </summary>
        /// <param name="aSession"></param>
        /// <returns> INDEX DA LISTA DE SERVIDORES </returns>
        private int getServer(IHttpSession aSession, IHttpRequest aCookie)
        {
            int index;

            if(aCookie.Cookies["session_id"] != null)
            {
                if (mSessionsClient.ContainsKey(aCookie.Cookies["session_id"].Value))
                {
                    index = mSessionsClient[aCookie.Cookies["session_id"].Value];
                }
                else
                {
                    index = getServerNewClient(aCookie.Cookies["session_id"].Value);
                }
            }
            else
            {
                index = getServerNewClient(aSession.Id);
            }
            return index;
        }

        /// <summary>
        /// 
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
        /// UTILIZAÇÃO DE ROUND-ROBIN PARA ESCOLHA DO SERVIDOR A RESPONDER AO PEDIDO
        /// </summary>
        /// <returns> INDEX DO SERVIDOR DA LISTA DOS SERVIDORES </returns>
        private int GetNextServer()
        {
            if (mServers.Count == 0) return -1;
            mNextServer = (mNextServer + 1) % mServers.Count;
            return mNextServer;
        }


        /// <summary>
        /// RESPONDE AO CLIENTE ATRAVÉS DO BODY OS DADOS PRENTENDIDOS
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
