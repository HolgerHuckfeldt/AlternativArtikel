using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Absolute.SolrClient
{
    public class SolrStatsResult
    {
        internal SolrStatsResult(string fieldName)
        {
            FieldName = fieldName;
        }

        public string FieldName { get; set; }

        /// <summary>
        /// The minimum value in the field if it's a numeric.
        /// </summary>
        public decimal? MinDecimal { get; set; }

        /// <summary>
        /// The maximum value in the field if it's a numeric.
        /// </summary>
        public decimal? MaxDecimal { get; set; }

        /// <summary>
        /// The minimum value in the field if it's a string.
        /// </summary>
        public string MinString { get; set; }

        /// <summary>
        /// The minimum value in the field if it's a string.
        /// </summary>
        public string MaxString { get; set; }

        /// <summary>
        /// The number of non-null values in the field.
        /// </summary>
        public long Count { get; set; }

        /// <summary>
        /// The number of null values in the field.
        /// </summary>
        public long Empty { get; set; }

        /// <summary>
        /// The sum of all values in the field.
        /// </summary>
        public decimal? Sum { get; set; }

        /// <summary>
        /// The average (v1 + v2 .... + vN)/N
        /// </summary>
        public decimal? Average { get; set; }
    }
}
