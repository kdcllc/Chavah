﻿namespace BitShuva.Chavah.Models
{

    public class AppSettings
    {
        public Connectionstrings ConnectionStrings { get; set; }
        public Application Application { get; set; }
        public Logging Logging { get; set; }
        public Email Email { get; set; }
        public Cdn Cdn { get; set; }
        public Filepickr FilePickr { get; set; }
        public Ifttt Ifttt { get; set; }
        public Jwt Jwt { get; set; }
    }

    public class Connectionstrings
    {
        public string RavenConnection { get; set; }
    }

    public class Application
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string DefaultUrl { get; set; }
        public string FacebookUrl { get; set; }
        public string TwitterUrl { get; set; }
        public string Keywords { get; set; }
        public string GoogleAnalytics { get; set; }
        public string TrackJsToken { get; set; }
    }

    public class Logging
    {
        public bool IncludeScopes { get; set; }
        public Loglevel LogLevel { get; set; }
    }

    public class Loglevel
    {
        public string Default { get; set; }
        public string System { get; set; }
        public string Microsoft { get; set; }
    }

    public class Email
    {
        public string SendGridApiKey { get; set; }
    }

    public class Cdn
    {
        public string FtpHost { get; set; }
        public string FtpUserName { get; set; }
        public string FtpPassword { get; set; }
        public string FtpWorkingDirectory { get; set; }
        public string MusicDirectory { get; set; }
        public string AlbumArtDirectory { get; set; }
        public string ArtistImagesDirectory { get; set; }
        public string HttpPath { get; set; }
    }

    public class Filepickr
    {
        public string Key { get; set; }
    }

    public class Ifttt
    {
        public string Key { get; set; }
    }

    public class Jwt
    {
        public string audience { get; set; }
        public string issuer { get; set; }
        public string key { get; set; }
    }

}