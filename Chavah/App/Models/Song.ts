namespace BitShuva.Chavah {
    export class Song {
        name: string;
        album: string;
        artist: string;
        artistImages: string[];
        albumArtUri: string;
        albumSongNumber: number;
        uri: string;
        likeStatus: number;
        communityRank: number;
        communityRankStanding: number;
        id: string;
        clientId: string;
        thumbUpImage: string;
        thumbDownImage: string;
        communityRankColor: string;
        lyrics: string;
        genres: string[];
        albumArtOrArtistImage = "";
        isCumulativeRank = true;
        totalPlays: number;
        totalUpVotes: number | null;
        totalDownVotes: number | null;
        artistInfo: Server.IArtist;
        tags: string[];
        purchaseUri: string;

        albumSwatchBackground = "white";
        albumSwatchForeground = "black";
        albumSwatchMuted = "gray";
        albumSwatchTextShadow = "white";
        hasCalculatedAlbumSwatches = false;

        static defaultSwatch: ISwatch = {
            getBodyTextColor: () => "black",
            getHex: () => "white",
            getHsl: () => "black",
            getPopulation: () => 0,
            getTitleTextColor: () => "black",
            hsl: [255, 255, 255],
            rgb: [255, 255, 255]
        }

        constructor(song: Server.ISong) {
            this.name = song.name;
            this.albumSongNumber = song.number;
            this.album = song.album;
            this.artist = song.artist;
            this.albumArtUri = song.albumArtUri;
            this.uri = song.uri;
            this.likeStatus = song.songLike;
            this.communityRank = song.communityRank;
            this.communityRankStanding = song.communityRankStanding;
            this.id = song.id;
            this.lyrics = song.lyrics;
            this.genres = song.genres;
            this.artistImages = song.artistImages;
            this.albumArtOrArtistImage = song.albumArtUri;
            this.totalPlays = song.totalPlays;
            this.tags = song.tags;

            this.purchaseUri = song.purchaseUri ? song.purchaseUri : 'http://lmgtfy.com/?q=' + encodeURIComponent(this.artist + " " + this.album + " purchase");
            this.clientId = `${song.id}_${new Date().getTime() + Math.random()}`;
        }

        calculateAlbumColors(q: ng.IQService): ng.IPromise<any> {
            var result = q.defer<any>();
            if (this.hasCalculatedAlbumSwatches) {
                result.resolve();
            } else {
                var img = document.createElement("img");
                img.src = "/api/albums/art/test?songId=" + this.id;
                img.addEventListener("load", () => {
                    this.hasCalculatedAlbumSwatches = true;
                    var vibrant = new Vibrant(img, 64, 5);
                    var swatches = vibrant.swatches();
                    if (swatches) {
                        this.albumSwatchBackground = (swatches.DarkVibrant || swatches.DarkMuted || Song.defaultSwatch).getHex();
                        this.albumSwatchForeground = (swatches.LightVibrant || swatches.Vibrant || Song.defaultSwatch).getHex();
                        this.albumSwatchMuted = (swatches.DarkMuted || swatches.DarkVibrant || swatches.Vibrant || Song.defaultSwatch).getBodyTextColor();
                        this.albumSwatchTextShadow = (swatches.DarkMuted || swatches.DarkVibrant || Song.defaultSwatch).getHex();
                    }

                    result.resolve();
                });
            }

            return result.promise;
        }

        isLiked(): boolean {
            return this.likeStatus === SongLike.Liked;
        }

        isDisliked(): boolean {
            return this.likeStatus === SongLike.Disliked;
        }

        likeHoverText(): string {
            return this.isLiked() ?
                "You've already liked this song. We'll keep playing more songs like it" :
                "I like this song, play more songs like it";
        }

        dislikeHoverText(): string {
            return this.isDisliked() ?
                "You already disliked this song. We'll keep it on the back shelf, and rarely play it for you" :
                "I don't like this song, don't play it again";
        }

        //dislike() {
        //    if (!this.isDisliked()) {
        //        var incrementAmount = this.isLiked() ? -2 : -1;
        //        this.likeOrDislike(incrementAmount, "Dislike");
        //        ko.postbox.publish("NextSong");

        //        if (this.totalDownVotes() != null) {
        //            this.totalDownVotes(this.totalDownVotes() + incrementAmount);
        //        }
        //    }
        //}

        //like() {
        //    if (!this.isLiked()) {
        //        var incrementAmount = this.isDisliked() ? 2 : 1;
        //        this.likeOrDislike(incrementAmount, "Like");

        //        if (this.totalUpVotes() != null) {
        //            this.totalUpVotes(this.totalUpVotes() + incrementAmount);
        //        }
        //    }
        //}

        colorClass() {
            return this.getColorClass();
        }

        totalUpVoteText(): string | null {
            return this.totalUpVotes ? '+' + this.totalUpVotes : null;
        }

        totalDownVoteText(): string | null {
            return this.totalDownVotes ? '-' + this.totalDownVotes : null;
        }

        get tagsCsv(): string {
            return this.tags.join(", ")
        }
        
        set tagsCsv(val: string) {
            this.tags = val.split(",").map(v => v.trim());
        }

        get genresCsv() {
            return this.genres.join(", ");
        }

        set genresCsv(val: string) {
            this.genres = val.split(",").map(v => v.trim());
        }

        //likeOrDislike(increment, actionName) {
        //    this.communityRank(this.communityRank() + increment);
        //    this.likeStatus(increment > 0 ? Song.liked : Song.disliked);

        //    new LikeDislikeSongCommand(this.id, increment > 0)
        //        .execute()
        //        .done((rating: number) => this.communityRank(rating));
        //}

        showIndividualRank() {
            this.isCumulativeRank = false;
            //this.fetchIndividualRanks();
        }

        //fetchIndividualRanks() {
        //    new GetUpDownVotesCommand(this.id)
        //        .execute()
        //        .done((result: UpDownVotesDto) => {
        //        this.totalUpVotes(result.UpVotes);
        //        this.totalDownVotes(result.DownVotes);
        //    });
        //}
        
        toDto(): Server.ISong {
            return {
                album: this.album,
                albumArtUri: this.albumArtUri,
                artist: this.artist,
                artistImages: this.artistImages,
                communityRank: this.communityRank,
                communityRankStanding: this.communityRankStanding,
                genres: this.genres,
                id: this.id,
                lyrics: this.lyrics,
                name: this.name,
                number: this.albumSongNumber,
                purchaseUri: this.purchaseUri,
                songLike: this.likeStatus,
                tags: this.tags,
                totalPlays: this.totalPlays,
                uri: this.uri
            };
        }

        updateFrom(other: Song) {
            this.album = other.album;
            this.albumArtUri = other.albumArtUri;
            this.albumSongNumber = other.albumSongNumber;
            this.artist = other.artist;
            this.artistImages = ([] as string[]).concat(other.artistImages);
            this.artistInfo = other.artistInfo;
            this.communityRank = other.communityRank;
            this.communityRankStanding = other.communityRankStanding;
            this.genres = ([] as string[]).concat(other.genres);
            this.tags = ([] as string[]).concat(other.tags);
            this.id = other.id;
            this.likeStatus = other.likeStatus;
            this.lyrics = other.lyrics;
            this.name = other.name;
            this.totalPlays = other.totalPlays;
            this.purchaseUri = other.purchaseUri;
            this.uri = other.uri;
        }

        static empty(): Song {
            return new Song({
                album: "",
                albumArtUri: "",
                artist: "",
                communityRank: 0,
                communityRankStanding: 0,
                id: "songs/0",
                artistImages: [],
                genres: [],
                lyrics: "",
                name: "",
                number: 0,
                purchaseUri: "",
                songLike: 0,
                tags: [],
                totalPlays: 0,
                uri: ""
            });
        }

        private getCommunityRankTitle() {
            var standing = this.communityRankStanding;
            var rank = this.communityRank;
            var standingText =
                standing === 0 ? "Average" :
                    standing === 1 ? "Very Poor" :
                        standing === 2 ? "Poor" :
                            standing === 3 ? "Good" :
                                standing === 4 ? "Great" :
                                    "Best";
            return standingText;
        }

        private getNthSongText(): string {
            var value =
                this.albumSongNumber === 0 ? "1st" :
                    this.albumSongNumber === 1 ? "1st" :
                        this.albumSongNumber === 2 ? "2nd" :
                            this.albumSongNumber === 3 ? "3rd" :
                                this.albumSongNumber >= 4 && this.albumSongNumber <= 19 ? this.albumSongNumber + "th" :
                                    "#" + this.albumSongNumber;
            return value;
        }

        private getColorClass() {
            var rank = this.communityRank;
            var styleNumber =
                rank <= -10 ? 0 :
                    rank <= -2 ? 1 :
                        rank <= 20 ? 2 :
                            rank <= 40 ? 3 :
                                rank <= 80 ? 4 :
                                    rank <= 100 ? 5 :
                                        rank <= 120 ? 6 :
                                            rank <= 150 ? 7 :
                                                rank <= 200 ? 8 :
                                                    rank <= 250 ? 9 :
                                                        rank <= 300 ? 10 :
                                                            rank <= 350 ? 11 :
                                                                rank <= 400 ? 12 :
                                                                    rank <= 450 ? 13 :
                                                                        rank <= 500 ? 14 :
                                                                            rank <= 600 ? 15 :
                                                                                rank <= 700 ? 16 :
                                                                                    17;
            return "song-rank-" + styleNumber;
        }
    }
}