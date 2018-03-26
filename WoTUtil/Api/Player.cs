using System;
using System.Collections.Generic;
using System.Linq;
using Negri.Wot.Tanks;

namespace Negri.Wot.Api
{
    /// <summary>
    /// A player
    /// </summary>
    public class Player
    {
        /// <summary>
        /// Player Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gamer Tag
        /// </summary>
        public string GamerTag { get; set; }

        /// <summary>
        /// The current clan, if any
        /// </summary>
        public long? CurrentClanId { get; set; }

        /// <summary>
        /// The current clan tag, if any
        /// </summary>
        public string CurrentClanTag { get; set; }

        /// <summary>
        /// Moment data was retrieved
        /// </summary>
        public DateTime Moment { get; set; } = DateTime.UtcNow;

        #region Overall Stats

        /// <summary>
        /// Tanks of the player
        /// </summary>
        public TankPlayer[] Tanks { get; internal set; } = new TankPlayer[0];

        /// <summary>
        /// Total battles
        /// </summary>
        public long Battles { get; private set; }

        /// <summary>
        /// Win Rate
        /// </summary>
        public double WinRate { get; private set; }

        /// <summary>
        /// Average Tier
        /// </summary>
        public double AvgTier { get; private set; }

        /// <summary>
        /// Overall WN8
        /// </summary>
        public double Wn8 { get; private set; }

        #endregion

        #region Tier 10 Stats

        /// <summary>
        /// Tanks of the player
        /// </summary>
        public TankPlayer[] Tier10Tanks { get; internal set; } = new TankPlayer[0];

        /// <summary>
        /// Total battles
        /// </summary>
        public long Tier10Battles { get; private set; }

        /// <summary>
        /// Win Rate
        /// </summary>
        public double Tier10WinRate { get; private set; }
        
        /// <summary>
        /// Overall WN8
        /// </summary>
        public double Tier10Wn8 { get; private set; }

        /// <summary>
        /// Tier 10 direct damage per battle
        /// </summary>
        public double Tier10DirectDamage { get; private set; }

        #endregion

        /// <summary>
        /// Calculate performance metrics on the player
        /// </summary>
        /// <param name="wn8ExpectedValues"></param>
        public void CalculatePerformance(Wn8ExpectedValues wn8ExpectedValues)
        {
            if (Tanks.Length == 0)
            {
                return;
            }

            // Only tanks that I know about
            Tanks = Tanks.Where(t => wn8ExpectedValues.Exists(t.TankId) && t.All.Battles > 0).ToArray();

            if (Tanks.Length == 0)
            {
                return;
            }

            Battles = Tanks.Sum(t => t.All.Battles);
            WinRate = Tanks.Sum(t => t.All.Wins) * 1.0 / Battles;
            AvgTier = Tanks.Sum(t => wn8ExpectedValues[t.TankId].Tier * t.All.Battles) * 1.0 / Battles;
            Wn8 = wn8ExpectedValues.CalculateWn8(Tanks.ToDictionary(t => t.TankId, t => (TankPlayerWn8Statistics)t.All));

            Tier10Tanks = Tanks.Where(t => wn8ExpectedValues[t.TankId].Tier == 10).ToArray();

            if (Tier10Tanks.Length == 0)
            {
                return;
            }

            Tier10Battles = Tier10Tanks.Sum(t => t.All.Battles);
            Tier10WinRate = Tier10Tanks.Sum(t => t.All.Wins) * 1.0 / Tier10Battles;
            Tier10Wn8 = wn8ExpectedValues.CalculateWn8(Tier10Tanks.ToDictionary(t => t.TankId, t => (TankPlayerWn8Statistics)t.All));
            Tier10DirectDamage = Tier10Tanks.Sum(t => t.All.DamageDealt) * 1.0 / Tier10Battles;
        }
    }
}