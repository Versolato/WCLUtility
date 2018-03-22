using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Negri.Wcl.Api;

namespace Negri.Wcl
{
    /// <summary>
    /// A raw record, from chalonge
    /// </summary>
    internal class RawRecord
    {
        #region Original Fields

        /// <summary>
        /// Clan Tag
        /// </summary>
        public string TeamName { get; set; }

        /// <summary>
        /// Gamer tag
        /// </summary>
        public string InGameName { get; set; }

        /// <summary>
        /// Team Pretty name
        /// </summary>
        public string TeamLongName { get; set; }

        /// <summary>
        /// Parent Clan Tag
        /// </summary>
        public string ParentClanTag { get; set; }

        /// <summary>
        /// URL of the clan on the WG site.
        /// </summary>
        public string ClanMembershipUrl { get; set; }

        /// <summary>
        /// The team contact gamer tag
        /// </summary>
        public string TeamContactGamerTag { get; set; }

        /// <summary>
        /// Team contact mail
        /// </summary>
        public string TeamContactMail { get; set; }

        /// <summary>
        /// The preferred Server
        /// </summary>
        public string PreferredServer { get; set; }

        /// <summary>
        /// The backup Server
        /// </summary>
        public string BackupServer { get; set; }

        /// <summary>
        /// Participants on the top 32 last season
        /// </summary>
        public string[] OnLastSeasonTop32 { get; set; } = new string[0];

        #endregion

        /// <summary>
        /// The original line for the record
        /// </summary>
        public int OriginalLine { get; set; }

        /// <summary>
        /// If the record is valid (only the structure)
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// The clan tag extracted from the ClanMembershipUrl
        /// </summary>
        public string ClanTagFromUrl { get; set; }

        /// <summary>
        /// Regex for clan tags
        /// </summary>
        private static readonly Regex ClanTagRegex = new Regex("^[A-Z0-9\\-_]{2,5}$", RegexOptions.Compiled);

