﻿using BitShuva.Common;
using BitShuva.Models;
using Raven.Client;
using Raven.Client.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace BitShuva.Controllers
{
    [RoutePrefix("api/requests")]
    public class SongRequestsController : RavenApiController
    {
        [Route("pending")]
        public async Task<string> GetPendingRequestedSongId()
        {
            var user = await this.GetCurrentUser();
            if (user == null)
            {
                return null;
            }

            var recentSongRequests = await this.DbSession
                 .Query<SongRequest>()
                 .Customize(x => x.WaitForNonStaleResultsAsOfNow(TimeSpan.FromSeconds(5)))
                 .OrderByDescending(d => d.DateTime)
                 .Take(10)
                 .ToListAsync();
            
            var recent = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(30));
            var validSongRequest = recentSongRequests
                .OrderBy(d => d.DateTime) // OrderBy to give us the oldest of the recent song requests first.
                .FirstOrDefault(s => s.DateTime >= recent && !s.PlayedForUserIds.Contains(user.Id));
            if (validSongRequest != null)
            {
                validSongRequest.PlayedForUserIds.Add(user.Id);
                await this.DbSession.StoreAsync(validSongRequest);
                await this.DbSession.SaveChangesAsync();
            }

            // We've got a valid song request. Verify the user hasn't disliked this song.
            if (validSongRequest != null)
            {
                var userDislikesSong = await this.DbSession
                    .Query<Like>()
                    .Where(l => l.UserId == user.Id && l.Status == LikeStatus.Dislike && l.SongId == validSongRequest.SongId)
                    .AnyAsync();
                if (!userDislikesSong)
                {
                    return validSongRequest.SongId;
                }
            }

            return null;
        }
        
        [HttpPost]
        [Route("requestsong")]
        public async Task RequestSong(string songId)
        {
            var user = await this.GetCurrentUser();
            var song = await this.DbSession.LoadAsync<Song>(songId);
            if (song != null && user != null)
            {
                var requestExpiration = DateTime.UtcNow.AddDays(2);
                var hasRecentPendingRequest = await this.HasRecentPendingSongRequest(songId);
                var hasManyRequestForArtist = await this.HasManyPendingSongRequestForArtist(song.Artist);
                var hasManySongRequestsFromUser = await this.HasManyRecentSongRequestsFromUser(user.Id);
                if (!hasRecentPendingRequest && !hasManyRequestForArtist && !hasManySongRequestsFromUser)
                {
                    user.TotalSongRequests++;
                    var songRequest = new SongRequest
                    {
                        DateTime = DateTime.Now,
                        PlayedForUserIds = new List<string> { user.Id },
                        SongId = songId,
                        Artist = song.Artist,
                        Name = song.Name,
                        UserId = user.Id
                    };
                    await this.DbSession.StoreAsync(songRequest);
                    this.DbSession.AddRavenExpiration(songRequest, requestExpiration);
                }

                var songArtist = song.Artist;
                var activity = new Activity
                {
                    DateTime = DateTime.Now,
                    Title = string.Format("{0} - {1} was requested by one of our listeners", song.Artist, song.Name),
                    Description = string.Format("\"{0}\" by {1} was requested by one of our listeners on Chavah Messianic Radio.", song.Name, songArtist),
                    MoreInfoUri = song.GetSongShareLink()
                };
                await this.DbSession.StoreAsync(activity);
                this.DbSession.AddRavenExpiration(activity, requestExpiration);
            }
        }

        private async Task<bool> HasRecentPendingSongRequest(string songId)
        {
            var recent = DateTime.Now.Subtract(TimeSpan.FromMinutes(120));
            return await this.DbSession
                .Query<SongRequest>()
                .AnyAsync(s => s.SongId == songId && s.DateTime >= recent);
        }

        private async Task<bool> HasManyPendingSongRequestForArtist(string artist)
        {
            var recent = DateTime.Now.Subtract(TimeSpan.FromMinutes(60));
            var many = 1;
            return await this.DbSession
                .Query<SongRequest>()
                .CountAsync(s => s.Artist == artist && s.DateTime >= recent) >= many;
        }

        private async Task<bool> HasManyRecentSongRequestsFromUser(string userId)
        {
            var recent = DateTime.Now.Subtract(TimeSpan.FromMinutes(60));
            var many = 2;
            var recentSongRequestsFromUser = await this.DbSession
                .Query<SongRequest>()
                .CountAsync(s => s.UserId == userId && s.DateTime >= recent);
            return recentSongRequestsFromUser >= many;
        }
    }
}
