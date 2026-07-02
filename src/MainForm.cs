namespace OnePicVideo;

public class MainForm : Form
{
    private readonly FfmpegEncoder _encoder = new();
    private readonly MusicApiClient _musicApi = new();
    private CancellationTokenSource? _cts;
    private readonly List<string> _tempFiles = new();

    private TabControl _tabs = null!;

    private TextBox _txtImage = null!, _txtAudio = null!, _txtOutput = null!;
    private ComboBox _cmbEncoder = null!, _cmbPreset = null!, _cmbResolution = null!;
    private NumericUpDown _numQuality = null!, _numBitrate = null!;
    private CheckBox _chkCopyAudio = null!;
    private Button _btnStart = null!, _btnCancel = null!;
    private ProgressBar _progress = null!;
    private RichTextBox _rtbLog = null!;

    private TextBox _txtSearch = null!;
    private Button _btnSearch = null!, _btnUseCover = null!, _btnDownCover = null!;
    private PictureBox _picCover = null!;
    private Label _lblSongInfo = null!;
    private SongInfo? _currentSong;

    public MainForm()
    {
        InitializeComponent();
        _encoder.AutoDetect();
        PopulateEncoders();
        FormClosing += (s, e) => CleanupTempFiles();
    }

    private void InitializeComponent()
    {
        Text = "一张图 · 压制MP4";
        Size = new Size(680, 570);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Font = new Font("Microsoft YaHei UI", 9F);

        _tabs = new TabControl { Location = new Point(6, 6), Size = new Size(656, 522) };
        var tab1 = new TabPage("一图流压制");
        var tab2 = new TabPage("搜索封面");
        BuildTab1(tab1);
        BuildTab2(tab2);
        _tabs.TabPages.Add(tab1);
        _tabs.TabPages.Add(tab2);
        Controls.Add(_tabs);
    }

