using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Xml;
using System.Runtime.Serialization;
using System.Collections;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Globalization;
using System.Threading;
using System.ComponentModel;
using System.Configuration;
using System.Web;
using Absolute.SolrClient.ResponseHeader;

namespace Absolute.SolrClient
{
    public class SolrIndex<TDoc> : ISolrIndexWithFieldInfos<TDoc>, ISolrIndex<TDoc>, ISolrIndex where TDoc : ISolrDocument
    {
        public SolrIndex(string solrBaseUri, bool solrDeserialisiereOhneReflection = false)
        {
            SolrDeserialisiereOhneReflection = solrDeserialisiereOhneReflection;
            DocFields = new Dictionary<string, PropertyInfo>();
            SolrBaseUri = new Uri(solrBaseUri.TrimEnd('/') + "/");
            DocFields = DetermineDocFields();
            if (DocFields.Count == 0)
            {
                throw new Exception("Konnte keine Felder für den SolrIndex ermitteln.");
            }
            _serializer = new SolrSerializer<TDoc>(DocFields);
            DetermineCoreInfos();
        }

        public SolrIndex(string solrPrimaryUri, string solrSecondaryUri, bool solrDeserialisiereOhneReflection = false)
        {
            IsPrimaryUriActive = true;
            DocFields = new Dictionary<string, PropertyInfo>();
            SolrPrimaryUri = new Uri(solrPrimaryUri.TrimEnd('/') + "/");
            SolrBaseUri = SolrPrimaryUri;
            SolrDeserialisiereOhneReflection = solrDeserialisiereOhneReflection;

            DocFields = DetermineDocFields();
            if (DocFields.Count == 0)
            {
                throw new Exception("Konnte keine Felder für den SolrIndex ermitteln.");
            }
            _serializer = new SolrSerializer<TDoc>(DocFields);

            //Determine the secondary core infos
            if (!string.IsNullOrEmpty(solrSecondaryUri))
            {
                SolrSecondaryUri = new Uri(solrSecondaryUri.TrimEnd('/') + "/");
                SecondaryCoreInfos = DetermineCoreInfos(SolrSecondaryUri);
            }

            //Determine the primary core infos
            PrimaryCoreInfos = DetermineCoreInfos(SolrPrimaryUri);

            //Select the default core
            if (PrimaryCoreInfos != null)
            {
                CoreInfos = PrimaryCoreInfos;
            }
            else
            {
                CoreInfos = SecondaryCoreInfos;
                IsPrimaryUriActive = false;
            } 

            //Set catched error codes
            CatchedErrorCode = GetCatchedErrorCode();

            //Set timer interval
            var timerInterval = 30000;
            TimerInterval = timerInterval;
        }

        public readonly Uri SolrBaseUri;
        private readonly SolrSerializer<TDoc> _serializer;
        public ICollection<SolrCoreInfo> CoreInfos { get; private set; }
        private bool SolrDeserialisiereOhneReflection { get; set; }

        /// <summary>
        /// Primary core
        /// </summary>
        private ICollection<SolrCoreInfo> PrimaryCoreInfos { get; set; } 
        /// <summary>
        /// Secondary core
        /// </summary>
        private ICollection<SolrCoreInfo> SecondaryCoreInfos { get; set; }
        /// <summary>
        /// True ist the primary server ist used
        /// </summary>
        private bool IsPrimaryUriActive { get; set; }
        /// <summary>
        /// Primary uri
        /// </summary>
        private readonly Uri SolrPrimaryUri;
        /// <summary>
        /// Secondary uri
        /// </summary>
        private readonly Uri SolrSecondaryUri;
        /// <summary>
        /// Used to check the availability of the primary server
        /// </summary>
        private Timer _Timer { get; set; }
        /// <summary>
        /// Errrcodes which catched to use the Failover
        /// </summary>
        private List<int> CatchedErrorCode { get; set; }
        /// <summary>
        /// Interval in ms used from the Timer
        /// </summary>
        private int TimerInterval { get; set; }


