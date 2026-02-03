using System.Net.Http.Headers;

namespace ProjectDoomsdayServer.ApiTests.TestSupport;

public static class TestHelpers
{
    public static MultipartFormDataContent CreateFileUpload(
        string fileName,
        byte[] content,
        string contentType = "application/octet-stream"
    )
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return form;
    }

    public static byte[] GenerateRandomBytes(int size)
    {
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    public static byte[] CreateTextContent(string text) => System.Text.Encoding.UTF8.GetBytes(text);
}
