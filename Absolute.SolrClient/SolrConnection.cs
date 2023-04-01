using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text.RegularExpressions;
using System.Xml;
using System.Diagnostics;
using System.Web;
using System.Reflection;

namespace Absolute.SolrClient
{
    public class SolrConnection
    {
        private readonly Uri _solrBaseUri;

        public SolrConnection(Uri solrBaseUri)
        {
            _solrBaseUri = new Uri(solrBaseUri.ToString().TrimEnd('/') + "/");
        }

        public XmlDocument GetXmlResult(string relativeUri, IDictionary<string, string> parameter)
        {
            var xdoc = new XmlDocument();
            try
            {
                var webRequest = (WebRequest)HttpWebRequest.Create(CombineUri(_solrBaseUri, new Uri(relativeUri, UriKind.Relative), parameter));
                webRequest.Method = "GET";
                var webResponse = webRequest.GetResponse();
                using (var responseStream = webResponse.GetResponseStream())
                {
                    xdoc.Load(responseStream);
                }
            }
            catch (Exception ex)
            {
                throw HandleException(ex);
            }
            return xdoc;
        }

        public XmlDocument PostXmlResult(string relativeUri, IDictionary<string, string> parameter)
        {

            var xdoc = new XmlDocument();
            xdoc.Load(Post(relativeUri, parameter));
            return xdoc;
        }

        public bool Get(string relativeUri, int timeout)
        {
            return Get(relativeUri, timeout, null);
        }

        public bool Get(string relativeUri, int timeout, IDictionary<string, string> parameter)
        {
            Exception exception = null;
            var tryCount = 0;
            var webResponse = (WebResponse)null;
            do
            {
                exception = null;
                var webRequest = (HttpWebRequest)null;
                try
                {
                    webRequest = (HttpWebRequest)WebRequest.Create(CombineUri(_solrBaseUri, new Uri(relativeUri, UriKind.Relative), parameter));
                    webRequest.Timeout = timeout;
                    webRequest.ReadWriteTimeout = timeout;
                    webRequest.Method = "GET";
                    webResponse = webRequest.GetResponse();
                }
                catch (Exception ex)
                {
                    exception = HandleException(ex);
                    tryCount++;
                }
                finally
                {
                    if (webResponse != null)
                    {
                        webResponse.Close();
                    }
                    if (webRequest != null)
                    {
                        webRequest.Abort();
                    }
                }
            }
            while (exception != null && tryCount < 3);
            if (exception != null)
            {
                throw exception;
            }
            return true;
        }

        public Stream Post(string relativeUri, IEnumerable<KeyValuePair<string, string>> parameter)
        {
            var paramString = string.Join("&", parameter.Select(x => string.Format("{0}={1}", x.Key, HttpUtility.UrlEncode(x.Value))));
            try
            {
                var webRequest = (WebRequest)HttpWebRequest.Create(CombineUri(_solrBaseUri, new Uri(relativeUri, UriKind.Relative), null));
                webRequest.Method = "POST";
                webRequest.ContentType = "application/x-www-form-urlencoded; charset=utf-8";
                using (var webStream = webRequest.GetRequestStream())
                {
                    var byteArr = Encoding.UTF8.GetBytes(paramString);
                    webStream.Write(byteArr, 0, byteArr.Length);
                    webStream.Close();
                }
                var webResponse = webRequest.GetResponse();
                return webResponse.GetResponseStream();
            }
            catch (Exception ex)
            {
                var Uri = string.Format("{0}{1}?{2}",_solrBaseUri, relativeUri,paramString);
                throw HandleException(ex);
            }
        }

        public Stream PostWithFailOver(string relativeUri, IEnumerable<KeyValuePair<string, string>> parameter)
        {
            var paramString = string.Join("&", parameter.Select(x => string.Format("{0}={1}", x.Key, HttpUtility.UrlEncode(x.Value))));

            var webRequest = (WebRequest)HttpWebRequest.Create(CombineUri(_solrBaseUri, new Uri(relativeUri, UriKind.Relative), null));
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded; charset=utf-8";
            using (var webStream = webRequest.GetRequestStream())
            {
                var byteArr = Encoding.UTF8.GetBytes(paramString);
                webStream.Write(byteArr, 0, byteArr.Length);
                webStream.Close();
            }
            var webResponse = webRequest.GetResponse();
            return webResponse.GetResponseStream();
            
        }
        
