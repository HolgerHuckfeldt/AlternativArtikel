using AlternativArtikel.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pv.Katalog.Services;
using Pv.Katalog.Services.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AlternativArtikel.Configuration;

namespace AlternativArtikel.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AlternativArtikelController : ControllerBase
    {
        private string Url1;
        private string Url2;

        public AlternativArtikelController()
        {
            Url1 = MyConfiguration.Url1;
            Url2 = MyConfiguration.Url2;
        }

        [HttpPost("Alternative")]
        [ProducesResponseType(typeof(List<ArtikelResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<ArtikelResponse>>> ArtikelAnfrage([FromBody] List<ArtikelInfo> request)
        {
            try
            {
                ArtikelServiceAlternativsuche artikelServiceAlternativsuche = new ArtikelServiceAlternativsuche(Url1, Url2);

                if (request == null)
                    return BadRequest();

                if (!request.Any())
                    return BadRequest();

                var result = new List<ArtikelResponse>();

                foreach (var artikelInfo in request)
                {

                    var artikelResponseItem = new ArtikelResponse()
                    {
                        ArtikelNumber = artikelInfo.ArtikelNumber,
                        Dlnr = artikelInfo.Dlnr,
                        Result = new List<ArtikelInfo>()
                    };
                    result.Add(artikelResponseItem);

                    var solrRequest = new List<Artikelidentifikator>()
                    {
                        new Artikelidentifikator
                        {
                            DLNr = artikelInfo.Dlnr,
                            Artikelnummer = '"'+artikelInfo.ArtikelNumber+'"'
                        }
                    };

                    var solrResults = artikelServiceAlternativsuche.GetAlternativartikel(solrRequest);

                    if (solrResults != null && solrResults.Any())
                    {
                        var myResult = solrResults.FirstOrDefault();
                        if (myResult?.Tauschartikel != null)
                        {
                            foreach (var solrResult in myResult.Tauschartikel)
                            {

                                artikelResponseItem.Result.Add(new ArtikelInfo()
                                {
                                    ArtikelNumber = solrResult.Artikelnummer,
                                    Beschreibung = solrResult.Beschreibung,
                                    Dlnr = solrResult.DLNr
                                });
                            }
                        }
                    }
                }

                if (!result.Any())
                    return NotFound();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