        /// <summary>
        /// Regex for GamerTags
        /// </summary>
        private static readonly Regex GamerTagRegex = new Regex("^[a-z0-9\\-_\\s]{1,25}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Regex for Clan URL
        /// </summary>
        private static readonly Regex ClanUrlRegex = new Regex("^(http://|https://)?console.worldoftanks(.com)?/[\\w-]+/clan[s]?/xbox/(?<clanTag>[A-Z0-9\\-_]{2,5})(/)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Validates the record (only internal coerence)
        /// </summary>        
        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(TeamName))
            {
                InvalidReason = "teamName (field 1) is empty.";
                IsValid = false;
                return false;
            }

            if (!ClanTagRegex.IsMatch(TeamName))
            {
                InvalidReason = $"teamName (field 1, '{TeamName}') is invalid.";
                IsValid = false;
                return false;
            }

            if (string.IsNullOrWhiteSpace(InGameName))
            {
                InvalidReason = "inGameName (field 2) is empty.";
                IsValid = false;
                return false;
            }

            if (!GamerTagRegex.IsMatch(InGameName))
            {
                InvalidReason = $"inGameName (field 2, '{InGameName}') is invalid.";
                IsValid = false;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(TeamLongName))
            {
                // Rules?
            }

            if (!string.IsNullOrWhiteSpace(ParentClanTag))
            {
                if ((ParentClanTag == "N/A") || (ParentClanTag == "N.A.") || (ParentClanTag == "NÃO"))
                {
                    ParentClanTag = string.Empty;
                }
                else if (!ClanTagRegex.IsMatch(ParentClanTag))
                {
                    InvalidReason = $"Parent Clan (field 4, '{ParentClanTag}') is invalid.";
                    IsValid = false;
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(ClanMembershipUrl))
            {
                var match = ClanUrlRegex.Match(ClanMembershipUrl);
                if (!match.Success)
                {
                    InvalidReason = $"Clan URL (field 5, '{ClanMembershipUrl}') is invalid.";
                    IsValid = false;
                    return false;
                }

                ClanTagFromUrl = match.Groups["clanTag"].Value.ToUpperInvariant();
            }

            if (!string.IsNullOrWhiteSpace(TeamContactGamerTag))
            {
                if (!GamerTagRegex.IsMatch(TeamContactGamerTag))
                {
                    InvalidReason = $"Team Contact Gamer Tag (field 6, '{TeamContactGamerTag}') is invalid.";
                    IsValid = false;
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(TeamContactMail))
            {
                try
                {
                    TeamContactMailAddress = new MailAddress(TeamContactMail);
                }
                catch (Exception)
                {
                    InvalidReason = $"Team Contact Gamer E-Mail (field 7, '{TeamContactMail}') is invalid.";
                    IsValid = false;
                    return false;
                }
            }

            if (OnLastSeasonTop32.Length > 0)
            {
                for (int i = 0; i < OnLastSeasonTop32.Length; i++)
                {
                    var gt = OnLastSeasonTop32[i];
                    if (!GamerTagRegex.IsMatch(gt))
                    {
                        InvalidReason = $"Players on last season top 21 (field 10, Gamer Tag {i + 1}, '{gt}') is invalid.";
                        IsValid = false;
                        return false;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(PreferredServer))
            {
                var location = Parse(PreferredServer);
                if (location == null)
                {
                    InvalidReason = $"Could not detect the preferred server on '{PreferredServer}'";
                    IsValid = false;
                    return false;
                }

                PreferredServerLocation = location;             
            }

            if (!string.IsNullOrWhiteSpace(BackupServer))
            {
                var location = Parse(BackupServer);
                if (location == null)
                {
                    InvalidReason = $"Could not detect the backup server on '{BackupServer}'";
                    IsValid = false;
                    return false;
                }

                BackupServerLocation = location;                
            }

            return true;
        }

        private static ServerLocation? Parse(string server)
        {
            if (string.IsNullOrWhiteSpace(server))
            {
                return ServerLocation.NoPreference;
            }

            server = server.ToLowerInvariant();

            if (server.Contains("west"))
            {
                return ServerLocation.West;
            }
            if (server.Contains("oeste"))
            {
                return ServerLocation.West;
            }

            if (server.Contains("east"))
            {
                return ServerLocation.East;
            }
            if (server.Contains("leste"))
            {
                return ServerLocation.East;
            }

            if (server.Contains("america"))
            {
                return ServerLocation.East;
            }

            if (server.Contains("euro"))
            {
                return ServerLocation.Euro;
            }

            if (server.Contains("eu"))
            {
                return ServerLocation.Euro;
            }

            if (server.Contains("est"))
            {
                return ServerLocation.East;
            }

            if (server.Contains("NAE"))
            {
                return ServerLocation.East;
            }

            if (server.Contains("NAW"))
            {
                return ServerLocation.West;
            }

            return null;
        }

        /// <summary>
        /// The Preferred Server Location
        /// </summary>
        public ServerLocation? PreferredServerLocation { get; set; }

        /// <summary>
        /// The Backup Server Location
        /// </summary>
        public ServerLocation? BackupServerLocation { get; set; }

        /// <summary>
        /// The validated Team Contact Mail Address
        /// </summary>
        public MailAddress TeamContactMailAddress { get; set; }

        /// <summary>
        /// The reason to invalidate the record
        /// </summary>
        public string InvalidReason { get; set; } = string.Empty;

        /// <summary>
        /// Clan Id
        /// </summary>
        public long? ClanId { get; set; }

        /// <summary>
        /// The parent clan id
        /// </summary>
        public long? ParentClanId { get; set; }

        /// <summary>
        /// The player related to the InGameName
        /// </summary>
        public Player Player { get; set; }

        /// <summary>
        /// The Player Id
        /// </summary>
        public long? InGameId => Player?.Id;

        /// <summary>
        /// Players that was on last season top 32 teans
        /// </summary>
        public List<Player> OnLastSeasonTop32Players { get; set; } = new List<Player>();

        /// <summary>
        /// The tean contact player
        /// </summary>
        public Player TeamContactPlayer { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder(1024);

            sb.Append(TeamName);
            sb.Append(",");

            sb.Append(InGameName);
            sb.Append(",");

            if (!string.IsNullOrWhiteSpace(TeamLongName))
            {
                sb.Append($"\"{TeamLongName}\"");
            }
            sb.Append(",");

            if (!string.IsNullOrWhiteSpace(ParentClanTag))
            {
                sb.Append(ParentClanTag);
            }
            sb.Append(",");

            if (!string.IsNullOrWhiteSpace(ClanMembershipUrl))
            {
                sb.Append(ClanMembershipUrl);
            }
            sb.Append(",");

            if (TeamContactPlayer != null)
            {
                sb.Append(TeamContactPlayer.GamerTag);
            }
            sb.Append(",");

            if (TeamContactMailAddress != null)
            {
                sb.Append(TeamContactMailAddress.Address);
            }
            sb.Append(",");

            if (!string.IsNullOrWhiteSpace(PreferredServer))
            {
                sb.Append(PreferredServer);
            }
            sb.Append(",");

            if (!string.IsNullOrWhiteSpace(BackupServer))
            {
                sb.Append(BackupServer);
            }
            sb.Append(",");

            if (OnLastSeasonTop32Players.Any())
            {
                sb.Append("\"");
                sb.Append(string.Join(",", OnLastSeasonTop32Players.Select(p => p.GamerTag)));
                sb.Append("\"");
            }
            sb.Append(",");

            // new fields
            sb.Append(IsValid ? "1" : "0");
            sb.Append(",");

            sb.Append(OriginalLine);
            sb.Append(",");

            if (!string.IsNullOrWhiteSpace(InvalidReason))
            {
                sb.Append($"\"{InvalidReason}\"");
            }
            sb.Append(",");

            if (ClanId.HasValue)
            {
                sb.Append(ClanId.Value);
            }
            sb.Append(",");

            if (ParentClanId.HasValue)
            {
                sb.Append(ParentClanId.Value);
            }
            sb.Append(",");

            if (Player != null)
            {
                sb.Append(Player.Id);
            }
            sb.Append(",");

            if (Player?.CurrentClanId != null)
            {
                sb.Append(Player.CurrentClanId.Value);
            }
            sb.Append(",");

            if (Player?.CurrentClanTag != null)
            {
                sb.Append(Player.CurrentClanTag);
            }
            sb.Append(",");

            if (TeamContactPlayer != null)
            {
                sb.Append(TeamContactPlayer.Id);
            }
            sb.Append(",");

            if (TeamContactPlayer?.CurrentClanId != null)
            {
                sb.Append(TeamContactPlayer.CurrentClanId.Value);
            }
            sb.Append(",");

            if (TeamContactPlayer?.CurrentClanTag != null)
            {
                sb.Append(TeamContactPlayer.CurrentClanTag);
            }
            sb.Append(",");

            if (OnLastSeasonTop32Players.Any())
            {
                sb.Append("\"");
                sb.Append(string.Join(",", OnLastSeasonTop32Players.Select(p => p.Id)));
                sb.Append("\"");
            }
            sb.Append(",");

            if (Player != null)
            {
                sb.Append(Player.Moment.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            sb.Append(",");

            if (PreferredServerLocation != null)
            {
                sb.Append(PreferredServerLocation.Value);
            }
            sb.Append(",");

            if (BackupServerLocation != null)
            {
                sb.Append(BackupServerLocation.Value);
            }
            //sb.Append(",");

            return sb.ToString();
        }

        public const string LineHeader = "teamName,inGameName,Team Name,Parent Clan,Clan Membership Page Link,Team Contact Gamer Tag,Team Contact E-Mail,Preferred Server,Backup Server,On Last Season Top 32 Players,Is Valid,Original Line,Invalid Reason,Clan Id,Parent Clan Id,Player Id,Current Clan Id,Current Clan Tag,Team Contact Player Id,Team Contact Current Clan Id, Team Contact Current Clan Tag,On Last Season Top 32 Players Ids,Player Moment,Preferred Server Code,Backup Server Code";

        public enum ServerLocation
        {
            NoPreference = 0,
            Euro = 1,
            East = 2,
            West = 3            
        }

    }
}