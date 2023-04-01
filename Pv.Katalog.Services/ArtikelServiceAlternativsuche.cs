using Absolute.SolrClient;
using MoreLinq.Extensions;
using Pv.Katalog.Services.DTOs;
using Pv.Katalog.Services.Solr.Documents;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Pv.Katalog.Services
{
    public class ArtikelServiceAlternativsuche
    {
        private readonly ISolrIndex<ArtikelDocument> _index;

        public ArtikelServiceAlternativsuche(string connectionString, string secondaryConnectionString)
        {
            _index = !string.IsNullOrEmpty(secondaryConnectionString) ? 
                new SolrIndex<ArtikelDocument>(connectionString, secondaryConnectionString, false) : 
                new SolrIndex<ArtikelDocument>(connectionString, false);
        }

        public List<Alternativartikel> GetAlternativartikel(List<Artikelidentifikator> artikelIdentList)
        {
            var result = new List<Alternativartikel>();

            if (artikelIdentList == null || !artikelIdentList.Any())
            {
                return result;
            }

            SolrQueryBuilder<ArtikelDocument> fqBuilder = _index.NewQueryBuilder();
            SolrQueryBuilder<ArtikelDocument> artikelListQBuilder = _index.NewQueryBuilder();

            foreach (Artikelidentifikator ident in artikelIdentList)
            {
                var artikelQBuilder = _index.NewQueryBuilder();
                artikelQBuilder.Expression(x => x.DLNr, ident.DLNr).And().Expression(x => x.Artikelnummer, ident.Artikelnummer);
                artikelListQBuilder.SubQuery(artikelQBuilder).Or();
            }

            fqBuilder.SubQuery(artikelListQBuilder);

            var qDesc = _index.NewQueryDescription();
            qDesc.Query = "*:*";
            qDesc.RowCount = artikelIdentList.Count;
            qDesc.FilterQueries.Add(fqBuilder.Create());
            qDesc.AddDynamicFieldKey(x => x.Tauschartikel, "001004");

            var qResult = _index.QueryWithFailOver(qDesc);

            if (qResult.Documents != null)
            {
                result = Mapper.ArtikelMapper.Map(qResult.Documents).Distinct().ToList();
            }

            return result;
        }
    }
}
