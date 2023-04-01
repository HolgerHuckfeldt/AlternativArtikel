using Absolute.SolrClient;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text;

namespace Pv.Katalog.Services.Solr.Documents
{
    [DataContract]
    public class ArtikelDocument : ISolrDocument
    {
        [SolrField(Name = "Id", Stored = true, Indexed = true)]
        public string Id { get; set; }

        [SolrField(Name = "Artikelnummer", Stored = true, Indexed = true)]
        public string Artikelnummer { get; set; }

        [SolrField(Name = "DLNr", Stored = true, Indexed = true)]
        public int DLNr { get; set; }

        [SolrField(Name = "Tauschartikel_*", Stored = true, Indexed = false)]
        public ICollection<string> Tauschartikel { get; set; }

    }
}
