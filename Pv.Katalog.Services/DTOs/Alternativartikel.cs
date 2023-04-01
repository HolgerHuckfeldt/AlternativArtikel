using System;
using System.Collections.Generic;
using System.Text;

namespace Pv.Katalog.Services.DTOs
{
    public class Alternativartikel : Artikelidentifikator
    {
        public List<AlternativartikelItem> Tauschartikel { get; set; }
    }
}
