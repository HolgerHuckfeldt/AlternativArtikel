using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace Absolute.SolrClient
{
    public class SolrCoreInfo
    {
        public SolrCoreInfo(Uri uri, ISolrSerializer serializer, ISolrIndex index)
        {
            Index = index;
            Uri = uri;
            Connection = new SolrConnection(uri);
            Commands = new SolrCommands(Connection);
            _serializer = serializer;
        }
        public string Name { get; set; }
        public string InstanceDir { get; set; }
        public string DataDir { get; set; }
        public int NumDocs { get; set; }
        public Uri Uri { get; set; }
        public SolrCommands Commands { get; set; }
        public SolrConnection Connection { get; set; }
        public ISolrIndex Index { get; private set; }
        private ISolrSerializer _serializer;

        public bool AddRange(Stream documents, bool commit = true)
        {
            var result = Commands.Update(this, documents);
            if (commit)
            {
                Commands.Commit();
            }
            return result;
        }

        public int SaveRange(ICollection<ISolrDocument> documents, Stream target)
        {
            using (var sw = new StreamWriter(target, Encoding.UTF8))
            {
                var result = 0;
                sw.WriteLine("<add>");
                foreach (var doc in documents)
                {
                    _serializer.Serialize(doc, sw);
                    result++;
                    if (target.Position > 512 * 1024 * 1024)
                    {
                        break;
                    }
                }
                sw.WriteLine("</add>");
                sw.Close();
                return result;
            }
        }


    }
}
