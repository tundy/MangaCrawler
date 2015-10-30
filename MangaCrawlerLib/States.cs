namespace MangaCrawlerLib
{
    public enum ServerState
    {
        Initial,
        Waiting,
        Downloading,
        Downloaded,
        Error
    }

    public enum SerieState
    {
        Initial,
        Waiting,
        Downloading,
        Downloaded, 
        Error
    }

    public enum ChapterState
    {
        Initial,
        Cancelled,
        Waiting,
        DownloadingPagesList,
        DownloadingPages,
        Cancelling,
        Zipping,
        Downloaded,
        Error
    }

    public enum PageState
    {
        Initial, 
        Waiting,
        Downloading, 
        Downloaded, 
        Error
    }
}
