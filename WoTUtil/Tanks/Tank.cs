﻿using Negri.Wot.WgApi;
using Newtonsoft.Json;

namespace Negri.Wot.Tanks
{
    public class Tank
    {
        private string _fullName;

       
        /// <summary>
        ///     Id numerico do tanque
        /// </summary>
        public long TankId { get; set; }

        /// <summary>
        ///     O nome curto, em Inglês, do tanque
        /// </summary>
        public string Name { get; set; }

        public string FullName
        {
            get => _fullName ?? Name;
            set => _fullName = value;
        }

        /// <summary>
        ///     O nome programático do tanque, util para linkar com a tankopedia e obter imagens
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Tier
        /// </summary>
        public int Tier { get; set; }

        /// <summary>
        ///     Tipo do Tanque
        /// </summary>
        public TankType Type { get; set; }

        /// <summary>
        ///     Tipo do nome, em string
        /// </summary>
        public string TypeName => Type.ToString();

        /// <summary>
        ///     Nação do Tanque
        /// </summary>
        public Nation Nation { get; set; }

        /// <summary>
        /// Nação do tanque, string
        /// </summary>
        public string NatioName => Nation.ToString();

        public bool IsPremium { get; set; }

        [JsonIgnore]
        public string PremiumClass => IsPremium ? "is-premium-tank" : null;
    }
}