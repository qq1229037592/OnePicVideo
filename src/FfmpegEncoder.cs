using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OnePicVideo;

public class FfmpegEncoder
{
    public string FfmpegPath { get; private set; } = @"C:\Program Files\FFmpeg\ffmpeg.exe";
    public List<EncoderInfo> AvailableEncoders { get; } = new();
    public string DetectedGpu { get; private set; } = "";

    public event Action<string>? LogReceived;
    public event Action<int>? ProgressChanged;

    public void AutoDetect()
    {
        DetectFfmpeg();
        DetectEncoders();
        DetectGpu();
    }

    private void DetectFfmpeg()
    {
        if (File.Exists(FfmpegPath)) return;
        var envPaths = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? Array.Empty<string>();
        foreach (var p in envPaths)
        {
            var fp = Path.Combine(p.Trim(), "ffmpeg.exe");
            if (File.Exists(fp)) { FfmpegPath = fp; return; }
        }
    }

    private void DetectEncoders()
    {
        AvailableEncoders.Clear();
        AvailableEncoders.Add(new EncoderInfo("libx264", "H.264 (CPU)", false, "medium", "23"));

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = FfmpegPath,
                Arguments = "-hide_banner -encoders",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(1000);

            if (output.Contains("h264_nvenc"))
                AvailableEncoders.Add(new EncoderInfo("h264_nvenc", "H.264 (NVENC GPU)", true, "p1", "18"));
            if (output.Contains("hevc_nvenc"))
                AvailableEncoders.Add(new EncoderInfo("hevc_nvenc", "H.265 (NVENC GPU)", true, "p1", "22"));
            if (output.Contains("h264_qsv"))
                AvailableEncoders.Add(new EncoderInfo("h264_qsv", "H.264 (QSV GPU)", true, "fast", "23"));
            if (output.Contains("hevc_qsv"))
                AvailableEncoders.Add(new EncoderInfo("hevc_qsv", "H.265 (QSV GPU)", true, "fast", "25"));
            if (output.Contains("h264_amf"))
                AvailableEncoders.Add(new EncoderInfo("h264_amf", "H.264 (AMF GPU)", true, "fast", "23"));
            if (output.Contains("hevc_amf"))
                AvailableEncoders.Add(new EncoderInfo("hevc_amf", "H.265 (AMF GPU)", true, "fast", "25"));
            if (output.Contains("h264_vulkan"))
                AvailableEncoders.Add(new EncoderInfo("h264_vulkan", "H.264 (Vulkan)", true, "fast", "23"));
            if (output.Contains("libx265"))
                AvailableEncoders.Add(new EncoderInfo("libx265", "H.265 (CPU)", false, "medium", "22"));
        }
        catch { }
    }

    private void DetectGpu()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name --format=csv,noheader",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                var name = p.StandardOutput.ReadLine();
                p.WaitForExit(2000);
                if (!string.IsNullOrEmpty(name)) DetectedGpu = name.Trim();
            }
        }
        catch { DetectedGpu = ""; }
    }

    public async Task EncodeAsync(EncodeOptions opts, CancellationToken ct = default)
    {
        var codec = opts.Encoder;
        var isGpu = AvailableEncoders.Any(e => e.Key == codec && e.IsGpu);

        var audioArgs = opts.CopyAudio
            ? "-c:a copy"
            : $"-c:a aac -b:a {opts.AudioBitrate}k";

        var vf = "";
        if (!string.IsNullOrEmpty(opts.TargetResolution))
            vf += $",scale={opts.TargetResolution}:force_original_aspect_ratio=1";
        vf += ",pad=ceil(iw/2)*2:ceil(ih/2)*2";

        var codecArgs = BuildCodecArgs(opts, isGpu);
        var extraArgs = opts.Encoder == "libx265" ? "-tag:v hvc1" : "";

        var args = $"-y -loop 1 -i \"{opts.ImagePath}\" -i \"{opts.AudioPath}\" " +
                   $"-c:v {codec} {codecArgs} " +
                   $"{audioArgs} " +
                   $"-pix_fmt yuv420p -vf \"{vf.TrimStart(',')}\" " +
                   $"-shortest -movflags +faststart {extraArgs} " +
                   $"\"{opts.OutputPath}\"";

        LogReceived?.Invoke($"> {FfmpegPath} {args}\n");

        var duration = await GetAudioDuration(opts.AudioPath, ct);
        if (duration == TimeSpan.Zero) duration = TimeSpan.FromMinutes(3);

        await RunFfmpeg(args, duration, ct);
    }

    private static string BuildCodecArgs(EncodeOptions opts, bool isGpu)
    {
        if (!isGpu)
        {
            return $"-tune stillimage -preset {opts.Preset} -crf {opts.CRF}";
        }

        return opts.Encoder switch
        {
            "h264_nvenc" or "hevc_nvenc" =>
                $"-preset {opts.Preset} -tune hq -rc constqp -qp {opts.CRF} -b_ref_mode 0",
            "h264_qsv" or "hevc_qsv" =>
                $"-preset {opts.Preset} -global_quality {opts.CRF} -look_ahead 0",
            "h264_amf" or "hevc_amf" =>
                $"-usage transcoding -quality quality -qp_i {opts.CRF} -qp_p {opts.CRF}",
            "h264_vulkan" =>
                $"-qp {opts.CRF}",
            _ => $"-tune stillimage -preset {opts.Preset} -crf {opts.CRF}"
        };
    }

    private async Task RunFfmpeg(string args, TimeSpan duration, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = FfmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var p = new Process { StartInfo = psi };
        p.Start();

        var totalSeconds = duration.TotalSeconds;

        var readTask = Task.Run(async () =>
        {
            using var reader = p.StandardError;
            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync();
                if (line == null) break;
                LogReceived?.Invoke(line);
                ParseProgress(line, totalSeconds);
            }
        }, ct);

        await p.WaitForExitAsync(ct);
        await readTask;

        if (p.ExitCode != 0)
            throw new Exception($"FFmpeg exited with code {p.ExitCode}");

        ProgressChanged?.Invoke(100);
    }

    private async Task<TimeSpan> GetAudioDuration(string audioPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = FfmpegPath,
            Arguments = $"-i \"{audioPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        using var p = new Process { StartInfo = psi };
        p.Start();
        var error = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);

        var match = Regex.Match(error, @"Duration:\s*(\d+):(\d+):(\d+)\.(\d+)");
        if (match.Success)
        {
            return new TimeSpan(0,
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value),
                int.Parse(match.Groups[4].Value) * 10);
        }
        return TimeSpan.Zero;
    }

    private void ParseProgress(string line, double totalSeconds)
    {
        var match = Regex.Match(line, @"time=(\d+):(\d+):(\d+)\.(\d+)");
        if (match.Success && totalSeconds > 0)
        {
            var ts = new TimeSpan(0,
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value),
                int.Parse(match.Groups[4].Value) * 10);
            var pct = (int)Math.Min(99, ts.TotalSeconds / totalSeconds * 100);
            ProgressChanged?.Invoke(pct);
        }
    }
}

public class EncoderInfo
{
    public string Key { get; }
    public string Display { get; }
    public bool IsGpu { get; }
    public string DefaultPreset { get; }
    public string DefaultQuality { get; }

    public EncoderInfo(string key, string display, bool isGpu, string defaultPreset, string defaultQuality)
    {
        Key = key;
        Display = display;
        IsGpu = isGpu;
        DefaultPreset = defaultPreset;
        DefaultQuality = defaultQuality;
    }

    public override string ToString() => Display;
}

public class EncodeOptions
{
    public string ImagePath { get; set; } = "";
    public string AudioPath { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string Encoder { get; set; } = "libx264";
    public string Preset { get; set; } = "medium";
    public decimal CRF { get; set; } = 23;
    public decimal AudioBitrate { get; set; } = 192;
    public bool CopyAudio { get; set; }
    public string TargetResolution { get; set; } = "";
}
