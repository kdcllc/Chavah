﻿using BitShuva.Common;
using BitShuva.Interfaces;
using BitShuva.Models;
using Raven.Client;
using Raven.Client.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using Optional;
using Optional.Async;
using BitShuva.Services;
using System.Collections.Concurrent;

namespace BitShuva.Controllers
{
    [JwtSession]
    [RoutePrefix("api/albums")]
    public class AlbumsController : RavenApiController
    {
        public AlbumsController(ILoggerService logger) : base(logger)
        {
        }

        /// <summary>
        /// Uploads the album art for a song. The album art will be applied to all songs matching the artist and album.
        /// </summary>
        /// <param name="albumArtAddress">The HTTP address where the album art can be fetched. This is expected to be a temporary address created by FilePickr.</param>
        /// <param name="fileName">The file name. Used for extracting the extension.</param>
        /// <param name="artist">The artist this album art applies to.</param>
        /// <param name="album">The name of the album this album art applies to.</param>
        [Route("Get")]
        [HttpGet]
        public Task<Album> Get(string id)
        {
            return DbSession.LoadAsync<Album>(id);
        }

        [Route("GetAlbumArtBySongId")]
        [HttpGet]
        public async Task<HttpResponseMessage> GetBySongId(string songId)
        {
            var song = await DbSession.LoadNonNull<Song>(songId);
            var response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = song.AlbumArtUri;
            return response;
        }

        [Route("GetByArtistAlbum")]
        [HttpGet]
        public Task<Album> GetByArtistAlbum(string artist, string album)
        {
            return DbSession.Query<Album>()
                .FirstOrDefaultAsync(a => a.Name == album && (a.Artist == artist || a.IsVariousArtists));
        }
        
        [HttpPost]
        [Route("changeArt")]
        public async Task<Album> ChangeArt(string albumId, string artUri)
        {
            await this.RequireAdminUser();

            var album = await DbSession.LoadAsync<Album>(albumId);
            if (album == null)
            {
                throw new ArgumentException("Couldn't find album with ID " + albumId);
            }

            var albumArtUri = await CdnManager.UploadAlbumArtToCdn(new Uri(artUri), album.Artist, album.Name, ".jpg");
            album.AlbumArtUri = albumArtUri;

            // Update the songs on this album.
            var songsOnAlbum = await this.DbSession
                .Query<Song>()
                .Where(s => s.Artist == album.Artist && s.Album == album.Name)
                .ToListAsync();
            songsOnAlbum.ForEach(s => s.AlbumArtUri = albumArtUri);

            return album;
        }

        [Route("Save")]
        [HttpPost]
        public async Task<Album> Save(Album album)
        {
            if (string.IsNullOrEmpty(album.Artist) || string.IsNullOrEmpty(album.Name))
            {
                throw new ArgumentException("Album must have a name and artist.");
            }
            if (album.Id == "")
            {
                album.Id = null;
            }

            await RequireAdminUser();
            await DbSession.StoreAsync(album);
            return album;
        }

        [HttpPost]
        [Route("upload")]
        public async Task<string> Upload(AlbumUpload album)
        {
            await this.RequireAdminUser();

            // Put the album art on the CDN.
            var albumArtUriCdn = await CdnManager.UploadAlbumArtToCdn(new Uri(album.AlbumArtUri), album.Artist, album.Name, ".jpg");

            // Put all the songs on the CDN.
            var songNumber = 1;
            var uploadService = new SongUploadService();
            foreach (var albumSong in album.Songs)
            {
                //var songUriCdn = await CdnManager.UploadMp3ToCdn(albumSong.Address, album.Artist, album.Name, songNumber, albumSong.FileName);
                var song = new Song
                {
                    Album = album.Name,
                    Artist = album.Artist,
                    AlbumArtUri = albumArtUriCdn,
                    CommunityRankStanding = CommunityRankStanding.Normal,
                    Genres = album.Genres.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(),
                    Name = albumSong.FileName,
                    Number = songNumber,
                    PurchaseUri = album.PurchaseUrl,
                    UploadDate = DateTime.UtcNow,
                    Uri = null
                };
                await this.DbSession.StoreAsync(song);
                uploadService.QueueMp3Upload(albumSong, album, songNumber, song.Id);
                songNumber++;
            }

            // Store the new album if it doesn't exist already.
            var existingAlbum = await DbSession.Query<Album>()
                .FirstOrDefaultAsync(a => a.Name == album.Name && a.Artist == album.Artist);
            if (existingAlbum == null)
            {
                existingAlbum = new Album();
            }

            existingAlbum.AlbumArtUri = albumArtUriCdn;
            existingAlbum.Artist = album.Artist;
            existingAlbum.BackgroundColor = album.BackColor;
            existingAlbum.ForegroundColor = album.ForeColor;
            existingAlbum.MutedColor = album.MutedColor;
            existingAlbum.Name = album.Name;
            existingAlbum.TextShadowColor = album.TextShadowColor;

            if (string.IsNullOrEmpty(existingAlbum.Id))
            {
                await this.DbSession.StoreAsync(existingAlbum);
            }

            await this.DbSession.SaveChangesAsync();
            return existingAlbum.Id;
        }