        public SolrQueryDescription<TDoc> NewQueryDescription()
        {
            return new SolrQueryDescription<TDoc>(this);
        }

        public SolrQueryBuilder<TDoc> NewQueryBuilder()
        {
            return new SolrQueryBuilder<TDoc>(this);
        }

        public SolrQueryResult<TDoc> Query(SolrQueryDescription<TDoc> queryDesc)
        {
            if (string.IsNullOrEmpty(queryDesc.Query))
            {
                throw new Exception("Ohne Query kann keine Abfrage ausgeführt werden.");
            }

            var conn = new SolrConnection(CoreInfos.First().Uri);
            var stream = conn.Post("select", GenerateSolrParameterList(conn, queryDesc));
            return _serializer.Deserialize(stream, queryDesc);
        }

        private List<KeyValuePair<string, string>> GenerateSolrParameterList(SolrConnection solrConnection,SolrQueryDescription<TDoc> queryDesc)
        {
            var paramList = new List<KeyValuePair<string, string>>();
            if (CoreInfos.Count > 1)
            {
                paramList.Add(new KeyValuePair<string, string>("shards", string.Join(",", CoreInfos.Select(x => x.Uri.ToString()))));
            }
            var queryString = queryDesc.Query;
            if (string.IsNullOrEmpty(queryString))
            {
                queryString = "*:*";
            }
            else if (queryDesc.QueryFields.Count > 0)
            {
                paramList.Add(new KeyValuePair<string, string>("defType", "dismax"));
                paramList.Add(new KeyValuePair<string, string>("qf", string.Join("+", queryDesc.QueryFields.Select(x => string.Format("{0}^{1}", x.Key, x.Value.ToString(new CultureInfo("en-US")))))));
            }

            paramList.Add(new KeyValuePair<string, string>("q", queryString));
            if (queryDesc.StartIndex.HasValue)
            {
                paramList.Add(new KeyValuePair<string, string>("start", queryDesc.StartIndex.ToString()));
            }
            if (queryDesc.RowCount.HasValue)
            {
                paramList.Add(new KeyValuePair<string, string>("rows", queryDesc.RowCount.ToString()));
            }

            // Filter
            foreach (var fq in queryDesc.FilterQueries.Where(x => !string.IsNullOrEmpty(x)))
            {
                paramList.Add(new KeyValuePair<string, string>("fq", fq));
            }

            // GroupQueries
            if (queryDesc.GroupQueries.Count > 0)
            {
                paramList.Add(new KeyValuePair<string, string>("group", "true"));
                paramList.Add(new KeyValuePair<string, string>("group.limit", queryDesc.RowCount.ToString()));
                paramList.Add(new KeyValuePair<string, string>("group.offset", queryDesc.StartIndex.ToString()));
                foreach (var gq in queryDesc.GroupQueries)
                {
                    paramList.Add(new KeyValuePair<string, string>("group.query", gq));
                }
            }

            // Anzeigefelder
            var fl = string.Join(",", queryDesc.FieldNames);
            paramList.Add(new KeyValuePair<string, string>("fl", fl));

            // Facets
            if (queryDesc.FacetFields.Count > 0)
            {
                paramList.Add(new KeyValuePair<string, string>("facet", "on"));
                foreach (var facet in queryDesc.FacetFields)
                {
                    foreach (var kvp in facet.CreateParams())
                    {
                        paramList.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Value));
                    }
                }
            }
            if (queryDesc.FacetQueries.Count > 0)
            {
                if (!paramList.Any(x => x.Key == "facet"))
                {
                    paramList.Add(new KeyValuePair<string, string>("facet", "on"));
                }
                foreach (var query in queryDesc.FacetQueries.Select(x => x.Query))
                {
                    paramList.Add(new KeyValuePair<string, string>("facet.query", query));
                }
            }

