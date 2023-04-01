using Pv.Katalog.Services.DTOs;
using Pv.Katalog.Services.Solr.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MoreLinq;

namespace Pv.Katalog.Services.Mapper
{
    public class ArtikelMapper
    {
        private const char DELIMITER = '¦';
        private static readonly Regex _regexTauschartikel = 
            new Regex(@"^(PV|TECDOC)" + DELIMITER + @"(\d*)" + DELIMITER + @"(\d*)" + DELIMITER + @"(.+)" + DELIMITER + @"(.+)$", RegexOptions.Compiled);

        public static List<Alternativartikel> Map(IEnumerable<ArtikelDocument> docList)
        {
            var result = new List<Alternativartikel>();

            foreach (var doc in docList)
            {
                var artikel = new Alternativartikel();

                artikel.Id = int.Parse(doc.Id); ;
                artikel.DLNr = doc.DLNr;
                artikel.Artikelnummer = doc.Artikelnummer;
                artikel.Tauschartikel = new List<AlternativartikelItem>();

                var tauschArtikelDist = doc.Tauschartikel.Distinct().ToList();
                var tauschArtikel = doc.Tauschartikel.ToList();

                foreach (var item in doc.Tauschartikel)
                {
                    var match = _regexTauschartikel.Match(item);
                    if (!match.Success)
                    {
                        continue;
                    }

                    var addItem = new AlternativartikelItem();
                    addItem.Typ = match.Groups[1].Value;
                    addItem.Id = int.Parse(match.Groups[2].Value);
                    addItem.DLNr = int.Parse(match.Groups[3].Value);
                    addItem.Artikelnummer = match.Groups[4].Value;
                    addItem.Beschreibung = match.Groups[5].Value;
                    artikel.Tauschartikel.Add(addItem);
                }

                artikel.Tauschartikel = artikel.Tauschartikel.DistinctBy(s => new{s.Artikelnummer, s.DLNr}).ToList();

                result.Add(artikel);
            }

            return result.Distinct().ToList();
        }
    }
}
