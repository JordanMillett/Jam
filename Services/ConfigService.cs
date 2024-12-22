public class ConfigService
{
    public string? ApiUrl { get; set; }
    
    public string FullImageURL(string FileName)
    {
        return ApiUrl + "/api/get/image/" + FileName;
    }
}