            // Highlighting
            if (queryDesc.Highlighting.Fields.Count > 0)
            {
                paramList.Add(new KeyValuePair<string, string>("hl", "on"));
                if (queryDesc.Highlighting.RequireFieldMatch)
                {
                    paramList.Add(new KeyValuePair<string, string>("hl.requireFieldMatch", "true"));
                }
                paramList.Add(new KeyValuePair<string, string>("hl.fl", string.Join(",", queryDesc.Highlighting.Fields)));
            }

            // Sortierung
            if (queryDesc.SortFields.Count > 0)
            {
                paramList.Add(new KeyValuePair<string, string>("sort", string.Join(",", queryDesc.SortFields)));
            }

            //SolrParameters
            if (queryDesc.SolrParameters.Count > 0)
            {
                foreach (var param in queryDesc.SolrParameters)
                {
                    paramList.Add(new KeyValuePair<string, string>(param.Key, param.Value));
                }
            }

            // Statistics
            if (queryDesc.StatsFields.Count > 0)
            {
                paramList.Add(new KeyValuePair<string, string>("stats", "true"));
                foreach (var field in queryDesc.StatsFields)
                {
                    paramList.Add(new KeyValuePair<string, string>("stats.field", field));
                }
            }


            return paramList;
        }

        public SolrQueryResult<TDoc> QueryForSpellChecker(SolrQueryDescription<TDoc> queryDesc)
        {
            var conn = new SolrConnection(CoreInfos.First().Uri);
            var paramList = new List<KeyValuePair<string, string>>();

            var queryString = queryDesc.Query;
            
            foreach (var sParam in queryDesc.SolrParameters)
            {
                paramList.Add(new KeyValuePair<string, string>(sParam.Key, sParam.Value));
            }
            
            var stream = conn.Post("spell", paramList);
            return _serializer.Deserialize(stream, queryDesc);
        }

        public string GetFieldName(Expression<Func<TDoc, object>> field, string dynamicKey)
        {
            var expression = field.Body as MemberExpression;
            if (expression == null)
            {
                if (field.Body is UnaryExpression)
                {
                    expression = ((UnaryExpression)field.Body).Operand as MemberExpression;
                }
                else
                {
                    return null;
                }
            }
            var name = expression.Member.Name;
            if (DocFields.Any(x => x.Value.Name == name))
            {
                name = DocFields.Single(x => x.Value.Name == name).Key;
                if (!string.IsNullOrEmpty(dynamicKey))
                {
                    return name.Replace("*", dynamicKey);
                }
            }
            return name;
        }

        public bool IsMultiValued(Expression<Func<TDoc, object>> field)
        {
            bool isMultiValued = false;

            var expression = field.Body as MemberExpression;
            if (expression == null)
            {
                if (field.Body is UnaryExpression)
                {
                    expression = ((UnaryExpression)field.Body).Operand as MemberExpression;
                }
                else
                {
                    return false;
                }
            }
            var attrs = expression.Member.GetCustomAttributes(typeof(SolrFieldAttribute),false);

            

            try
            {
                if (attrs != null && attrs.Count() > 0) isMultiValued = true;
            }
            catch
            {
            }

            return isMultiValued;
        }

        private readonly Dictionary<string, string> _fieldCache = new Dictionary<string, string>(); 

        public string GetFieldName(Expression<Func<TDoc, object>> field)
        {
            string back;
            if(_fieldCache.TryGetValue(field.ToString(), out back))
            {
                return back;
            }

            string name = GetFieldName(field, null);
            _fieldCache.Add(field.ToString(), name);

            return name;
        }

        public bool IsStoredField(PropertyInfo prop)
        {
            var attr = prop.GetCustomAttributes(typeof(SolrFieldAttribute), true).FirstOrDefault() as SolrFieldAttribute;
            if (attr == null || !attr.Stored)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Ermitteln der Felder des angegebenen DocTypes
        /// </summary>
        /// <returns></returns>
        private IDictionary<string, PropertyInfo> DetermineDocFields()
        {
            var docFields = new Dictionary<string, PropertyInfo>();
            var props = typeof(TDoc).GetProperties();
            foreach (var prop in props)
            {
                var attr = prop.GetCustomAttributes(typeof(SolrFieldAttribute), true).FirstOrDefault() as SolrFieldAttribute;
                if (attr != null)
                {
                    docFields.Add(attr.Name, prop);
                }
            }
            return docFields;
        }

        /// <summary>
        /// Ermitteln der zum Index zugehörigen Cores. Bei mehreren Cores wird automatisch Sharding verwendet.
        /// </summary>
        /// <returns></returns>
        private bool DetermineCoreInfos()
        {
            CoreInfos = new List<SolrCoreInfo>();
            var xdoc = new XmlDocument();
            var webRequest = WebRequest.Create(SolrConnection.CombineUri(SolrBaseUri, new Uri("admin/cores?action=STATUS", UriKind.Relative), null));

            var response = webRequest.GetResponse();
            using (var responseStream = response.GetResponseStream())
            {
                using (var sr = new StreamReader(responseStream))
                {
                    xdoc.LoadXml(sr.ReadToEnd());
                }
            }
            foreach (XmlNode node in xdoc.SelectNodes("/response/lst[@name='status']/lst"))
            {
                var name = node.SelectSingleNode("@name").Value;
                var coreInfo = new SolrCoreInfo(new Uri(SolrBaseUri, name + "/"), _serializer, this);
                if (string.IsNullOrEmpty(name))
                {
                    coreInfo = new SolrCoreInfo(SolrBaseUri, _serializer, this);
                    name = "Main";
                }
                coreInfo.Name = name;
                coreInfo.InstanceDir = node.SelectSingleNode("str[@name='instanceDir']").InnerText;
                coreInfo.DataDir = node.SelectSingleNode("str[@name='dataDir']").InnerText;
                coreInfo.NumDocs = int.Parse(node.SelectSingleNode("lst[@name='index']/int[@name='numDocs']").InnerText);
                CoreInfos.Add(coreInfo);
            }

            return CoreInfos.Count > 0;

        }

        public  IDictionary<string, PropertyInfo> DocFields
        {
            get;
            private set;
        }


        public SolrQueryResult<TDoc> QueryWithFailOver(SolrQueryDescription<TDoc> queryDesc)
        {
            var relativeUri = "select";

            var conn = new SolrConnection(CoreInfos.First().Uri);
            var parameterList = GenerateSolrParameterList(conn, queryDesc);

            var paramString = string.Join("&", parameterList.Select(x => string.Format("{0}={1}", x.Key, HttpUtility.UrlEncode(x.Value))));
            var Uri = string.Format("{0}{1}?{2}", CoreInfos.First().Uri, relativeUri, paramString);


            try
            {
                var stream = conn.PostWithFailOver(relativeUri, parameterList);

                if (SolrDeserialisiereOhneReflection)
                {
                    return _serializer.DeserializeDirect(stream, queryDesc);
                }

                return _serializer.Deserialize(stream, queryDesc);
            }
            catch (WebException ex)
            {
                if (CatchedErrorCode.Count > 0 && CatchedErrorCode.Contains(Convert.ToInt32(ex.Status)))
                {

                    IsPrimaryUriActive = false;
                    SwitchServer();

                    //Start timer
                    StartFailOverTimer();

                    //Send the request agin with the secondary server infos 
                    conn = new SolrConnection(CoreInfos.First().Uri);
                    var stream = conn.PostWithFailOver(relativeUri, parameterList);
                    
                    if (SolrDeserialisiereOhneReflection)
                    {
                        _serializer.DeserializeDirect(stream, queryDesc);
                    }

                    return _serializer.Deserialize(stream, queryDesc);
                }
                else
                {
                    throw;
                }

            }
            catch (Exception ex)
            {
                throw;
            }


        }

        private List<SolrCoreInfo> DetermineCoreInfos(Uri solrBaseUri)
        {

            try
            {
                var coreInfos = new List<SolrCoreInfo>();

                var xdoc = new XmlDocument();
                var webRequest = WebRequest.Create(SolrConnection.CombineUri(solrBaseUri, new Uri("admin/cores?action=STATUS", UriKind.Relative), null));

                var response = webRequest.GetResponse();
                using (var responseStream = response.GetResponseStream())
                {
                    using (var sr = new StreamReader(responseStream))
                    {
                        xdoc.LoadXml(sr.ReadToEnd());
                    }
                }
                foreach (XmlNode node in xdoc.SelectNodes("/response/lst[@name='status']/lst"))
                {
                    var name = node.SelectSingleNode("@name").Value;
                    var coreInfo = new SolrCoreInfo(new Uri(solrBaseUri, name + "/"), _serializer, this);
                    if (string.IsNullOrEmpty(name))
                    {
                        coreInfo = new SolrCoreInfo(solrBaseUri, _serializer, this);
                        name = "Main";
                    }
                    coreInfo.Name = name;
                    coreInfo.InstanceDir = node.SelectSingleNode("str[@name='instanceDir']").InnerText;
                    coreInfo.DataDir = node.SelectSingleNode("str[@name='dataDir']").InnerText;
                    coreInfo.NumDocs = int.Parse(node.SelectSingleNode("lst[@name='index']/int[@name='numDocs']").InnerText);
                    coreInfos.Add(coreInfo);

                }
                return coreInfos;
            }
            catch (WebException ex)
            {
                StartFailOverTimer();
                return null;
            }
            catch (Exception ex)
            {
                throw;
            }


        }

        /// <summary>
        /// Switch the main core with secondary core or vice versa.
        /// </summary>
        private void SwitchServer()
        {
            CoreInfos = IsPrimaryUriActive ? PrimaryCoreInfos : SecondaryCoreInfos;
        }

        /// <summary>
        /// Start the timer for callback method "OnFailOverEvent"
        /// </summary>
        private void StartFailOverTimer()
        {
            _Timer = new Timer(OnFailOverEvent);
            _Timer.Change(TimerInterval, TimerInterval);
            //Debug.WriteLine("1) FailOverTimer wurde gestartet!");
        }

        /// <summary>
        /// Eventhandler for the checking of the main server availabilty
        /// </summary>
        /// <param name="state"></param>
        private void OnFailOverEvent(object state)
        {
            var pingResult = Ping(SolrPrimaryUri);
            if (pingResult)
            {
                _Timer.Change(Timeout.Infinite, Timeout.Infinite);

                if (PrimaryCoreInfos == null || (PrimaryCoreInfos != null && PrimaryCoreInfos.Count == 0))
                {
                    PrimaryCoreInfos = DetermineCoreInfos(SolrPrimaryUri);
                    //Debug.WriteLine("2) Der Primärserver ist wieder da!");
                }

                if (!IsPrimaryUriActive)
                {
                    IsPrimaryUriActive = true;
                    SwitchServer();
                    //Debug.WriteLine("3) Der Primärserver wurde zurückgesetzt!"); //Email??
                }
            }

            //Debug.WriteLine("4) FailOverEvent wurde ausgeführt!");

        }

        /// <summary>
        /// Get the list of catched erro from the configuration file .config
        /// </summary>
        /// <returns>List of errorcodes</returns>
        private List<int> GetCatchedErrorCode()
        {
            var errorCodesString = "2,7,14";
            if (string.IsNullOrEmpty(errorCodesString)) return new List<int>();
            try
            {
                return  errorCodesString.Trim(',').Split(',').Select(Int32.Parse).ToList();
            }
            catch (Exception)
            {
                return new List<int>();
            }
        }

        /// <summary>
        /// Execute a ping request to the solr server
        /// </summary>
        /// <param name="solrBaseUri">solr server</param>
        /// <returns>ture when the server is available</returns>
        public bool Ping(Uri solrBaseUri)
        {
            try
            {
                var conn = new SolrConnection(solrBaseUri);
                var result = conn.GetXmlResult("admin/ping", new Dictionary<string, string>());

                var responseHeader = HeaderResponseParser.Parse(result);

                return responseHeader != null && responseHeader.Status == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
