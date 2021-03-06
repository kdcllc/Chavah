﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BitShuva.Chavah.Common;
using BitShuva.Chavah.Models;
using BitShuva.Chavah.Models.Indexes;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;

namespace BitShuva.Chavah.Controllers
{
    public class StreamController : RavenController
    {
        public StreamController(
            IAsyncDocumentSession dbSession,
            ILogger<StreamController> logger)
            : base(dbSession, logger)
        {
        }

        /// <summary>
        /// Returns an M3U file. Used for streaming services such as TuneIn radio.
        /// </summary>
        /// <returns></returns>
        public IActionResult TuneInV4()
        {
            // The M3U file will contain a single URL:
            // The URL to our GetNextSong() action.
            // That method will intelligently pick a song.

            // Build the M3U file.
            // M3U format is very simple: https://en.wikipedia.org/wiki/M3U
            var m3uBuilder = new StringBuilder();
            m3uBuilder.AppendLine("# EXTM3U"); // The header

            // Normally we could append just a single URL, but some apps (like Roku) only play 1 song then. We're fixing that here by appending many songs.
            var getNextSongUrl = Url.Action(nameof(GetNextSong), "Stream", null, Request.Scheme);
            for (var i = 0; i < 100; i++)
            {
                m3uBuilder.AppendLine(getNextSongUrl + "?i=" + i.ToString());
            }

            var m3uBytes = Encoding.UTF8.GetBytes(m3uBuilder.ToString());
            return File(m3uBytes, "application/vnd.apple.mpegurl", "ChavahTuneInStream.m3u");
        }

        /// <summary>
        /// Returns a PLS file format for streaming playlists, used for stations like TuneIn Radio.
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> TuneInV3()
        {
            // Build the PLS file.
            // PLS format is very simple: https://en.wikipedia.org/wiki/PLS_(file_format)
            var plsBuilder = new StringBuilder();
            plsBuilder.AppendLine("[playlist]"); // The header

            var songsWithRanking = await DbSession.Query<Song, Songs_RankStandings>()
                    .As<Songs_RankStandings.Result>()
                    .ToListAsync();
            var userPrefs = new UserSongPreferences();
            const int totalSongCount = 25;
            var songPicks = Enumerable
                .Range(0, totalSongCount)
                .Select(s => userPrefs.PickSong(songsWithRanking))
                .ToList();
            var songs = await DbSession.LoadWithoutNulls<Song>(songPicks.Select(p => p.SongId));
            for (var i = 0; i < songs.Count; i++)
            {
                var oneBasedIndex = i + 1;

                // File1=http://thefile.mp3
                plsBuilder.AppendLine();
                plsBuilder.Append("File");
                plsBuilder.Append(oneBasedIndex);
                plsBuilder.Append('=');
                plsBuilder.AppendLine(songs[i].Uri.ToString());

                // Title1=Shema Yisrael by Barry & Batya Segal
                plsBuilder.Append("Title");
                plsBuilder.Append(oneBasedIndex);
                plsBuilder.Append('=');
                var songName = string.IsNullOrEmpty(songs[i].HebrewName) ? songs[i].Name : songs[i].Name + " " + songs[i].HebrewName;
                plsBuilder.Append(songName);
                plsBuilder.Append(" by ");
                plsBuilder.AppendLine(songs[i].Artist);
            }

            // NumberOfEntries=20
            plsBuilder.AppendLine();
            plsBuilder.Append("NumberOfEntries");
            plsBuilder.Append('=');
            plsBuilder.Append(songs.Count);

            var plsBytes = Encoding.UTF8.GetBytes(plsBuilder.ToString());
            return File(plsBytes, "application/pls+xml", "ChavahTuneInStream.pls");
        }

        /// <summary>
        /// Returns an M3U file. Used for streaming services such as TuneIn radio.
        /// </summary>
        /// <returns></returns>
        public ActionResult ShabbatMusic()
        {
            // The M3U file will contain a single URL:
            // The URL to our GetNextSong() action.
            // That method will intelligently pick a song.

            // Build the M3U file.
            // M3U format is very simple: https://en.wikipedia.org/wiki/M3U
            var m3uBuilder = new StringBuilder();
            m3uBuilder.AppendLine("# EXTM3U"); // The header

            // Normally we could append just a single URL, but some apps (like Roku) only play 1 song then. We're fixing that here by appending many songs.
            var getNextSongUrl = Url.Action(nameof(GetNextShabbatSong), "Stream", null, Request.Scheme);
            for (var i = 0; i < 100; i++)
            {
                m3uBuilder.AppendLine(getNextSongUrl);
            }

            var m3uBytes = Encoding.UTF8.GetBytes(m3uBuilder.ToString());
            return File(m3uBytes, "application/vnd.apple.mpegurl", "ChavahTuneInStream.m3u");
        }

        public async Task<ActionResult> GetNextShabbatSong()
        {
            var goodShabbatTags = new[]
            {
                "shabbat",
                "peaceful",
                "beautiful",
                "soft",
                "slow",
                "prayer",
                "liturgy",
                "blessing",
                "hymn"
            };
            var song = await DbSession.Query<Song, Songs_GeneralQuery>()
                .Customize(x => x.RandomOrdering())
                .Where(s => s.CommunityRank >= 10 && s.Tags.ContainsAny(goodShabbatTags))
                .FirstOrDefaultAsync();
            return Redirect(song.Uri.ToString());
        }

        public async Task<ActionResult> GetNextSong()
        {
            var userPreferences = new UserSongPreferences();
            var songsWithRanking = default(IList<Songs_RankStandings.Result>);

            // Aggressive caching for the UserSongPreferences and SongsWithRanking. These don't change often.
            using (var cache = DbSession.Advanced.DocumentStore.AggressivelyCacheFor(TimeSpan.FromDays(1)))
            {
                // This is NOT an unbounded result set:
                // This queries the Songs_RankStandings index, which will reduce the results. Max number of results will be the number of CommunityRankStanding enum constants.
                songsWithRanking = await DbSession.Query<Song, Songs_RankStandings>()
                    .As<Songs_RankStandings.Result>()
                    .ToListAsync();
            }

            var songPick = userPreferences.PickSong(songsWithRanking);
            var song = await DbSession.LoadRequiredAsync<Song>(songPick.SongId);
            return Redirect(song.Uri.ToString());
        }
    }
}
