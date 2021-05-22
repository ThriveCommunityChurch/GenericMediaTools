namespace DupeCleaner
{
    public class AppSettings
    {
       /// <summary>
       /// Whether or not to enforce uniqueness across similar image extensions (specifically .jpg/.jpeg)
       /// </summary>
       public string EnforceImageExtensions { get; set; }
    }
}