﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Negri.Wot.Api
{
    /// <summary>
    /// Um clã
    /// </summary>
    public class Clan
    {
        private HashSet<long> _membersIds = new HashSet<long>();
        private Dictionary<long, Player> _players = new Dictionary<long, Player>();
        private int _membersCount;

        /// <summary>
        /// Id do Clã (nunca muda)
        /// </summary>
        [JsonProperty("clan_id")]
        public long ClanId { get; set; }

        /// <summary>
        /// Tag do Clã (as vezes muda)
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Nome do Clã (muda com frequencia)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Numero de membros do clã
        /// </summary>
        [JsonProperty("members_count")]
        public int MembersCount
        {
            get => (_players?.Count ?? 0) <= 0 ? _membersCount : _players?.Count ?? 0;
            set => _membersCount = value;
        }

        /// <summary>
        /// Data de criação do clã
        /// </summary>
        [JsonProperty("created_at")]
        public long CreatedAtUnix { get; set; }

        public DateTime CreatedAtUtc => CreatedAtUnix.ToDateTime();

        /// <summary>
        /// Se o clã foi desfeito
        /// </summary>
        [JsonProperty("is_clan_disbanded")]
        public bool IsDisbanded { get; set; }

        /// <summary>
        /// Os membros e seus papeis no clã
        /// </summary>
        public Dictionary<long, Member> Members { get; set; } = new Dictionary<long, Member>();

        [JsonProperty("members_ids")]
        public long[] MembersIds
        {
            get => _membersIds?.ToArray() ?? new long[0];
            set => _membersIds = new HashSet<long>(value ?? new long[0]);
        }

        /// <summary>
        /// All battles
        /// </summary>
        public long AllBattles => _players?.Values.Sum(p => p.Battles) ?? 0;

        /// <summary>
        /// Tier 10 battles
        /// </summary>
        public long Tier10Battles => _players?.Values.Sum(p => p.Tier10Battles) ?? 0;

        /// <summary>
        /// The count of distinct Tier 10 tanks
        /// </summary>
        public long Tier10DistinctTanks => _players?.Values.SelectMany(p => p.Tier10Tanks).Select(t => t.TankId).Distinct().Count() ?? 0;

        /// <summary>
        /// Tier 10 Win Rate
        /// </summary>
        public double Tier10WinRate => _players?.Values.Sum(p => p.Tier10Battles*p.Tier10WinRate) / Tier10Battles ?? 0.0;

        /// <summary>
        /// Tier 10 WN8
        /// </summary>
        public double Tier10Wn8 => _players?.Values.Sum(p => p.Tier10Battles * p.Tier10Wn8) / Tier10Battles ?? 0.0;

        /// <summary>
        /// Add a player id to the clan
        /// </summary>
        /// <param name="playerId"></param>
        /// <returns><c>false</c> if the player was already on the clan</returns>
        public bool AddMember(long playerId)
        {
            if (_membersIds == null)
            {
                _membersIds = new HashSet<long>();
            }

            return _membersIds.Add(playerId);
        }

        /// <summary>
        /// <c>True</c> if the clan has this member
        /// </summary>
        public bool HasMember(long playerId)
        {
            return _membersIds != null && _membersIds.Contains(playerId);
        }

        /// <summary>
        /// Add a player to the clan
        /// </summary>
        /// <param name="player"></param>
        public void AddMember(Player player)
        {
            if (_membersIds == null)
            {
                _membersIds = new HashSet<long>();
            }

            if (_membersIds.Contains(player.Id))
            {
                return;
            }

            if (_players == null)
            {
                _players = new Dictionary<long, Player>();
            }

            _players.Add(player.Id, player);
        }
    }
}