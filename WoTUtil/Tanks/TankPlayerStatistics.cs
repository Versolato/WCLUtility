using Newtonsoft.Json;

namespace Negri.Wot.Tanks
{
    /// <inheritdoc />
    /// <summary>
    /// Estatísticas de um tanque jogado
    /// </summary>
    /// <remarks>
    /// Dados que vem da API da WG
    /// </remarks>
    public class TankPlayerStatistics : TankPlayerWn8Statistics
    {
        /// <summary>
        /// Numero de Tiros que penetrou a blindagem
        /// </summary>
        [JsonProperty("piercings_received")]
        public long PiercingsReceived { get; set; }

        /// <summary>
        /// Número de Acertos nos Tanques inimigos
        /// </summary>
        [JsonProperty("hits")]
        public long Hits { get; set; }

        [JsonProperty("damage_assisted_track")]
        public long DamageAssistedTrack { get; set; }

        [JsonProperty("losses")]
        public long Losses { get; set; }

        /// <summary>
        /// Tiros Recebidos que não causaram dano
        /// </summary>
        [JsonProperty("no_damage_direct_hits_received")]
        public long NoDamageDirectHitsReceived { get; set; }

        [JsonProperty("capture_points")]
        public long CapturePoints { get; set; }

        /// <summary>
        /// Dano de Splash causado em inimigos
        /// </summary>
        [JsonProperty("explosion_hits")]
        public long ExplosionHits { get; set; }

        [JsonProperty("damage_received")]
        public long DamageReceived { get; set; }

        /// <summary>
        /// Tiros dados que penetraram
        /// </summary>
        [JsonProperty("piercings")]
        public long Piercings { get; set; }

        /// <summary>
        /// Tiros dados
        /// </summary>
        [JsonProperty("shots")]
        public long Shots { get; set; }

        /// <summary>
        /// Tiros recebidos que causaram dano de splash
        /// </summary>
        [JsonProperty("explosion_hits_received")]
        public long ExplosionHitsReceived { get; set; }

        /// <summary>
        /// Dano de assistência por Radio
        /// </summary>
        [JsonProperty("damage_assisted_radio")]
        public long DamageAssistedRadio { get; set; }

        /// <summary>
        /// XP Base acumulado
        /// </summary>
        [JsonProperty("xp")]
        public long XP { get; set; }

        /// <summary>
        /// Tiros Diretos recebidos
        /// </summary>
        [JsonProperty("direct_hits_received")]
        public long DirectHitsReceived { get; set; }


        [JsonProperty("survived_battles")]
        public long SurvivedBattles { get; set; }

        /// <summary>
        /// Vezes em que morreu na batalha
        /// </summary>
        public long Deaths => Battles - SurvivedBattles;

        /// <summary>
        /// O dano assistido total
        /// </summary>
        public long DamageAssisted => DamageAssistedRadio + DamageAssistedTrack;

        /// <summary>
        /// Dano total causado (para MoE)
        /// </summary>
        public long TotalDamage => DamageAssisted + DamageDealt;

        /// <summary>
        /// Empates
        /// </summary>
        public long Draws => Battles - Wins - Losses;

    }
}