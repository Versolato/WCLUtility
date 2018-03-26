using System;

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
    }
}