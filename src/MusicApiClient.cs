using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OnePicVideo;

public class MusicApiClient
{
    private const string MODULUS = "00e0b509f6259df8642dbc35662901477df22677ec152b5ff68ace615bb7b725152b3ab17a876aea8a5aa76d2e417629ec4ee341f56135fccf695280104e0312ecbda92557c93870114af6c9d05c4f7f0c3685b7a46bee255932575cce10b424d813cfe4875d3e82047b97ddef52741d546b8e289dc6935b3ece0462db0a22b8e7";
    private const string NONCE = "0CoJUm6Qyw8W8jud";
    private const string PUBKEY = "010001";
    private const string VI = "0102030405060708";
    private const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.132 Safari/537.36";
    private const string REFERER = "https://music.163.com/";

    public async Task<SongInfo?> Search163Async(string urlOrId, CancellationToken ct = default)
    {
        var songId = Extract163Id(urlOrId);
        if (songId == null) return null;

        try
        {
            var body = $"{{\"c\":\"[{{\\\"id\\\":\\\"{songId}\\\"}}]\",\"csrf_token\":\"\"}}";
            var resp = await WeapiPostAsync("https://music.163.com/weapi/v3/song/detail?csrf_token=", body);

            using var doc = JsonDocument.Parse(resp);
            var root = doc.RootElement;

            if (root.TryGetProperty("code", out var code) && code.GetInt32() != 200)
                return null;

            if (root.TryGetProperty("songs", out var songs) && songs.GetArrayLength() > 0)
            {
                var song = songs[0];
                var album = song.GetProperty("al");
                var artist = song.GetProperty("ar")[0];

                var picUrl = album.TryGetProperty("picUrl", out var pu) && !string.IsNullOrEmpty(pu.GetString())
                    ? pu.GetString()!
                    : $"https://p1.music.126.net/{album.GetProperty("pic").GetInt64()}/{album.GetProperty("pic").GetInt64()}.jpg";

                return new SongInfo
                {
                    Title = song.GetProperty("name").GetString() ?? "",
                    Artist = artist.GetProperty("name").GetString() ?? "",
                    Album = album.GetProperty("name").GetString() ?? "",
                    CoverUrl = picUrl,
                    SongId = songId
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API Error: {ex.Message}");
        }
        return null;
    }

    public async Task<string> DownloadCoverAsync(string coverUrl, string saveDir, string? customName = null, CancellationToken ct = default)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Add("User-Agent", USER_AGENT);
            client.DefaultRequestHeaders.Add("Referer", REFERER);

            var data = await client.GetByteArrayAsync(coverUrl, ct);

            var ext = ".jpg";
            if (coverUrl.Contains(".png")) ext = ".png";
            else if (coverUrl.Contains(".webp")) ext = ".webp";

            var fileName = !string.IsNullOrWhiteSpace(customName)
                ? SanitizeFileName(customName) + ext
                : $"cover_{DateTime.Now:yyyyMMddHHmmss}{ext}";

            var savePath = Path.Combine(saveDir, fileName);
            await File.WriteAllBytesAsync(savePath, data, ct);
            return savePath;
        }
        catch
        {
            return "";
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid) name = name.Replace(c, '_');
        name = new string(name.Where(c => c != '?').ToArray());
        if (name.Length > 120) name = name[..120];
        return name.Trim();
    }

    private async Task<string> WeapiPostAsync(string url, string body)
    {
        var secretKey = GenerateSecretKey(16);
        var encSecKey = RsaEncode(secretKey);

        var @params = AesEncode(body, NONCE);
        @params = AesEncode(@params, secretKey);

        var postData = $"params={Uri.EscapeDataString(@params)}&encSecKey={Uri.EscapeDataString(encSecKey)}";

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Add("User-Agent", USER_AGENT);
        client.DefaultRequestHeaders.Add("Referer", REFERER);
        client.DefaultRequestHeaders.Add("Cookie", "NMTID=" + Guid.NewGuid().ToString("N"));

        using var content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync(url, content, CancellationToken.None);
        return await response.Content.ReadAsStringAsync();
    }

    private static string AesEncode(string data, string key)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(VI);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var input = Encoding.UTF8.GetBytes(data);
        var output = encryptor.TransformFinalBlock(input, 0, input.Length);
        return Convert.ToBase64String(output);
    }

    private static string RsaEncode(string text)
    {
        var reversed = new string(text.Reverse().ToArray());
        var hex = BitConverter.ToString(Encoding.UTF8.GetBytes(reversed)).Replace("-", "").ToLower();
        var a = BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
        var b = BigInteger.Parse(PUBKEY, System.Globalization.NumberStyles.HexNumber);
        var c = BigInteger.Parse(MODULUS, System.Globalization.NumberStyles.HexNumber);
        var key = BigInteger.ModPow(a, b, c).ToString("x").PadLeft(256, '0');
        return key.Length > 256 ? key[^256..] : key;
    }

    private static string GenerateSecretKey(int length)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
            sb.Append(chars[Random.Shared.Next(chars.Length)]);
        return sb.ToString();
    }

    public static string? Extract163Id(string urlOrId)
    {
        if (long.TryParse(urlOrId.Trim(), out var id)) return id.ToString();
        var match = System.Text.RegularExpressions.Regex.Match(urlOrId, @"id=(\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}

public class SongInfo
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string CoverUrl { get; set; } = "";
    public string SongId { get; set; } = "";
    public override string ToString() => $"{Title} - {Artist}";
}
