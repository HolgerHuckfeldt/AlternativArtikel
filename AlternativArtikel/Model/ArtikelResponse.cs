using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlternativArtikel.Model
{
    public class ArtikelResponse
    {
        public string ArtikelNumber { get; set; }

        public int Dlnr { get; set; }

        public List<ArtikelInfo> Result { get; set; }
    }
}
