using ArchBench.PlugIns;
using HttpServer;
using HttpServer.Sessions;
using System;
using System.IO;
using System.Resources;
using System.Text;
using System.Web;


namespace ArchBench.Plugin.HtmlExample
{
    public class HtmlExample : IArchBenchHttpPlugIn
    {
        public string Name => "PlugIn HtmlExample";

        public string Description => "Exemplo de uma pagina HTML ";

        public string Author => "Ricardo Lucas";

        public string Version => "1.0";

        public bool Enabled { get; set; } = false;

        public IArchBenchPlugInHost Host { get; set; }

        public IArchBenchSettings Settings { get; } = new ArchBenchSettings();

        public void Dispose()
        {
        }

        public void Initialize()
        {
           
        }
        /// <summary>
        /// ESTE METODOS IRA PROCESSAR 2 TIDOS DE PEDIDOS (POST OU GET) 
        /// RETORNA FICHEIROS (IMAGENS E VIDEOS) OU RETORNA TEXTO EM HTML
        /// </summary>
        /// <param name="aRequest"></param>
        /// <param name="aResponse"></param>
        /// <param name="aSession"></param>
        /// <returns> SUCESSO OU FALHA </returns>
        public bool Process(IHttpRequest aRequest, IHttpResponse aResponse, IHttpSession aSession)
        {
            switch (aRequest.Method)  
            {
                case Method.Get:
                    if (aRequest.Uri.AbsolutePath.StartsWith("/favicon", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Host.Logger.WriteLine("Responding to request '/favicon' ");

                        var ext = aRequest.Uri.AbsolutePath.Substring(aRequest.Uri.AbsolutePath.LastIndexOf('.'));
                       
                        if (ext.Equals(".ico"))
                        {
                            SendBackResources(aResponse, @"favicon.ico", "image/x-icon");
                        }
                        else
                        {
                            SendBackResources(aResponse, @"favicon.png", "image/png");
                        }


                        Host.Logger.WriteLine("Sending back to broker ");
                        return true;
                    }

                    if (aRequest.Uri.AbsolutePath.StartsWith("/htmlex", StringComparison.InvariantCultureIgnoreCase))
                    {


                        var display = ReadHtmlFile(@"..\..\..\ArchBench.Plugin.HtmlExample\HtmlExample.html");
                        var writer = new StreamWriter(aResponse.Body);

                        Host.Logger.WriteLine("Responding to request '/htmlex' ");

                        writer.WriteLine(display);
                        writer.Flush();

                        Host.Logger.WriteLine("Sending back to broker ");
                        return true;
                    }

                    if (aRequest.Uri.AbsolutePath.StartsWith("/example", StringComparison.InvariantCultureIgnoreCase))
                    {


                        var display = ReadHtmlFile(@"..\..\..\ArchBench.Plugin.HtmlExample\HtmlExample2.html");

                        Host.Logger.WriteLine("Responding to request '/example' ");

                        var writer = new StreamWriter(aResponse.Body);

                        writer.WriteLine(display);
                        writer.Flush();

                        Host.Logger.WriteLine("Sending back to broker  ");
                        return true;
                    }

                    if (aRequest.Uri.AbsolutePath.StartsWith("/image", StringComparison.InvariantCultureIgnoreCase))
                    {

                        Host.Logger.WriteLine("Responding to request '/image' ");

                        SendBackResources(aResponse, @"images\greatimage.png", "image/png");

                        Host.Logger.WriteLine("Sending back to broker  ");
                        return true;


                    }

                    //FAZ 2 PEDIDOS PORQUE O 1 -> DOCUMENT E 2 -> MEDIA
                    if (aRequest.Uri.AbsolutePath.StartsWith("/video", StringComparison.InvariantCultureIgnoreCase))
                    {

                        Host.Logger.WriteLine("Responding to request '/video' ");

                        SendBackResources(aResponse, @"videos\wave.mp4", "video/mp4");

                        Host.Logger.WriteLine("Sending back to broker  ");
                        return true;

                    }
                    break;
                case Method.Post:

                    if (aRequest.Uri.AbsolutePath.StartsWith("/form", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Host.Logger.WriteLine("Responding to post request '/form' ");

                        if (aRequest.Form.Contains("fname") && aRequest.Form.Contains("lname"))
                        {
                            string[] stringArray = new string[2];
                            stringArray[0] = aRequest.Form["fname"].Value;
                            stringArray[1] = aRequest.Form["lname"].Value;

                            var result = CreateHtml(stringArray);

                            var writer = new StreamWriter(aResponse.Body);
                            writer.WriteLine(result);
                            writer.Flush();

                            Host.Logger.WriteLine("Sending back to broker  ");


                            return true;
                        }
                    }

                    //Nao consegui fazer ainda
                    if (aRequest.Uri.AbsolutePath.StartsWith("/post/image", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Host.Logger.WriteLine("Responding to post request '/post/image' ");

                        var ext = aRequest.Form.GetFile("file_pic").UploadFilename.Substring(aRequest.Form.GetFile("file_pic").UploadFilename.LastIndexOf('.'));
                        var extension = ext.ToLower();

                        Host.Logger.WriteLine($" name : {aRequest.Form["name"].Value}");
                        Host.Logger.WriteLine($" file_pic : {aRequest.Form.GetFile("file_pic").UploadFilename} + ext : {extension}");



                        if (aRequest.Form.Contains("name") && aRequest.Form.ContainsFile("file_pic"))
                        {

                            /* 
                            var x = (HttpInputItem) aRequest.Form.GetFile("file_pic")


                           string path = @"..\..\..\ArchBench.Plugin.HtmlExample\images\" + aRequest.Form["name"].Value + extension;
                            var result = $"<html><body> <h2> HTML Image Name: {aRequest.Form["name"].Value} </h2>  <img src='{extension}' width='500' height='333' ></ body ></ html > ";

                            var writer = new StreamWriter(aResponse.Body);
                            writer.WriteLine(result);
                            writer.Flush();

                            Host.Logger.WriteLine("Sending back to broker  ");*/


                            return true;
                        }
                    }
                    break;
                default:
                    return false;
            }
            return false;
        }

        /// <summary>
        /// LÊ UM FICHEIO HTML E TRANSFORMA NUMA STRING PARA SER UTILIZADA NA ESCRITA DO BODY
        /// </summary>
        /// <param name="htmlFileNameWithPath"></param>
        /// <returns> StringBuilder </returns>
        public StringBuilder ReadHtmlFile(string htmlFileNameWithPath)
        {
            StringBuilder storeContent = new StringBuilder();

            try
            {
                using (StreamReader htmlReader = new StreamReader(htmlFileNameWithPath))
                {
                    string lineStr;
                    while ((lineStr = htmlReader.ReadLine()) != null)
                    {
                        storeContent.Append(lineStr);
                    }
                }
            }
            catch (Exception objError)
            {
                throw objError;
            }

            return storeContent;
        }

        /// <summary>
        /// DEVOLVE AO CLIENTE RESURSOS (IMAGENS E VIDEO) 
        /// USANDO A MemoryStream E MODIFICANDO O Content-type E Content-Length DEPENDENTE DO TIPO DE FICHEIRO
        /// </summary>
        /// <param name="aResponse"></param>
        /// <param name="resourcePath"></param>
        /// <param name="contentType"></param>
        private void SendBackResources(IHttpResponse aResponse, string resourcePath, string contentType)
        {
            string path = @"..\..\..\ArchBench.Plugin.HtmlExample\"+ resourcePath;

            byte[] imageByteData = File.ReadAllBytes(path);
            MemoryStream aStream = new MemoryStream(imageByteData);

            aResponse.AddHeader("Content-type", contentType);
            aResponse.AddHeader("Content-Length", aStream.Length.ToString());


            aResponse.Body = aStream;
            aResponse.Send();
            aStream.Close();
        }

        /// <summary>
        /// RECEBE UM ARRAY DE STRING E TRAMSFORMA EM TEXTO
        /// </summary>
        /// <param name="array"></param>
        /// <returns> TEXTO EM HTML</returns>
        private string CreateHtml(string[] array)
        {
            string result = $"<html><body style='margin: 0px; background-color: linen;'> " +
                            $"<h2 style='text-align: center; color: #008CBA;'> First name: {array[0]} </h2>" +
                            $"<h2 style='text-align: center; color: #008CBA;'> Last name: {array[1]} </h2>"
                            + $"</body></html>";

            return result;
        }
    }
}
