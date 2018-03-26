using Newtonsoft.Json;

namespace Negri.Wot.Tanks
{
    /// <summary>
    /// Estat�sticas b�sicas de um tanque jogado, necess�rias para calcular o WN8
    /// </summary>
    /// <remarks>
    /// Dados que vem da API da WG
    /// </remarks>
    public class TankPlayerWn8Statistics
    {
        [JsonProperty("battles")]
        public long Battles { get; set; }

        [JsonProperty("damage_dealt")]
        public long DamageDealt { get; set; }

        [JsonProperty("wins")]
        public long Wins { get; set; }

        /// <summary>
        /// Numero de Advers�rios destruidos
        /// </summary>
        [JsonProperty("frags")]
        public long Kills { get; set; }

        [JsonProperty("spotted")]
        public long Spotted { get; set; }

        [JsonProperty("dropped_capture_points")]
        public long DroppedCapturePoints { get; set; }
    }
}