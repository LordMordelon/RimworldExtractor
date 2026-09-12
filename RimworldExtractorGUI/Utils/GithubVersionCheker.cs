using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RimworldExtractorGUI
{
    internal static class GithubVersionCheker
    {
        // La misma que usa el actualizador para armar las URL de descarga. Se toma de ahi y no
        // se repite: dos copias de la direccion del repositorio se desincronizan solas.
        private const string RepoUrl = RimworldExtractorInternal.Actualizador.RepoUrl;

        internal static readonly string ReleasesUrl = $"{RepoUrl}/releases";

        // Upstream armaba esta URL con Path.Combine, que en Windows mete una barra
        // invertida. Funcionaba de casualidad porque solo se lee el redirect.
        internal static readonly string LatestUrl = $"{ReleasesUrl}/latest";

        internal static readonly string IssueUrl = $"{RepoUrl}/issues/new/choose";

        internal static readonly string DiscussionUrl = $"{RepoUrl}/discussions";

        public static string GetLatest()
        {
            using var hc = new HttpClient(new HttpClientHandler(){AllowAutoRedirect = false});
            var response = hc.GetAsync(LatestUrl).Result;

            if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently)
            {
                var redirectedUrl = response.Headers.Location;
                return redirectedUrl?.AbsolutePath.Split('/').Last() ?? throw new WebException("RedirectedUrl was null.");
            }

            throw new WebException("HttpClient got no response.");
        }
    }
}