    private void BuildTab1(TabPage tab)
    {
        var x1 = 12;
        var w = 636;
        var y = 10;

        var grp1 = new GroupBox { Text = "输入文件", Location = new Point(x1, y), Size = new Size(w, 120) };
        {
            var l1 = new Label { Text = "图片:", Location = new Point(12, 24), Size = new Size(45, 23), TextAlign = ContentAlignment.MiddleRight };
            _txtImage = new TextBox { Location = new Point(62, 24), Size = new Size(450, 23) };
            _txtImage.AllowDrop = true;
            _txtImage.DragEnter += (s, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            _txtImage.DragDrop += (s, e) => { var f = (string[])e.Data!.GetData(DataFormats.FileDrop)!; if (f.Length > 0) { _txtImage.Text = f[0]; AutoOutput(); } };
            var b1 = new Button { Text = "浏览...", Location = new Point(520, 23), Size = new Size(90, 25) };
            b1.Click += (s, e) => BrowseImage();
            var l2 = new Label { Text = "音频:", Location = new Point(12, 54), Size = new Size(45, 23), TextAlign = ContentAlignment.MiddleRight };
            _txtAudio = new TextBox { Location = new Point(62, 54), Size = new Size(450, 23) };
            _txtAudio.AllowDrop = true;
            _txtAudio.DragEnter += (s, e) => { if (e.Data!.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            _txtAudio.DragDrop += (s, e) => { var f = (string[])e.Data!.GetData(DataFormats.FileDrop)!; if (f.Length > 0) { _txtAudio.Text = f[0]; AutoOutput(); } };
            var b2 = new Button { Text = "浏览...", Location = new Point(520, 53), Size = new Size(90, 25) };
            b2.Click += (s, e) => BrowseAudio();
            var l3 = new Label { Text = "输出:", Location = new Point(12, 84), Size = new Size(45, 23), TextAlign = ContentAlignment.MiddleRight };
            _txtOutput = new TextBox { Location = new Point(62, 84), Size = new Size(450, 23) };
            var b3 = new Button { Text = "浏览...", Location = new Point(520, 83), Size = new Size(90, 25) };
            b3.Click += (s, e) => BrowseOutput();
            grp1.Controls.AddRange(new Control[] { l1, _txtImage, b1, l2, _txtAudio, b2, l3, _txtOutput, b3 });
        }
        y += 128;

        var grp2 = new GroupBox { Text = "压制设置", Location = new Point(x1, y), Size = new Size(w, 80) };
        {
            var le = new Label { Text = "编码器:", Location = new Point(12, 24), Size = new Size(52, 23), TextAlign = ContentAlignment.MiddleRight };
            _cmbEncoder = new ComboBox { Location = new Point(68, 24), Size = new Size(180, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbEncoder.SelectedIndexChanged += (s, e) => OnEncoderChanged();

            var lp = new Label { Text = "预设:", Location = new Point(260, 24), Size = new Size(40, 23), TextAlign = ContentAlignment.MiddleRight };
            _cmbPreset = new ComboBox { Location = new Point(304, 24), Size = new Size(160, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbPreset.SelectedIndexChanged += (s, e) => SyncQualityToPreset();

            var lq = new Label { Text = "画质:", Location = new Point(476, 24), Size = new Size(40, 23), TextAlign = ContentAlignment.MiddleRight };
            _numQuality = new NumericUpDown { Location = new Point(518, 24), Size = new Size(55, 23), Minimum = 0, Maximum = 51, Value = 23 };

            var lr = new Label { Text = "分辨率:", Location = new Point(12, 52), Size = new Size(52, 23), TextAlign = ContentAlignment.MiddleRight };
            _cmbResolution = new ComboBox { Location = new Point(68, 52), Size = new Size(120, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbResolution.Items.AddRange(new[] { "原始", "1920x1080", "1280x720", "720x480" });
            _cmbResolution.SelectedIndex = 0;

            var la = new Label { Text = "音频码率:", Location = new Point(200, 52), Size = new Size(60, 23), TextAlign = ContentAlignment.MiddleRight };
            _numBitrate = new NumericUpDown { Location = new Point(264, 52), Size = new Size(65, 23), Minimum = 32, Maximum = 512, Value = 192, Increment = 32 };
            _chkCopyAudio = new CheckBox { Text = "复制原始音频", Location = new Point(340, 54), Size = new Size(110, 20) };
            _chkCopyAudio.CheckedChanged += (s, e) => _numBitrate.Enabled = !_chkCopyAudio.Checked;
            grp2.Controls.AddRange(new Control[] { le, _cmbEncoder, lp, _cmbPreset, lq, _numQuality, lr, _cmbResolution, la, _numBitrate, _chkCopyAudio });
        }
        y += 88;

        _btnStart = new Button { Text = "▶  开始压制", Location = new Point(x1, y), Size = new Size(130, 34), FlatStyle = FlatStyle.System };
        _btnStart.Click += async (s, e) => await StartEncode();
        _btnCancel = new Button { Text = "取消", Location = new Point(150, y), Size = new Size(70, 34) };
        _btnCancel.Click += (s, e) => _cts?.Cancel();
        _progress = new ProgressBar { Location = new Point(230, y + 6), Size = new Size(418, 23) };
        _progress.Visible = false;
        y += 42;

        _rtbLog = new RichTextBox
        {
            Location = new Point(x1, y), Size = new Size(w, 148),
            ReadOnly = true, BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Consolas", 8.5F), BorderStyle = BorderStyle.FixedSingle
        };

        tab.Controls.AddRange(new Control[] { grp1, grp2, _btnStart, _btnCancel, _progress, _rtbLog });
    }

    private void BuildTab2(TabPage tab)
    {
        var x1 = 12;
        var w = 636;
        var y = 10;

        var grp1 = new GroupBox { Text = "搜索", Location = new Point(x1, y), Size = new Size(w, 70) };
        {
            var l1 = new Label { Text = "链接:", Location = new Point(12, 26), Size = new Size(40, 23), TextAlign = ContentAlignment.MiddleRight };
            _txtSearch = new TextBox { Location = new Point(56, 26), Size = new Size(440, 23) };
            _txtSearch.PlaceholderText = "https://music.163.com/song?id=769609  或直接输入 769609";
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = DoSearch(); };
            _btnSearch = new Button { Text = "搜索", Location = new Point(504, 25), Size = new Size(100, 25) };
            _btnSearch.Click += async (s, e) => await DoSearch();
            grp1.Controls.AddRange(new Control[] { l1, _txtSearch, _btnSearch });
        }
        y += 78;

        var grp2 = new GroupBox { Text = "结果", Location = new Point(x1, y), Size = new Size(w, 195) };
        {
            _picCover = new PictureBox { Location = new Point(12, 22), Size = new Size(160, 160), SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
            _lblSongInfo = new Label { Location = new Point(185, 28), Size = new Size(435, 100), Text = "搜索歌曲后将显示信息" };
            _btnUseCover = new Button { Text = "填入图片 → 压制页", Location = new Point(185, 150), Size = new Size(155, 28), Enabled = false };
            _btnUseCover.Click += async (s, e) => await UseCover();
            _btnDownCover = new Button { Text = "下载封面到本地", Location = new Point(350, 150), Size = new Size(140, 28), Enabled = false };
            _btnDownCover.Click += async (s, e) => await DownloadCover();
            grp2.Controls.AddRange(new Control[] { _picCover, _lblSongInfo, _btnUseCover, _btnDownCover });
        }
        y += 205;

        var lblHint = new Label
        {
            Text = "提示: 搜索到封面后点击「填入图片 → 压制页」自动填入，退出软件时临时文件自动删除。",
            Location = new Point(x1, y), Size = new Size(w, 20), ForeColor = Color.Gray
        };

        tab.Controls.AddRange(new Control[] { grp1, grp2, lblHint });
    }

    private void BrowseImage()
    {
        using var dlg = new OpenFileDialog { Title = "选择图片", Filter = "图片|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.gif;*.heic;*.heif;*.jxl|所有文件|*.*" };
        if (dlg.ShowDialog() == DialogResult.OK) { _txtImage.Text = dlg.FileName; AutoOutput(); }
    }

    private void BrowseAudio()
    {
        using var dlg = new OpenFileDialog { Title = "选择音频", Filter = "音频|*.mp3;*.wav;*.flac;*.aac;*.ogg;*.opus;*.m4a;*.wma;*.ape|所有文件|*.*" };
        if (dlg.ShowDialog() == DialogResult.OK) { _txtAudio.Text = dlg.FileName; AutoOutput(); }
    }

    private void BrowseOutput()
    {
        using var dlg = new SaveFileDialog { Title = "保存MP4", Filter = "MP4|*.mp4", DefaultExt = "mp4" };
        if (!string.IsNullOrEmpty(_txtAudio.Text))
        { dlg.FileName = Path.GetFileNameWithoutExtension(_txtAudio.Text) + "_pv.mp4"; dlg.InitialDirectory = Path.GetDirectoryName(_txtAudio.Text); }
        if (dlg.ShowDialog() == DialogResult.OK) _txtOutput.Text = dlg.FileName;
    }

    private void AutoOutput()
    {
        if (!string.IsNullOrEmpty(_txtAudio.Text) && string.IsNullOrEmpty(_txtOutput.Text))
        {
            var dir = Path.GetDirectoryName(_txtAudio.Text) ?? ".";
            var name = Path.GetFileNameWithoutExtension(_txtAudio.Text);
            _txtOutput.Text = Path.Combine(dir, name + "_pv.mp4");
        }
    }

    private void PopulateEncoders()
    {
        _cmbEncoder.Items.Clear();
        foreach (var e in _encoder.AvailableEncoders) _cmbEncoder.Items.Add(e);
        if (_cmbEncoder.Items.Count > 0)
        {
            var gpu = _encoder.AvailableEncoders.FirstOrDefault(e => e.IsGpu);
            _cmbEncoder.SelectedItem = gpu ?? _encoder.AvailableEncoders[0];
        }
    }

    private void OnEncoderChanged()
    {
        _cmbPreset.Items.Clear();
        if (_cmbEncoder.SelectedItem is not EncoderInfo info) return;

        if (info.IsGpu && info.Key.Contains("nvenc"))
        {
            _cmbPreset.Items.AddRange(new[] {
                "p1 极速(质量最低)",
                "p2 快速(质量较低)",
                "p3 中速(质量中等)",
                "p4 慢速(质量较好)",
                "p5 较慢(高质量)",
                "p6 很慢(质量很高)",
                "p7 最慢(质量最佳)"
            });
            _cmbPreset.SelectedIndex = 0;
        }
        else if (info.IsGpu)
        {
            _cmbPreset.Items.AddRange(new[] {
                "veryfast 极速(质量最低)",
                "fast 快速(质量较低)",
                "medium 中速(质量中等)",
                "slow 慢速(质量较好)"
            });
            _cmbPreset.SelectedIndex = 1;
        }
        else
        {
            _cmbPreset.Items.AddRange(new[] {
                "ultrafast 极速(质量最低)",
                "superfast 超快(低质量)",
                "veryfast 快速(较低质量)",
                "faster 较快(中等偏下)",
                "fast 快速(中等质量)",
                "medium 中速(平衡推荐)",
                "slow 慢速(较好质量)",
                "slower 较慢(高质量)",
                "veryslow 极慢(质量最佳)"
            });
            _cmbPreset.SelectedIndex = 5;
        }

        _numQuality.Value = decimal.TryParse(info.DefaultQuality, out var q) ? q : 23;
        SyncQualityToPreset();
    }

    private void SyncQualityToPreset()
    {
        if (_cmbEncoder.SelectedItem is not EncoderInfo info) return;
        var presetText = _cmbPreset.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(presetText)) return;
        var key = presetText.Split(' ')[0];

        var quality = info.Key switch
        {
            "h264_nvenc" or "hevc_nvenc" => key switch
            {
                "p1" => 24m, "p2" => 22m, "p3" => 20m,
                "p4" => 18m, "p5" => 16m, "p6" => 15m, "p7" => 14m,
                _ => 18m
            },
            "libx264" or "libx265" => key switch
            {
                "ultrafast" => 26m, "superfast" => 25m, "veryfast" => 24m,
                "faster" => 23m, "fast" => 22m, "medium" => 21m,
                "slow" => 20m, "slower" => 19m, "veryslow" => 18m,
                _ => 21m
            },
            _ => key switch
            {
                "veryfast" => 25m, "fast" => 22m, "medium" => 20m,
                "slow" => 18m, _ => 22m
            }
        };

        _numQuality.Value = quality;
    }

    private async Task DoSearch()
    {
        var input = _txtSearch.Text.Trim();
        if (string.IsNullOrEmpty(input)) { MessageBox.Show("请输入歌曲链接或ID", "提示"); return; }

        _btnSearch.Enabled = false; _btnSearch.Text = "搜索中...";
        try
        {
            var song = await _musicApi.Search163Async(input);
            if (song == null) { MessageBox.Show("未找到歌曲信息", "提示"); return; }
            _currentSong = song;
            _lblSongInfo.Text = $"歌曲: {song.Title}\n歌手: {song.Artist}\n专辑: {song.Album}";
            _btnUseCover.Enabled = true;
            _btnDownCover.Enabled = true;

            try
            {
                using var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                hc.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                var data = await hc.GetByteArrayAsync(song.CoverUrl);
                using var ms = new MemoryStream(data);
                _picCover.Image = Image.FromStream(ms);
            }
            catch { _picCover.Image = null; }
        }
        catch (Exception ex) { MessageBox.Show($"搜索失败: {ex.Message}", "错误"); }
        finally { _btnSearch.Enabled = true; _btnSearch.Text = "搜索"; }
    }

    private async Task UseCover()
    {
        if (_currentSong == null) return;
        _btnUseCover.Enabled = false;
        try
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), "OnePicVideo");
            Directory.CreateDirectory(tmpDir);

            var customName = $"{_currentSong.Title} - {_currentSong.Artist} - cover";
            var path = await _musicApi.DownloadCoverAsync(_currentSong.CoverUrl, tmpDir, customName);

            if (!string.IsNullOrEmpty(path))
            {
                _tempFiles.Add(path);
                _txtImage.Text = path;
                AutoOutput();
                Log($"封面已填入(临时): {path}");
            }
        }
        finally { _btnUseCover.Enabled = true; }
    }

    private async Task DownloadCover()
    {
        if (_currentSong == null) return;
        _btnDownCover.Enabled = false;
        try
        {
            var dir = !string.IsNullOrEmpty(_txtAudio.Text)
                ? Path.GetDirectoryName(_txtAudio.Text) ?? "."
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var customName = $"{_currentSong.Title} - {_currentSong.Artist} - cover";
            var path = await _musicApi.DownloadCoverAsync(_currentSong.CoverUrl, dir, customName);
            if (!string.IsNullOrEmpty(path)) MessageBox.Show($"已保存:\n{path}", "完成");
        }
        finally { _btnDownCover.Enabled = true; }
    }

    private void CleanupTempFiles()
    {
        foreach (var f in _tempFiles)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
        try
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), "OnePicVideo");
            if (Directory.Exists(tmpDir) && !Directory.EnumerateFileSystemEntries(tmpDir).Any())
                Directory.Delete(tmpDir);
        }
        catch { }
    }

    private async Task StartEncode()
    {
        if (string.IsNullOrEmpty(_txtImage.Text) || !File.Exists(_txtImage.Text))
        { MessageBox.Show("请选择图片。", "提示"); return; }
        if (string.IsNullOrEmpty(_txtAudio.Text) || !File.Exists(_txtAudio.Text))
        { MessageBox.Show("请选择音频。", "提示"); return; }
        if (string.IsNullOrEmpty(_txtOutput.Text))
        { MessageBox.Show("请指定输出路径。", "提示"); return; }

        var dir = Path.GetDirectoryName(_txtOutput.Text)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        _btnStart.Enabled = false; _btnStart.Text = "压制中...";
        _progress.Visible = true; _progress.Value = 0;
        _rtbLog.Clear();

        _cts = new CancellationTokenSource();

        var info = _cmbEncoder.SelectedItem as EncoderInfo;
        var presetKey = _cmbPreset.SelectedItem?.ToString()?.Split(' ')[0] ?? "medium";
        var res = _cmbResolution.SelectedIndex == 0 ? "" : _cmbResolution.SelectedItem!.ToString()!;
        var resArg = res.Contains('x') ? res.Split('x')[0] + ":" + res.Split('x')[1] : "";

        var opts = new EncodeOptions
        {
            ImagePath = _txtImage.Text,
            AudioPath = _txtAudio.Text,
            OutputPath = _txtOutput.Text,
            Encoder = info?.Key ?? "libx264",
            Preset = presetKey,
            CRF = _numQuality.Value,
            AudioBitrate = _numBitrate.Value,
            CopyAudio = _chkCopyAudio.Checked,
            TargetResolution = resArg
        };

        _encoder.LogReceived += Log;
        _encoder.ProgressChanged += p => Invoke(() => _progress.Value = Math.Min(p, 100));

        try
        {
            await _encoder.EncodeAsync(opts, _cts.Token);
            Log("=== 压制完成 ===");
            _progress.Value = 100;
            MessageBox.Show("压制完成!", "完成");
        }
        catch (OperationCanceledException) { Log("=== 已取消 ==="); }
        catch (Exception ex) { Log($"错误: {ex.Message}"); MessageBox.Show($"失败: {ex.Message}", "错误"); }
        finally
        {
            _btnStart.Enabled = true; _btnStart.Text = "▶  开始压制";
            _encoder.LogReceived -= Log;
            _encoder.ProgressChanged -= p => { };
            _cts?.Dispose(); _cts = null;
        }
    }

    private void Log(string msg)
    {
        if (_rtbLog.InvokeRequired) _rtbLog.Invoke(() => Log(msg));
        else { _rtbLog.AppendText(msg + "\n"); _rtbLog.ScrollToCaret(); }
    }
}
