﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using Negri.Wcl.Api;

namespace Negri.Wcl
{
    /// <summary>
    ///     A raw record, from chalonge
    /// </summary>
    internal class Record
    {
        public enum ServerLocation
        {
            NoPreference = 0,
            Euro = 1,
            East = 2,
            West = 3
        }

        public const string LineHeader =
            "Team Name,Gamer Tag,Country,Clan Tag,Clan Membership Page Link,Team Contact E-Mail,Preferred Server,Alternate Server,Original Line Number,Is Valid,Invalid Reasons,Clan Id,Player Id,Current Clan Id,Current Clan Tag,Player Moment,Preferred Server Code,Alternate Server Code";

        /// <summary>
        ///     Log
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(Record));

        /// <summary>
        ///     Regex for clan tags
        /// </summary>
        private static readonly Regex ClanTagRegex = new Regex("^[A-Z0-9\\-_]{2,5}$", RegexOptions.Compiled);

        /// <summary>
        ///     Regex for GamerTags
        /// </summary>
        private static readonly Regex GamerTagRegex = new Regex("^[a-z0-9\\-_\\s]{1,25}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        ///     Regex for Clan URL
        /// </summary>
        private static readonly Regex ClanUrlRegex =
            new Regex("^(http://|https://)?console.worldoftanks(.com)?/[\\w-]+/clan[s]?/xbox/(?<clanTag>[A-Z0-9\\-_]{2,5})(/)?$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex Splitter = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)", RegexOptions.Compiled | RegexOptions.Singleline);

        private readonly List<string> _invalidReasons = new List<string>();

        /// <summary>
        ///     Call parse to build one
        /// </summary>
        private Record()
        {
        }

        /// <summary>
        ///     The original line for the record
        /// </summary>
        public int OriginalLine { get; private set; }

        /// <summary>
        ///     If the record is valid (only the structure)
        /// </summary>
        public bool IsValid { get; private set; } = true;

        /// <summary>
        ///     The clan tag extracted from the ClanMembershipUrl
        /// </summary>
        public string ClanTagFromUrl { get; set; }

        /// <summary>
        ///     The Preferred Server Location
        /// </summary>
        public ServerLocation? PreferredServerLocation { get; set; }

        /// <summary>
        ///     The Backup Server Location
        /// </summary>
        public ServerLocation? AlternateServerLocation { get; set; }

        /// <summary>
        ///     The validated Team Contact Mail Address
        /// </summary>
        public MailAddress TeamContactMailAddress { get; set; }

        /// <summary>
        ///     The reasons to invalidate the record
        /// </summary>
        public string InvalidReasons
        {
            get
            {
                if (!_invalidReasons.Any())
                {
                    return string.Empty;
                }

                if (_invalidReasons.Count == 1)
                {
                    return _invalidReasons[0];
                }

                return string.Join("; ", _invalidReasons);
            }
        }

        /// <summary>
        ///     Clan Id
        /// </summary>
        public long? ClanId { get; set; }

        /// <summary>
        ///     The player related to the InGameName
        /// </summary>
        public Player Player { get; set; }

        /// <summary>
        ///     The Player Id
        /// </summary>
        public long? PlayerId => Player?.Id;

        /// <summary>
        ///     Validates the record (only internal coerence)
        /// </summary>
        public bool Validate()
        {
            if (!string.IsNullOrWhiteSpace(TeamName))
            {
                AddInvalidReason("Team Name (field 1) is empty.");
            }

            if (string.IsNullOrWhiteSpace(GamerTag))
            {
                AddInvalidReason("Gamer Tag (field 2) is empty.");
            }
            else if (!GamerTagRegex.IsMatch(GamerTag))
            {
                AddInvalidReason($"Gamer Tag (field 2, '{GamerTag}') is invalid.");
            }

            if (string.IsNullOrEmpty(Country))
            {
                // Is it a problem?
                Country = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(ClanTag) && !ClanTagRegex.IsMatch(ClanTag))
            {
                AddInvalidReason($"Clan Tag (field 4, '{ClanTag}') is invalid.");
            }

            if (!string.IsNullOrWhiteSpace(ClanUrl))
            {
                var match = ClanUrlRegex.Match(ClanUrl);
                if (!match.Success)
                {
                    AddInvalidReason($"Clan URL (field 5, '{ClanUrl}') is invalid.");
                }

                ClanTagFromUrl = match.Groups["clanTag"].Value.ToUpperInvariant();
            }

            if (!string.IsNullOrWhiteSpace(PreferredServer))
            {
                var location = ParseServer(PreferredServer);
                if (location == null)
                {
                    AddInvalidReason($"Could not detect the preferred server ('{PreferredServer}') on field 6.");
                }

                PreferredServerLocation = location;
            }

            if (!string.IsNullOrWhiteSpace(AlternateServer))
            {
                var location = ParseServer(AlternateServer);
                if (location == null)
                {
                    AddInvalidReason($"Could not detect the alternate server on '{AlternateServer}' on field 7");
                }

                AlternateServerLocation = location;
            }

            if (!string.IsNullOrWhiteSpace(TeamContactMail))
            {
                try
                {
                    TeamContactMailAddress = new MailAddress(TeamContactMail);
                }
                catch (Exception)
                {
                    AddInvalidReason($"Team Contact Gamer E-Mail (field 8, '{TeamContactMail}') is invalid.");
                }
            }

            return true;
        }

        private static ServerLocation? ParseServer(string server)
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

        public void AddInvalidReason(string reason)
        {
            _invalidReasons.Add(reason);
            IsValid = false;
            Log.Error($"Line {OriginalLine:0000} is invalid. Reason: {reason}");
        }

        private static string SanitizeToCsv(string original)
        {
            if (string.IsNullOrEmpty(original))
            {
                return string.Empty;
            }

            if (original.Contains('"'))
            {
                // replace double quotes with two double quotes
                original = original.Replace("\"", "\"\"");
            }

            if (original.Contains(','))
            {
                original = "\"" + original + "\"";
            }

            return original;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder(1024);

            sb.Append(SanitizeToCsv(TeamName));
            sb.Append(",");

            sb.Append(SanitizeToCsv(GamerTag));
            sb.Append(",");

            if (!string.IsNullOrWhiteSpace(Country))
            {
                sb.Append(SanitizeToCsv(Country));
            }

            sb.Append(",");

            if (!string.IsNullOrWhiteSpace(ClanTag))
            {
                sb.Append(SanitizeToCsv(ClanTag));
            }

            sb.Append(",");

            if (!string.IsNullOrWhiteSpace(ClanUrl))
            {
                sb.Append(SanitizeToCsv(ClanUrl));
            }

            sb.Append(",");

            if (!string.IsNullOrWhiteSpace(PreferredServer))
            {
                sb.Append(SanitizeToCsv(PreferredServer));
            }

            sb.Append(",");

            if (!string.IsNullOrWhiteSpace(AlternateServer))
            {
                sb.Append(SanitizeToCsv(AlternateServer));
            }

            sb.Append(",");

            if (TeamContactMailAddress != null)
            {
                sb.Append(SanitizeToCsv(TeamContactMailAddress.Address));
            }

            sb.Append(",");

            // new fields

            sb.Append(OriginalLine);
            sb.Append(",");

            sb.Append(IsValid ? "1" : "0");
            sb.Append(",");

            if (!IsValid)
            {
                sb.Append(SanitizeToCsv(InvalidReasons));
            }

            sb.Append(",");

            if (ClanId.HasValue)
            {
                sb.Append(ClanId.Value);
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

            if (AlternateServerLocation != null)
            {
                sb.Append(AlternateServerLocation.Value);
            }
            //sb.Append(",");

            return sb.ToString();
        }

        public static Record Parse(string line, int lineNumber)
        {
            var f = Splitter.Split(line);

            if (f.Length < 8)
            {
                Log.Warn($"There are {f.Length} fields when the expected was at least 8");
                return null;
            }

            var r = new Record
            {
                TeamName = f[0].Trim().Trim('"').Trim(),
                GamerTag = f[1].Trim().Trim('"').Trim(),
                Country = f[2].Trim().Trim('"').Trim(),
                ClanTag = f[3].Trim().Trim('"').Trim(),
                ClanUrl = f[4].Trim().Trim('"').Trim(),
                PreferredServer = f[5].Trim().Trim('"').Trim(),
                AlternateServer = f[6].Trim().Trim('"').Trim(),
                TeamContactMail = f[7].Trim().Trim('"').Trim(),
                OriginalLine = lineNumber
            };

            return r;
        }

        #region Original Fields

        /// <summary>
        ///     The Team Name
        /// </summary>
        public string TeamName { get; set; }

        /// <summary>
        ///     Gamer tag
        /// </summary>
        public string GamerTag { get; set; }

        /// <summary>
        ///     The Team "Country"
        /// </summary>
        public string Country { get; set; }

        /// <summary>
        ///     The name of the team
        /// </summary>
        public string ClanTag { get; set; }

        /// <summary>
        ///     URL of the clan on the WG site.
        /// </summary>
        public string ClanUrl { get; set; }

        /// <summary>
        ///     The preferred Server
        /// </summary>
        public string PreferredServer { get; set; }

        /// <summary>
        ///     The backup Server
        /// </summary>
        public string AlternateServer { get; set; }

        /// <summary>
        ///     Team contact mail
        /// </summary>
        public string TeamContactMail { get; set; }

        #endregion
    }
}