        public Stream GetStream(string relativeUri, IDictionary<string, string> parameter)
        {
            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(CombineUri(_solrBaseUri, new Uri(relativeUri, UriKind.Relative), parameter));
                webRequest.Method = "GET";
                webRequest.ContentType = "application/x-www-form-urlencoded";
                
                var webResponse = webRequest.GetResponse();

                return webResponse.GetResponseStream();
            }
            catch (Exception ex)
            {
                throw HandleException(ex);
            }
        }


        public bool PostStream(string relativeUri, string contentType, Stream contentStream)
        {
            return PostStream(relativeUri, contentType, contentStream, null);
        }

        public bool PostStream(string relativeUri, string contentType, Stream contentStream, IDictionary<string, string> parameter)
        {
            Exception exception = null;
            var tryCount = 0;
            var webResponse = (WebResponse)null;
            do
            {
                exception = null;
                var webRequest = (HttpWebRequest)null;
                try
                {
                    contentStream.Position = 0;
                    webRequest = (HttpWebRequest)WebRequest.Create(CombineUri(_solrBaseUri, new Uri(relativeUri, UriKind.Relative), parameter));
                    webRequest.Timeout = 600000;
                    webRequest.ReadWriteTimeout = 600000;
                    webRequest.Method = "POST";
                    webRequest.ContentType = contentType;
                    using (var webStream = webRequest.GetRequestStream())
                    {
                        int byteCount;
                        var buffer = new byte[4096];
                        byteCount = contentStream.Read(buffer, 0, buffer.Length);
                        while (byteCount > 0)
                        {
                            webStream.Write(buffer, 0, byteCount);
                            byteCount = contentStream.Read(buffer, 0, buffer.Length);
                        }
                        webStream.Close();
                    }

                    webResponse = webRequest.GetResponse();
                    string response = null;
                    using (var responseStream = webResponse.GetResponseStream())
                    {
                        using (var sr = new StreamReader(responseStream))
                        {
                            response = sr.ReadToEnd();
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is WebException)
                    {
                        var stream = (ex as WebException).Response.GetResponseStream();
                        using (var sr = new StreamReader(stream))
                        {
                            var msg = sr.ReadToEnd();
                            Console.Error.WriteLine(msg);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine(ex.ToString());
                    }
                    exception = HandleException(ex);
                    if (!exception.Message.Contains("Timeout"))
                    {
                        tryCount++;
                    }
                    Thread.Sleep(5000);
                }
                finally
                {
                    if (webResponse != null)
                    {
                        webResponse.Close();
                    }
                    if (webRequest != null)
                    {
                        webRequest.Abort();
                    }
                }
            }
            while (exception != null && tryCount < 3);
            if (exception != null)
            {
                contentStream.Position = 0;
                using (var fs = new FileStream(string.Format(@"logs\error_{0:yyyyMMddHHmmss}.log", DateTime.Now), FileMode.Create, FileAccess.Write))
                {
                    int cnt = 0;
                    const int LEN = 4096;
                    byte[] buffer = new byte[LEN];

                    while ((cnt = contentStream.Read(buffer, 0, LEN)) != 0)
                        fs.Write(buffer, 0, cnt);

                    fs.Close();
                }
                throw exception;
            }

            return true;
        }

        internal static Uri CombineUri(Uri baseUri, Uri relativeUri, IDictionary<string, string> parameter)
        {
            var uri = new Uri(baseUri, relativeUri);
            var uriBuilder = new UriBuilder(uri);
            if (parameter != null)
            {
                uriBuilder.Query = string.Join("&", parameter.Select(x => string.Format("{0}={1}", x.Key, x.Value)));
            }
            return uriBuilder.Uri;
        }

        private Exception HandleException(Exception ex)
        {
            Exception result = null;
            if (ex is WebException)
            {
                var wex = (WebException)ex;
                if (wex.Response != null)
                {
                    var message = new StreamReader(wex.Response.GetResponseStream()).ReadToEnd();
                    var regex = new Regex(@"<h1>(.*?)<\/h1>", RegexOptions.Singleline);
                    var match = regex.Match(message);
                    if (match.Success)
                    {
                        message = match.Groups[1].Value;
                    }
                    result = new Exception(message, ex);
                }
                else
                {
                    result = new Exception("Unbekannter Serverfehler.", ex);
                }
            }
            else
            {
                result = ex;
            }

            return result;
        }
    }
}
