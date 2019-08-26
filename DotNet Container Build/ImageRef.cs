namespace DotNet_Container_Build
{
    public class ImageRef
    {
            public string Registry { get; set; }
            public string Repository { get; set; }
            public string Tag { get; set; }
            public string Password { get; set; }
            public string Username { get; set; }
    }
}