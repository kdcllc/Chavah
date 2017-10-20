var BitShuva;
(function (BitShuva) {
    var Chavah;
    (function (Chavah) {
        var SongApiService = (function () {
            function SongApiService(httpApi) {
                this.httpApi = httpApi;
            }
            SongApiService.prototype.chooseSong = function () {
                return this.httpApi.query("/api/songs/chooseSong", null, SongApiService.songConverter);
            };
            SongApiService.prototype.chooseSongBatch = function () {
                return this.httpApi.query("/api/songs/chooseSongBatch", null, SongApiService.songListConverter);
            };
            SongApiService.prototype.getSongById = function (id, songPickReason) {
                var task = this.httpApi.query("/api/songs/getById", { songId: id }, SongApiService.songOrNullConverter);
                if (songPickReason != null) {
                    task.then(function (song) {
                        if (song) {
                            song.setSolePickReason(songPickReason);
                        }
                    });
                }
                return task;
            };
            SongApiService.prototype.getSongByArtistAndAlbum = function (artist, album) {
                var url = "/api/songs/getByArtistAndAlbum";
                var args = {
                    artist: artist,
                    album: album
                };
                return this.httpApi.query(url, args, SongApiService.songOrNullConverter);
            };
            SongApiService.prototype.getSongByAlbum = function (album) {
                var url = "/api/songs/getByAlbum/";
                var args = {
                    album: album
                };
                return this.httpApi.query(url, args, SongApiService.songOrNullConverter);
            };
            SongApiService.prototype.getSongWithTag = function (tag) {
                var url = "/api/songs/getByTag";
                var args = {
                    tag: tag
                };
                return this.httpApi.query(url, args, SongApiService.songOrNullConverter);
            };
            SongApiService.prototype.getSongByArtist = function (artist) {
                var url = "/api/songs/getByArtist";
                var args = {
                    artist: artist
                };
                return this.httpApi.query(url, args, SongApiService.songOrNullConverter);
            };
            SongApiService.prototype.getSongMatches = function (searchText) {
                var url = "/api/songs/search";
                var args = {
                    searchText: searchText
                };
                return this.httpApi.query(url, args, SongApiService.songListConverter);
            };
            SongApiService.prototype.getTrendingSongs = function (skip, take) {
                var args = {
                    skip: skip,
                    take: take
                };
                return this.httpApi.query("/api/songs/getTrending", args, SongApiService.songPagedListConverter);
            };
            SongApiService.prototype.getPopularSongs = function (count) {
                var args = {
                    count: count
                };
                return this.httpApi.query("/api/songs/top", args, SongApiService.songListConverter);
            };
            SongApiService.prototype.getLikes = function (count) {
                var args = {
                    count: count
                };
                return this.httpApi.query("/api/songs/getRandomLikedSongs", args, SongApiService.songListConverter);
            };
            SongApiService.prototype.getRecentPlays = function (count) {
                var args = {
                    count: count
                };
                return this.httpApi.query("/api/songs/getRecentPlays", args, SongApiService.songListConverter);
            };
            SongApiService.prototype.songCompleted = function (songId) {
                return this.httpApi.post("/api/songs/completed?songId=" + songId, null);
            };
            SongApiService.prototype.songFailed = function (error) {
                return this.httpApi.post("/api/songs/audiofailed", error);
            };
            SongApiService.songPagedListConverter = function (dto) {
                return {
                    items: dto.items.map(function (s) { return new Chavah.Song(s); }),
                    skip: dto.skip,
                    take: dto.take,
                    total: dto.total
                };
            };
            SongApiService.songListConverter = function (songs) {
                return songs.map(function (r) { return SongApiService.songConverter(r); });
            };
            SongApiService.songOrNullConverter = function (raw) {
                if (raw) {
                    return SongApiService.songConverter(raw);
                }
                return null;
            };
            SongApiService.songConverter = function (raw) {
                return new Chavah.Song(raw);
            };
            return SongApiService;
        }());
        SongApiService.$inject = ["httpApi"];
        Chavah.SongApiService = SongApiService;
        Chavah.App.service("songApi", SongApiService);
    })(Chavah = BitShuva.Chavah || (BitShuva.Chavah = {}));
})(BitShuva || (BitShuva = {}));
