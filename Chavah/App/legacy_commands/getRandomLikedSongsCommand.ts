import CommandBase = require("commands/commandBase");
import Song = require("models/song");

class GetRandomLikedSongsCommand extends CommandBase {
    constructor(private count) {
        super();
    }

    execute(): JQueryPromise<Song[]> {
        var selector = (dtos: SongDto[]) => dtos.map(d => new Song(d));
        return this.query("/api/likes/random/" + this.count, null, selector);
    }
}

export = GetRandomLikedSongsCommand;  