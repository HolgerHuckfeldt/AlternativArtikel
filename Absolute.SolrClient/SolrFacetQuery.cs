using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Absolute.SolrClient
{
    public class SolrFacetQuery
    {
        private SolrFacetQuery() { }

        public SolrFacetQuery(string query)
        {
            Query = query;
        }

        public string Query { get; private set; }
    }
}
