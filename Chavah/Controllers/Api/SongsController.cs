﻿using BitShuva.Common;
using BitShuva.Models;
using Raven.Client.Linq;
using Raven.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Threading.Tasks;
using BitShuva.Models.Indexes;
using BitShuva.Interfaces;
using BitShuva.Services;

namespace BitShuva.Controllers
{
    [RoutePrefix("api/songs")]
    [JwtSession]
    public class SongsController : RavenApiController
    {
        private ILoggerService _logger;

        public SongsController(ILoggerService logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [Route("GetRecentPlays")]
        public async Task<IEnumerable<Song>> GetRecentPlays(int count)
        {
            var user = await this.GetCurrentUser();
            if (user == null)
            {
                return new Song[0];
            }

            var songs = await DbSession.LoadAsync<Song>(user.RecentSongIds.Take(count * 2));
            return songs
                .Where(s => s != null)
                .Distinct(s => s.Id)
                .Take(count)
                .ToList();
        }

        [HttpGet]
        [Route("GetRandomLikedSongs")]
        public async Task<IEnumerable<Song>> GetRandomLikedSongs(int count)
        {
            var user = await this.GetCurrentUser();
            if (user == null)
            {
                return new Song[0];
            }

            var likedSongIds = await this.DbSession
                .Query<Like>()
                .Customize(x => x.RandomOrdering())
                .Customize(x => x.Include<Like>(l => l.SongId))
                .Where(l => l.Status == LikeStatus.Like && l.UserId == user.Id)
                .Select(l => l.SongId)
                .Take(count)
                .ToListAsync();

            var loadedSongs = await this.DbSession.LoadAsync<Song>(likedSongIds);
            return loadedSongs
                .Where(s => s != null)
                .Select(s => s.ToDto());
        }

        [HttpGet]
        [Route("GetLikedSongs")]
        public async Task<PagedList<Song>> GetLikedSongs(int skip, int take)
        {
            var user = await this.GetCurrentUser();
            if (user == null)
            {
                return new PagedList<Song>();
            }
            
            var likedSongIds = await this.DbSession
                .Query<Like>()
                .Customize(x => x.Include<Like>(l => l.SongId))
                .Statistics(out var stats)
                .Where(l => l.Status == LikeStatus.Like && l.UserId == user.Id)
                .OrderByDescending(l => l.Date)
                .Select(l => l.SongId)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var songs = await this.DbSession.LoadAsync<Song>(likedSongIds);
            return new PagedList<Song>
            {
                Items = songs.Where(s => s != null).ToArray(),
                Skip = skip,
                Take = take,
                Total = stats.TotalResults
            };
        }

        [HttpPost]
        [Authorize(Roles = ApplicationUser.AdminRole)]
        [Route("delete")]
        public async Task Delete(string songId)
        {
            var song = await this.DbSession.LoadAsync<Song>(songId);
            if (song != null)
            {
                this.DbSession.Delete(song);

                // Remove any likes of this song.
                var songLikeIds = await this.DbSession.Query<Like>()
                    .Where(l => l.SongId == songId)
                    .Take(1000)
                    .Select(l => l.Id)
                    .ToListAsync();
                songLikeIds.ForEach(id => DbSession.Delete(id));

                try
                {
                    await Task.Run(() => CdnManager.DeleteFromCdn(song));
                }
                catch (Exception error)
                {
                    await _logger.Error("Admin deleted song from the database, but we couldn't delete it from the CDN.", error.ToString(), songId);
                    
                    // We eat the exception here, because the song has been deleted from the database. That we didn't remove it from the CDN is a minor inconvenience.
                }
            }
        }

        [HttpPost]
        [Route("admin/save")]
        public async Task<Song> Save(Song song)
        {
            await this.RequireAdminUser();

            var dbSong = await this.DbSession.LoadAsync<Song>(song.Id);
            dbSong.Artist = song.Artist;
            dbSong.Album = song.Album;
            dbSong.CommunityRank = song.CommunityRank;
            dbSong.Name = song.Name;
            dbSong.Number = song.Number;
            dbSong.PurchaseUri = song.PurchaseUri;
            dbSong.AlbumArtUri = song.AlbumArtUri;
            dbSong.Genres = song.Genres;
            dbSong.Tags = song.Tags;
            dbSong.Lyrics = song.Lyrics;

            await this.DbSession.StoreAsync(dbSong);

            return dbSong;
        }

        [Route("search")]
        public async Task<IEnumerable<Song>> GetSongsMatches(string searchText)
        {
            var makeQuery = new Func<Func<string, IQueryable<Song>>>(() =>
            {
                return q => this.DbSession
                    .Query<Song, Songs_Search>()
                    .Search(s => s.Name, q, 2, SearchOptions.Guess, EscapeQueryOptions.AllowPostfixWildcard)
                    .Search(s => s.Album, q, 1, SearchOptions.Guess, EscapeQueryOptions.AllowPostfixWildcard)
                    .Search(s => s.Artist, q, 1, SearchOptions.Guess, EscapeQueryOptions.AllowPostfixWildcard)
                    .Take(50);
            });

            var query = makeQuery()(searchText + "*");
            var results = await query.ToListAsync();
            
            // No results? See if we can suggest some near matches.
            if (results.Count == 0)
            {
                var suggestResults = await makeQuery()(searchText + "*").SuggestAsync();
                var suggestions = suggestResults.Suggestions;
                var firstSuggestion = suggestions.FirstOrDefault();
                if (firstSuggestion != null)
                {
                    // Run the query for that suggestion.
                    var newQuery = makeQuery();
                    var suggestedResults = await newQuery(firstSuggestion).ToListAsync();
                    return suggestedResults.Select(r => r.ToDto());
                }
            }

            return results.Select(r => r.ToDto());
        }

        /// <summary>
        /// Used for debugging: generates a user's song preferences table as a list of strings. Includes performance measurements.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<List<string>> GetPrefsDebug(string email)
        {
            var stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();

            var userId = "ApplicationUsers/" + email;
            var userPreferences = await DbSession.Query<Like, Likes_SongPreferences>()
                .As<UserSongPreferences>()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            var userPrefsTime = stopWatch.Elapsed;
            stopWatch.Restart();

            if (userPreferences == null)
            {
                userPreferences = new UserSongPreferences();
            }

            var songsWithRanking = await this.DbSession.Query<Song, Songs_RankStandings>()
                    .As<Songs_RankStandings.Result>()
                    .ToListAsync();
            var rankingTime = stopWatch.Elapsed;
            stopWatch.Restart();

            var table = userPreferences.BuildSongWeightsTable(songsWithRanking);

            var tableTime = stopWatch.Elapsed;
            stopWatch.Stop();

            var songsOrderedByWeight = table
                .Select(s => (SongId: s.Key, Weight: s.Value.Weight, ArtistMultiplier: s.Value.ArtistMultiplier, AlbumMultiplier: s.Value.AlbumMultiplier, SongMultipler: s.Value.SongMultiplier, TagMultiplier: s.Value.TagMultiplier, RankMultiplier: s.Value.CommunityRankMultiplier))
                .OrderByDescending(s => s.Weight)
                .Select(s => $"Song ID {s.SongId}, Weight {s.Weight}, Artist multiplier: {s.ArtistMultiplier}, Album multipler: {s.AlbumMultiplier}, Song multiplier: {s.SongMultipler}, Tag multiplier {s.TagMultiplier}, Rank multiplier: {s.RankMultiplier}")
                .ToList();

            songsOrderedByWeight.Insert(0, $"Performance statistics: Total query time {tableTime + rankingTime + userPrefsTime}. Querying user prefs {userPrefsTime}, querying ranking {rankingTime}, building table {tableTime}");

            return songsOrderedByWeight;
        }

        /// <summary>
        /// Called when the user asks for the next song.
        /// </summary>
        /// <returns></returns>
        [Route("chooseSong")]
        [HttpGet]
        public async Task<Song> ChooseSong()
        {
            // HOT PATH: This method greatly impacts the UI. The user waits for this method before ever hearing a song.
            // We want to send back the next song ASAP.

            var userPreferences = default(UserSongPreferences);
            var songsWithRanking = default(IList<Songs_RankStandings.Result>);

            // Aggressive caching for the UserSongPreferences and SongsWithRanking. These don't change often.
            using (DbSession.Advanced.DocumentStore.AggressivelyCacheFor(TimeSpan.FromDays(1)))
            {
                var user = await this.GetCurrentUser();

                // This is NOT an unbounded result set:
                // This queries the Songs_RankStandings index, which will reduce the results. Max number of results will be the number of CommunityRankStanding enum constants.
                songsWithRanking = await this.DbSession.Query<Song, Songs_RankStandings>()
                    .As<Songs_RankStandings.Result>()
                    .ToListAsync();
                if (user != null)
                {
                    userPreferences = await DbSession.Query<Like, Likes_SongPreferences>()
                        .As<UserSongPreferences>()
                        .FirstOrDefaultAsync(u => u.UserId == user.Id);
                }
                if (userPreferences == null)
                {
                    userPreferences = new UserSongPreferences();
                }
            }

            // Run the song picking algorithm.
            var songPick = userPreferences.PickSong(songsWithRanking);
            if (string.IsNullOrEmpty(songPick.SongId))
            {
                await _logger.Warn("Chose song but ended up with an empty Song ID.", songPick);
                return await this.PickRandomSong();
            }

            var song = await DbSession.LoadNonNull<Song>(songPick.SongId);            
            var songLikeDislike = userPreferences.Songs.FirstOrDefault(s => s.SongId == song.Id);
            var songLikeStatus = songLikeDislike != null && songLikeDislike.LikeCount > 0 ?
                LikeStatus.Like : songLikeDislike != null && songLikeDislike.DislikeCount > 0 ?
                LikeStatus.Dislike : LikeStatus.None;
            return song.ToDto(songLikeStatus, songPick);
        }
        
        [HttpGet]
        [Route("chooseSongBatch")]
        public async Task<IEnumerable<Song>> ChooseSongBatch()
        {
            const int songsInBatch = 5;
            var userPreferences = default(UserSongPreferences);
            var songsWithRanking = default(IList<Songs_RankStandings.Result>);

            // Aggressive caching for the UserSongPreferences and SongsWithRanking. These don't change often.
            using (var cache = DbSession.Advanced.DocumentStore.AggressivelyCacheFor(TimeSpan.FromDays(1)))
            {
                var user = await this.GetCurrentUser();

                // This is NOT an unbounded result set:
                // This queries the Songs_RankStandings index, which will reduce the results. Max number of results will be the number of CommunityRankStanding enum constants.
                songsWithRanking = await this.DbSession.Query<Song, Songs_RankStandings>()
                    .As<Songs_RankStandings.Result>()
                    .ToListAsync();
                if (user != null)
                {
                    userPreferences = await DbSession.Query<Like, Likes_SongPreferences>()
                        .As<UserSongPreferences>()
                        .FirstOrDefaultAsync(u => u.UserId == user.Id);
                }
                if (userPreferences == null)
                {
                    userPreferences = new UserSongPreferences();
                }
            }

            // Run the song picking algorithm.
            var batch = new List<Song>(songsInBatch);
            var pickedSongs = Enumerable.Range(0, 5)
                .Select(_ => userPreferences.PickSong(songsWithRanking))
                .ToList();
            if (pickedSongs.Any(s => string.IsNullOrEmpty(s.SongId)))
            {
                await _logger.Warn("Picked songs for batch, but returned one or more empty song IDs", pickedSongs);
            }

            // Make a single trip to the database to load all the picked songs.
            var pickedSongIds = pickedSongs
                .Select(s => s.SongId)
                .ToList();
            var songs = await DbSession.LoadAsync<Song>(pickedSongIds);
            if (songs.Any(s => s == null))
            {
                await _logger.Warn("Picked songs for batch, but some of the songs came back null.", (SongPicks: pickedSongs, SongIds: pickedSongIds));
            }

            var songDtos = new List<Song>(songs.Length);
            for (var i = 0; i < songs.Length; i++)
            {
                var song = songs[i];
                if (song != null)
                {
                    var pickReasons = pickedSongs[i];
                    var songLikeDislike = userPreferences.Songs.FirstOrDefault(s => s.SongId == song.Id);
                    var songLikeStatus = songLikeDislike != null && songLikeDislike.LikeCount > 0 ?
                        LikeStatus.Like : songLikeDislike != null && songLikeDislike.DislikeCount > 0 ?
                        LikeStatus.Dislike : LikeStatus.None;
                    var dto = song.ToDto(songLikeStatus, pickReasons);
                    songDtos.Add(dto);
                }
            }

            return songDtos;
        }

        [Route("GetById")]
        public async Task<Song> GetSongById(string songId)
        {
            var song = await this.DbSession.LoadAsync<Song>(songId);
            if (song == null)
            {
                return null;
            }

            return await this.GetSongDto(song, SongPick.YouRequestedSong);
        }

        [HttpPost]
        [Route("completed")]
        public async Task SongCompleted(string songId)
        {
            var user = await this.GetCurrentUser();
            if (user != null)
            {
                user.TotalPlays++;
                user.LastSeen = DateTime.UtcNow;
                user.AddRecentSong(songId);
            }

            var song = await this.DbSession.LoadAsync<Song>(songId);
            if (song != null)
            {
                song.TotalPlays++;
            }
        }

        [Route("GetByArtistAndAlbum")]
        [HttpGet]
        public async Task<Song> GetByArtistAndAlbum(string artist, string album)
        {
            var songOrNull = await this.DbSession.Query<Song>()
                    .Customize(c => c.RandomOrdering())
                    .FirstOrDefaultAsync(s => s.Album == album && s.Artist == artist);
            if (songOrNull == null)
            {
                await _logger.Warn("Couldn't find song by artist and album", (Artist: artist, Album: album));
                return null;
            }

            return await this.GetSongDto(songOrNull, SongPick.SongFromAlbumRequested);
        }

        [Route("getByTag")]
        public async Task<Song> GetByTag(string tag)
        {
            var songOrNull = await this.DbSession
                    .Query<Song>()
                    .Customize(c => c.RandomOrdering())
                    .FirstOrDefaultAsync(s => s.Tags.Contains(tag));
            if (songOrNull == null)
            {
                await _logger.Warn("Couldn't find song with tag", tag);
                return null;
            }

            return await this.GetSongDto(songOrNull, SongPick.SongWithTagRequested);
        }
        
        [Route("getByAlbum")]
        public async Task<Song> GetSongByAlbum(string album)
        {
            var albumUnescaped = Uri.UnescapeDataString(album);
            var songOrNull = await this.DbSession.Query<Song>()
                    .Customize(c => c.RandomOrdering())
                    .FirstOrDefaultAsync(s => s.Album == albumUnescaped);
            if (songOrNull != null)
            {
                return await GetSongDto(songOrNull, SongPick.SongFromAlbumRequested);
            }

            return null;
        }

        [Route("getByArtist")]
        [HttpGet]
        public async Task<Song> GetByArtist(string artist)
        {
            var artistUnescaped = Uri.UnescapeDataString(artist);
            var songOrNull = await this.DbSession.Query<Song>()
                .Customize(c => c.RandomOrdering())
                .FirstOrDefaultAsync(s => s.Artist == artistUnescaped);

            if (songOrNull != null)
            {
                return await GetSongDto(songOrNull, SongPick.SongFromArtistRequested);
            }

            return null;
        }

        [Route("getTrending")]
        public async Task<PagedList<Song>> GetTrending(int skip, int take)
        {
            var recentLikedSongIds = await this.DbSession
                .Query<Like>()
                .Statistics(out var stats)
                .Customize(x => x.Include<Like>(l => l.SongId))
                .Where(l => l.Status == LikeStatus.Like)
                .OrderByDescending(l => l.Date)
                .Select(l => l.SongId)
                .Skip(skip)
                .Take(take + 10)
                .ToListAsync();
            var distinctSongIds = recentLikedSongIds
                .Distinct()
                .Take(take);

            var matchingSongs = await this.DbSession.LoadWithoutNulls<Song>(distinctSongIds);
            return new PagedList<Song>
            {
                Items = matchingSongs
                    .Select(s => s.ToDto())
                    .ToList(),
                Skip = skip,
                Take = take,
                Total = stats.TotalResults
            };
        }

        [Obsolete("Delete this after July 4th 2017")]
        [Route("trending")]
        public async Task<IEnumerable<Song>> GetTrendingSongs(int count)
        {
            var recentLikedSongIds = await this.DbSession
                .Query<Like>()
                .Customize(c => c.Include<Like>(l => l.SongId))
                .Where(l => l.Status == LikeStatus.Like)
                .OrderByDescending(l => l.Date)
                .Select(l => l.SongId)
                .Take(count + 10)
                .ToListAsync();
            var distinctSongIds = recentLikedSongIds
                .Distinct()
                .Take(count);

            var matchingSongs = await this.DbSession.LoadAsync<Song>(distinctSongIds);
            return matchingSongs
                .Where(s => s != null)
                .Select(s => s.ToDto());
        }
        
        [Route("top")]
        public async Task<IEnumerable<Song>> GetTopSongs(int count)
        {
            var randomSpotInTop70 = new Random().Next(0, 70);
            var songs = await this.DbSession.Query<Song, Songs_GeneralQuery>()
                .Customize(x => x.RandomOrdering())
                .OrderByDescending(s => s.CommunityRank)
                .Skip(randomSpotInTop70)
                .Take(count)
                .ToListAsync();

            return songs.Select(s => s.ToDto(LikeStatus.None, SongPick.RandomSong));
        }

        [Route("heavenly70")]
        public async Task<IEnumerable<Song>> GetHeavenly70()
        {
            return await this.DbSession.Query<Song, Songs_GeneralQuery>()
                .OrderByDescending(s => s.CommunityRank)
                .Take(70)
                .ToListAsync();
        }

        [HttpPost]
        [Route("audioFailed")]
        public async Task<AudioErrorInfo> AudioFailed(AudioErrorInfo errorInfo)
        {
            errorInfo.UserId = this.SessionToken?.UserId;
            await _logger.Error("Audio playback failed", null, errorInfo);
            return errorInfo;
        }
        
        private async Task<Song> PickRandomSong()
        {
            return await this.DbSession.Query<Song>()
                .Customize(c => c.RandomOrdering())
                .FirstAsync();
        }

        private async Task<Song> GetSongDto(Song song, SongPick pickReason)
        {
            var user = await this.GetCurrentUser();
            if (user != null)
            {
                var songLike = await this.DbSession
                    .Query<Like>()
                    .FirstOrDefaultAsync(s => s.UserId == user.Id && s.SongId == song.Id);

                return song.ToDto(songLike.StatusOrNone(), pickReason);
            }

            return song.ToDto();
        }
    }
}