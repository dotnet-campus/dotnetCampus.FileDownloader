using System;

namespace dotnetCampus.FileDownloader
{
    public class WebResponseException : Exception
    {
        public WebResponseException(string? message) : base(message)
        {
        }
    }
}