﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SearchQueryService.Indexes.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SearchQueryService.Controllers
{
    [ApiController]
    [Route("indexes/{indexName}/docs")]
    public class SearchController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public SearchController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _config = configuration;
        }

        [HttpGet]
        public async Task<object> GetAsync(
            [FromRoute] string indexName,
            [FromQuery(Name = "$top")] int? top,
            [FromQuery(Name = "$skip")] int? skip,
            [FromQuery] string search,
            [FromQuery(Name = "$filter")] string filter,
            //string searchMode = "", TODO find out how to set
            [FromQuery(Name = "$orderby")] string orderBy
        )
        {
            var response = await _httpClient.GetAsync(BuildSearchQuery(indexName, top, skip, search, filter, orderBy));
            dynamic result = JsonConvert.DeserializeObject<SearchResponse>(await response.Content.ReadAsStringAsync());

            return result.Response.Docs;
        }

        private string BuildSearchQuery(string indexName, int? top, int? skip, string search, string filter, string orderBy)
        {
            string searchUrl = _config.GetConnectionString("SolrUri") + indexName + "/select?q=";
            if (!string.IsNullOrEmpty(search))
            {
                searchUrl += search;
            }

            if (top is not null)
            {
                searchUrl += "&rows=" + top;
            }

            if (skip is not null)
            {
                searchUrl += "&start=" + skip;
            }

            if (!string.IsNullOrEmpty(filter))
            {
                searchUrl += "&fq=" + AzToSolrQuery(filter);
            }

            if (!string.IsNullOrEmpty(orderBy))
            {
                searchUrl += "&sort=" + orderBy;
            }

            return searchUrl;
        }

        private string AzToSolrQuery(string filter)
        {
            var replacements = new Dictionary<string, string>()
            {
                { @"(\w+)\s+(ge)\s+([^\s]+)", "$1:[$3 TO *]"},
                { @"(\w+)\s+(gt)\s+([^\s]+)", "$1:{$3 TO *}"},
                { @"(\w+)\s+(le)\s+([^\s]+)", "$1:[* TO $3]"},
                { @"(\w+)\s+(lt)\s+([^\s]+)", "$1:{* TO $3}"},
                { @"(\w+)\s+(ne)", "NOT $1:"}
            };

            foreach (var kv in replacements)
            {
                filter = Regex.Replace(filter, kv.Key, kv.Value);
            }

            StringBuilder sb = new StringBuilder(filter);
            sb.Replace(" eq", ":");
            sb.Replace("and", "AND");
            sb.Replace("or", "OR");
            sb.Replace("not", "NOT");

            return sb.ToString();
        }
    }
}
