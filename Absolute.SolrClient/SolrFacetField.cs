using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Absolute.SolrClient
{
    public class SolrFacetField
    {
        public SolrFacetField(string fieldName, string prefix, FacetSort sort = FacetSort.Count, int limit = 100, int minCount = 1, string excludeTag = null, FacetMethod method = FacetMethod.Ohne)
        {
            FieldName = fieldName;
            MinCount = minCount;
            Prefix = prefix;
            Limit = limit;
            Sort = sort;
            ExcludeTag = excludeTag;
            Method = method;
        }

        public string FieldName { get; private set; }
        public int MinCount { get; private set; }
        public int Limit { get; private set; }
        public string Prefix { get; private set; }
        public FacetSort Sort { get; private set; }
        public string ExcludeTag { get; private set; }
        public FacetMethod Method { get; private set; }

        internal IDictionary<string, string> CreateParams()
        {
            var result = new Dictionary<string, string>();
            var field = FieldName;
            if (!string.IsNullOrEmpty(ExcludeTag))
            {
                field = string.Format("{{!ex={0}}}{1}", ExcludeTag, field);
            }
            result.Add("facet.field", field);
            result.Add(string.Format("f.{0}.facet.mincount", FieldName), MinCount.ToString());
            result.Add(string.Format("f.{0}.facet.limit", FieldName), Limit.ToString());
            result.Add(string.Format("f.{0}.facet.sort", FieldName), Sort.ToString().ToLower());
            if (!string.IsNullOrEmpty(Prefix))
            {
                result.Add(string.Format("f.{0}.facet.prefix", FieldName), Prefix);
            }

            if (Method != FacetMethod.Ohne)
            {
                result.Add(string.Format("f.{0}.facet.method", FieldName), Method.ToString().ToLower());
            }

            return result;
        }

        public override string ToString()
        {
            return FieldName;
        }
    }

    public enum FacetSort
    {
        Count,
        Index
    }

    public enum FacetMethod
    {
        Ohne,
        Enum,
        Fc, //facet cache
        Fcs //facet cache per segment
    }
}
