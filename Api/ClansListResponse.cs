using Newtonsoft.Json;

namespace Negri.Wcl.Api
{
    /// <summary>
    /// A resposta da busca por um clã
    /// </summary>
    public class ClansListResponse : ResponseBase
    {
        /// <summary>
        /// Id do Clã (nunca muda)
        /// </summary>
        [JsonProperty("data")]
        public Clan[] Clans { get; set; } = new Clan[0];
    }
}