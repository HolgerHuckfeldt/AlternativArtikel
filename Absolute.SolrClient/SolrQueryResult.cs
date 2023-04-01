using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Absolute.SolrClient
{
    public class SolrQueryResult<TDoc> where TDoc : ISolrDocument
    {
        public SolrQueryResult(SolrQueryDescription<TDoc> queryDesc)
        {
            QueryDescription = queryDesc;
            Documents = new LinkedList<TDoc>();
            FacetFields = new LinkedList<SolrFacetFieldResult>();
            FacetQueries = new Dictionary<string, int>();
            Highlights = new Dictionary<string, IDictionary<string, ICollection<string>>>();
            Statistics = new LinkedList<SolrStatsResult>();
            Groups = new LinkedList<SolrGroupResult<TDoc>>();
            Index = queryDesc.Index;
            ResultOfSpellCheck = new List<SpellcheckResult>();
        }

        public ISolrIndex<TDoc> Index { get; private set; }
        public SolrQueryDescription<TDoc> QueryDescription { get; private set; }
        public int NumFound { get; internal set; }
        public int StartIndex { get; set; }
        public int QueryTime { get; internal set; }

        public ICollection<TDoc> Documents { get; private set; }
        public ICollection<SolrGroupResult<TDoc>> Groups { get; private set; }

        public ICollection<SolrFacetFieldResult> FacetFields { get; private set; }
        public IDictionary<string, int> FacetQueries { get; private set; }
        public IDictionary<string, IDictionary<string, ICollection<string>>> Highlights { get; private set; }
        public ICollection<SolrStatsResult> Statistics { get; private set; }
        public List<SpellcheckResult> ResultOfSpellCheck { get; private set; }
    }
}
