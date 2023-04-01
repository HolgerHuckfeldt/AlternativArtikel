using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Absolute.SolrClient
{
    public class SolrFacetFieldResult
    {
        internal SolrFacetFieldResult(SolrFacetField facetField)
        {
            FacetField = facetField;
            Counts = new Dictionary<string, int>();
        }

        public SolrFacetField FacetField { get; private set; }
        public IDictionary<string, int> Counts { get; private set; }
        public string FieldName
        {
            get { return FacetField.FieldName; }
        }
    }
}