        [Route("GetAlbumsForSongs")]
        [HttpGet]
        public async Task<IList<Album>> GetAlbumsForSongs(string songIdsCsv)
        {
            // TODO: we might want to implement a song ID => album ID cache. That would save us a lot of work.

            if (string.IsNullOrEmpty(songIdsCsv))
            {
                throw new ArgumentNullException(nameof(songIdsCsv));
            }

            const int maxAlbumArtFetch = 30;
            var songIds = songIdsCsv.Split(',')
                .Where(s => !string.IsNullOrWhiteSpace(s) && s.StartsWith("songs/", StringComparison.InvariantCultureIgnoreCase)) // Somehow, some users are calling this with ApplicationUsers/[current email].
                .Take(maxAlbumArtFetch);

            var songs = await DbSession.LoadAsync<Song>(songIds);
            var songsWithAlbums = songs
                .Where(s => s != null && !string.IsNullOrEmpty(s.Album));
            var albumNames = songsWithAlbums
                .Select(s => s.Album)
                .ToList();
            var albums = await DbSession.Query<Album>()
                .Where(a => a.Name.In(albumNames))
                .ToListAsync();

            return albums;
        }

        /// <summary>
        /// Streams an image from another domain through our domain. 
        /// Needed for client-side canvas rendering of images on other domains (e.g. on our media CDN.)
        /// For example, when upload a new album, we use this URL to draw an image to a canvas in order to extract prominent colors from the album art.
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("art/imageondomain")]
        public async Task<HttpResponseMessage> ImageOnDomain(string imageUrl)
        {
            var response = new HttpResponseMessage();
            using (var webClient = new WebClient())
            {
                var bytes = await webClient.DownloadDataTaskAsync(imageUrl);
                response.Content = new StreamContent(new MemoryStream(bytes)); // this file stream will be closed by lower layers of web api for you once the response is completed.
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                return response;
            }
        }

        /// <summary>
        /// Find album art with the specified artist and album name.
        /// </summary>
        /// <param name="songId">The ID of the song we're checking for.</param>
        /// <param name="artist">The artist name.</param>
        /// <param name="album">The album.</param>
        /// <returns>An Album-like object containing the ID of the song.</returns>
        [Route("art/{songId}/{artist}/{album}")]
        public async Task<dynamic> GetAlbumArt(string songId, string artist, string album)
        {
            var existingAlbum = await this.DbSession
                .Query<Album>()
                .FirstOrDefaultAsync(a => a.Artist == artist && a.Name == album);
            return new
            {
                SongId = songId,
                Artist = existingAlbum?.Artist,
                Name = existingAlbum?.Name,
                AlbumArtUri = existingAlbum?.AlbumArtUri
            };
        }
        
        /// <summary>
        /// Gets the HTTP address for the album art image for the album with the specified name and artist.
        /// </summary>
        /// <param name="artist"></param>
        /// <param name="album"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("art/get")]
        public async Task<HttpResponseMessage> GetAlbumArt(string artist, string album)
        {
            var redirectUri = default(Uri);
            await this.DbSession.Query<Album>()
                .FirstOrNoneAsync(a => a.Artist == artist && a.Name == album)
                .ToAsyncOption()
                .Map(a => a.AlbumArtUri)
                .MatchSome(uri => redirectUri = uri);
            
            if (redirectUri == null)
            {
                // We don't have an album for this. See if we have a matching song.
                await this.DbSession.Query<Song>()
                    .FirstOrNoneAsync(s => s.Album == album && s.Artist == artist)
                    .ToAsyncOption()
                    .Map(s => s.AlbumArtUri)
                    .MatchSome(uri => redirectUri = uri);
                
                if (redirectUri == null)
                {
                    // We can't find album art with this artist and album, nor any song with this album and artist.
                    // See if we have an album by that name.
                    await DbSession.Query<Album>()
                        .FirstOrNoneAsync(a => a.Name == album)
                        .ToAsyncOption()
                        .Map(a => a.AlbumArtUri)
                        .MatchSome(uri => redirectUri = uri);
                }
            }

            if (redirectUri != null)
            {
                var response = Request.CreateResponse(HttpStatusCode.Moved);
                response.Headers.Location = redirectUri;
                return response;
            }

            throw new Exception("Unable to find any matching album art for " + artist + " - " + album);
        }

        /// <summary>
        /// Gets the album art for a particular song. Used in the UI by Facebook song share.
        /// </summary>
        /// <param name="songId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("art/forSong")]
        public async Task<HttpResponseMessage> GetArtForSong(string songId)
        {
            var song = await DbSession.LoadNonNull<Song>(songId);
            var response = Request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = song.AlbumArtUri;
            return response; ;
        }

        [HttpGet]
        [Route("songlisting")]
        public async Task<List<string>> SongListing(string artist, string album)
        {
            var songs = await DbSession.Query<Song>()
                .Where(s => s.Artist == artist && s.Album == album)
                .ToListAsync();
            return songs
                .Select(s => $"{s.Artist} - {s.Album} - {s.Number} - {s.Name}: http://messianicradio.com/?song={s.Id}")
                .ToList();
        }
    }
}
