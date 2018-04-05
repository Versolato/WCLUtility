using System;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

namespace Negri.Wot.Tanks
{
    /// <summary>
    ///     Um tanque jogado
    /// </summary>
    public class TankPlayer
    {
        public const string TankHeader =
            "Gamer Tag,Clan Tag,Original Line Number,Clan Id, Player Id,Player Moment,Tank,Type,TankId,Battles,Last Battle,Time (minutes),WN8,Damage,Win Rate,Kills,Spotted,Defense,AssistedDamage,Shots,Hits,Piercings,DPM,Survival Rate,Kills/Deaths,Blocked Hits %";


        public TankPlayer()
        {
            Moment = DateTime.UtcNow;
        }

        /// <summary>
        ///     Momento (UTC) da captura de dados
        /// </summary>
        public DateTime Moment { get; set; }

        /// <summary>
        ///     A data a que se referem esses dados (Data da ultima batalha)
        /// </summary>
        public DateTime Date => LastBattle.Date;

        [JsonProperty("account_id")]
        public long PlayerId { get; set; }

        [JsonProperty("tank_id")]
        public long TankId { get; set; }

        [JsonProperty("last_battle_time")]
        public long LastBattleUnix { get; set; }

        /// <summary>
        ///     Quando foi feita a ultima batalha
        /// </summary>
        public DateTime LastBattle => LastBattleUnix.ToDateTime();

        [JsonProperty("trees_cut")]
        public long TreesCut { get; set; }

        [JsonProperty("max_frags")]
        public long MaxFrags { get; set; }

        [JsonProperty("mark_of_mastery")]
        public long MarkOfMastery { get; set; }

        [JsonProperty("battle_life_time")]
        public long BattleLifeTimeSeconds { get; set; }

        /// <summary>
        ///     Tempo jogado com esse tanque
        /// </summary>
        public TimeSpan BattleLifeTime => TimeSpan.FromSeconds(BattleLifeTimeSeconds);

        /// <summary>
        ///     Todos os detalhes de jogadas com esse tanque
        /// </summary>
        [JsonProperty("all")]
        public TankPlayerStatistics All { get; set; }

        /// <summary>
        ///     Retuns the tank as a CSV line
        /// </summary>
        /// <param name="wn8ExpectedValues">To lookup tank name and expected values</param>
        public string ToString(Wn8ExpectedValues wn8ExpectedValues)
        {
            var sb = new StringBuilder();

            var e = wn8ExpectedValues[TankId];
            Debug.Assert(e != null, "A refernce tank must exist!");

            sb.Append(e.Name.SanitizeToCsv());
            sb.Append(",");

            sb.Append(e.TypeName.SanitizeToCsv());
            sb.Append(",");

            sb.Append(TankId);
            sb.Append(",");

            sb.Append(All.Battles);
            sb.Append(",");

            sb.Append(LastBattle.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.Append(",");

            sb.Append(BattleLifeTime.TotalMinutes);
            sb.Append(",");
            
            double wn8 = wn8ExpectedValues.CalculateWn8(TankId, All);
            sb.Append(wn8);
            sb.Append(",");

            sb.Append(All.DamageDealt * 1.0 / All.Battles);
            sb.Append(",");

            sb.Append(All.Wins * 1.0 / All.Battles);
            sb.Append(",");

            sb.Append(All.Kills * 1.0 / All.Battles);
            sb.Append(",");

            sb.Append(All.Spotted * 1.0 / All.Battles);
            sb.Append(",");

            sb.Append(All.DroppedCapturePoints * 1.0 / All.Battles);
            sb.Append(",");

            sb.Append(All.DroppedCapturePoints * 1.0 / All.Battles);
            sb.Append(",");

            sb.Append(All.DamageAssisted * 1.0 / All.Battles);
            sb.Append(",");

            sb.Append(All.Shots * 1.0 / All.Battles);
            sb.Append(",");

            sb.Append(All.Hits * 1.0 / All.Battles);
            sb.Append(",");

            sb.Append(All.Piercings * 1.0 / All.Battles);
            sb.Append(",");

            sb.Append(All.Piercings * 1.0 / All.Battles);
            sb.Append(",");

            sb.Append(All.DamageDealt * 1.0 / BattleLifeTime.TotalMinutes);
            sb.Append(",");

            sb.Append(All.SurvivedBattles * 1.0 / All.Battles);
            sb.Append(",");

            if (All.Deaths > 0)
            {
                sb.Append(All.Kills * 1.0 / All.Deaths);
            }

            sb.Append(",");

            if (All.DirectHitsReceived > 0)
            {
                sb.Append(All.NoDamageDirectHitsReceived * 1.0 / All.DirectHitsReceived);
            }

            return sb.ToString();
        }
    }
}