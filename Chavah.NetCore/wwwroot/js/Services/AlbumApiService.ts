﻿namespace BitShuva.Chavah {
    export class AlbumApiService {

        static $inject = [
            "httpApi",
            "$q"
        ];

        constructor(
            private httpApi: HttpApiService,
            private $q: ng.IQService) {
        }

        /**
         * Uploads a new album. Returns a promise containing the ID of the new album.
         */
        upload(album: Server.IAlbumUpload): ng.IPromise<string> {
            return this.httpApi.post("/api/albums/upload", album);
        }

        changeArt(albumId: string, artUri: string) {
            var args = {
                albumId: albumId,
                artUri: artUri
            };
            return this.httpApi.postUriEncoded("/api/albums/changeArt", args, AlbumApiService.albumSelector);
        }

        get(id: string): ng.IPromise<Album | null> {
            var args = {
                id: id
            };
            return this.httpApi.query<Server.IAlbum | null>("/api/albums/get", args, AlbumApiService.albumSelector);
        }

        getAll(skip: number, take: number, search: string | null): ng.IPromise<Server.IPagedList<Album>> {
            var args = {
                skip: skip,
                take: take,
                search: search
            };
            return this.httpApi.query("/api/albums/getAll", args, AlbumApiService.albumPagedListSelector);
        }

        getByArtistAndAlbumName(artist: string, album: string): ng.IPromise<Album | null> {
            var args = {
                artist: artist,
                album: album
            };
            return this.httpApi.query<Server.IAlbum | null>("/api/albums/getByArtistAlbum", args, AlbumApiService.albumSelector);
        }

        save(album: Album): ng.IPromise<Album> {
            return this.httpApi.post("/api/albums/save", album, AlbumApiService.albumSelector);
        }

        getAlbums(albumIds: string[]): ng.IPromise<Album[]> {
            var args = {
                albumIdsCsv: albumIds.join(",")
            };
            return this.httpApi.query("/api/albums/getAlbums", args, AlbumApiService.albumArraySelector);
        }

        //getAlbumsForSongs(songIds: string[]): ng.IPromise<Album[]> {
        //    var songIdsCsv = songIds.join(",");
        //    if (songIdsCsv.length === 0) {
        //        return this.$q.resolve<Album[]>([]);
        //    }

        //    var args = {
        //        songIdsCsv: songIdsCsv
        //    };
        //    return this.httpApi.query("/api/albums/GetAlbumsForSongs", args, AlbumApiService.albumArraySelector);
        //}

        deleteAlbum(albumId: string): ng.IPromise<any> {
            var args = {
                albumId: albumId
            };
            return this.httpApi.postUriEncoded("/api/albums/delete", args);
        }

        static albumSelector(serverObj: Server.IAlbum | null): Album | null {
            if (serverObj) {
                return new Album(serverObj);
            } 

            return null;
        }

        static albumArraySelector(serverObjs: Server.IAlbum[]): Album[] {
            return serverObjs.map(s => AlbumApiService.albumSelector(s)!);
        }

        static albumPagedListSelector(serverObj: Server.IPagedList<Server.IAlbum>): Server.IPagedList<Album> {
            return {
                items: AlbumApiService.albumArraySelector(serverObj.items),
                skip: serverObj.skip,
                take: serverObj.take,
                total: serverObj.total
            }
        }
    }

    App.service("albumApi", AlbumApiService);
}