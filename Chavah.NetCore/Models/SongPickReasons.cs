﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BitShuva.Chavah.Models
{
    public class SongPickReasons
    {
        public SongPickReasons()
        {
        }

        public string SongId { get; set; }
        public LikeLevel Artist { get; set; }
        public LikeLevel Album { get; set; }
        public bool SongThumbedUp { get; set; }
        public LikeLevel Ranking { get; set; }
        public LikeLevel Similar { get; set; }

        /// <summary>
        /// Used when the user requests a particular song, artist, album, or tag.
        /// </summary>
        public SongPick? SoleReason { get; set; }

        public static SongPickReasons FromSoleReason(SongPick playedReason)
        {
            return new SongPickReasons { SoleReason = playedReason };
        }
    }
}