using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System;
using System.Diagnostics; // For Debug.WriteLine
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using System.Linq;

namespace VideoTools
{
    public sealed partial class MainWindow : Window
    {

        private List<string> selectedVideoPaths = new List<string>();
        private bool isProcessing = false;

        public MainWindow()
        {
            Debug.WriteLine("MainWindow constructor started.");

            try
            {
                this.InitializeComponent();
                Debug.WriteLine("InitializeComponent completed.");

                // Set dark title bar and custom title in dark mode only
                AppWindow appWindow = this.AppWindow;
                if (appWindow != null)
                {
                    appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 1000)); // Set width and height
                    var titleBar = appWindow.TitleBar;
                    titleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;

                    // Apply custom title bar colors only in dark mode
                    if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
                    {
                        titleBar.BackgroundColor = Windows.UI.Color.FromArgb(255, 32, 32, 32); // Dark gray
                        titleBar.ForegroundColor = Windows.UI.Color.FromArgb(255, 200, 200, 200); // Light text
                        titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(255, 32, 32, 32); // Match buttons
                        titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 200, 200, 200);
                        titleBar.InactiveBackgroundColor = Windows.UI.Color.FromArgb(255, 64, 64, 64); // Slightly lighter when inactive
                        titleBar.InactiveForegroundColor = Windows.UI.Color.FromArgb(255, 200, 200, 200);
                    }
                }

                // Populate ComboBoxes with options
                cb_encoding.ItemsSource = new List<string> { "h264.mp4", "h265.mp4" };
                cb_encoding.SelectedIndex = 0;

                cb_quality.ItemsSource = new List<string> { "lossless", "visual LL", "default", "rough", "blocky", "worst" };
                cb_quality.SelectedIndex = 2;

                cb_volume.ItemsSource = new List<string> { "copy", "0", "0.25", "0.5", "1", "2", "3", "4", "8", "16", "32" };
                cb_volume.SelectedIndex = 0;

                cb_sound_fx.ItemsSource = new List<string> { "none", "loud_norm", "down_bass", "down_treble", "speech_low", "speech_wide", "speech_high" };
                cb_sound_fx.SelectedIndex = 0;

                cb_size.ItemsSource = new List<string> { "keep", "240", "360", "480", "640", "720", "1080", "1440", "2160", "4320" };
                cb_size.SelectedIndex = 0;

                cb_crop_offset.ItemsSource = new List<string> { "none", "start", "end", "N", "S", "E", "W", "NW", "NE", "SE", "SW" };
                cb_crop_offset.SelectedIndex = 0;

                cb_crop_ar.ItemsSource = new List<string> { "keep", "1:1", "16:9", "4:3", "9:16", "3:4" };
                cb_crop_ar.SelectedIndex = 0;

                cb_rotate.ItemsSource = new List<string> { "none", "90", "180", "270" };
                cb_rotate.SelectedIndex = 0;

                cb_flip.ItemsSource = new List<string> { "none", "horizontal", "vertical" };
                cb_flip.SelectedIndex = 0;

                cb_stab_frames.ItemsSource = new List<string> { "none", "5", "10", "15", "30", "50", "100", "200" };
                cb_stab_frames.SelectedIndex = 0;

                cb_stab_zoom.ItemsSource = new List<string> { "auto", "none", "5", "10", "15", "20", "30", "40", "50", "60", "70", "80", "90", "100" };
                cb_stab_zoom.SelectedIndex = 0;

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in InitializeComponent: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            // Check initialization of all controls
            Debug.WriteLine("Checking control initialization...");
            CheckControlInitialization(tb_feedback, "tb_feedback");
            CheckControlInitialization(tb_files_text, "tb_files_text");
            CheckControlInitialization(cb_encoding, "cb_encoding");
            CheckControlInitialization(cb_quality, "cb_quality");
            CheckControlInitialization(cb_volume, "cb_volume");
            CheckControlInitialization(cb_sound_fx, "cb_sound_fx");
            CheckControlInitialization(cb_stab_frames, "cb_stab_frames");
            CheckControlInitialization(cb_stab_zoom, "cb_stab_zoom");
            CheckControlInitialization(cb_size, "cb_size");
            CheckControlInitialization(cb_crop_offset, "cb_crop_offset");
            CheckControlInitialization(cb_crop_ar, "cb_crop_ar");
            CheckControlInitialization(cb_rotate, "cb_rotate");
            CheckControlInitialization(cb_flip, "cb_flip");
            CheckControlInitialization(rb_scale, "rb_scale");
            CheckControlInitialization(rb_crop, "rb_crop");
            CheckControlInitialization(btn_files_select, "btn_files_select");
            CheckControlInitialization(btn_start, "btn_start");
            CheckControlInitialization(btn_reset, "btn_reset");
            CheckControlInitialization(btn_stop, "btn_stop");

            Debug.WriteLine("MainWindow constructor completed.");
        }

        private async Task<string> GenerateFFmpegCommandAsync(string videoPath)
        {
            // Ensure FFmpeg is available
            string ffmpegPath = "ffmpeg"; // Assumes FFmpeg is in PATH; otherwise, specify the full path

            // Temporary files for analysis passes
            string tempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos", "temp");
            Directory.CreateDirectory(tempDir);
            string vidstabDataFile = Path.Combine(tempDir, "VideoTools_vidstabdetect.trf");
            string loudnormDataFile = Path.Combine(tempDir, "VideoTools_loudnorm.trf");

            // Encoding settings
            string codec = cb_encoding.SelectedItem?.ToString() == "h265.mp4" ? "libx265" : "libx264";
            string quality = cb_quality.SelectedItem?.ToString() ?? "default";
            var crfValues = new Dictionary<string, Dictionary<string, string>>
    {
        { "h264.mp4", new Dictionary<string, string> { { "lossless", "0" }, { "visual LL", "17" }, { "default", "23" }, { "rough", "28" }, { "blocky", "35" }, { "worst", "50" } } },
        { "h265.mp4", new Dictionary<string, string> { { "lossless", "0" }, { "visual LL", "20" }, { "default", "28" }, { "rough", "32" }, { "blocky", "40" }, { "worst", "50" } } }
    };
            string crf = crfValues[cb_encoding.SelectedItem?.ToString()][quality];
            string fileTagEnc = $"_{codec}_q{crf}.mp4";

            // Check file extension and adjust volume if necessary
            string volume = cb_volume.SelectedItem?.ToString() ?? "copy";
            string audioCodec = "aac";
            if (volume == "copy")
            {
                bool isMp4 = Path.GetExtension(videoPath).ToLower() == ".mp4";
                if (!isMp4)
                {
                    volume = "1"; // Force re-encoding to AAC if not MP4
                }
            }

            // Audio settings
            string soundFx = cb_sound_fx.SelectedItem?.ToString() ?? "none";
            string opAF = "";
            string fileTagAF = "";
            if (volume != "copy")
            {
                opAF = $",volume={volume}";
                fileTagAF = $"_vol{volume}";
            }
            if (soundFx != "none")
            {
                if (soundFx == "loud_norm")
                {
                    // Run loudnorm analysis pass
                    string loudnormCmd = $"-y -i \"{videoPath}\" -af loudnorm=print_format=summary -f null -";
                    var loudnormResult = await RunFFmpegAsync(ffmpegPath, loudnormCmd, loudnormDataFile);
                    if (!loudnormResult.Success) return $"Error: Loudnorm analysis failed for {Path.GetFileName(videoPath)}";

                    // Parse loudnorm output
                    string loudnormOutput = File.ReadAllText(loudnormDataFile);
                    string measuredI = "-24", measuredLRA = "7", measuredTP = "-2";
                    foreach (var line in loudnormOutput.Split('\n'))
                    {
                        if (line.Contains("Output Integrated:")) measuredI = line.Split(':')[1].Trim().Split(' ')[0];
                        else if (line.Contains("Output LRA:")) measuredLRA = line.Split(':')[1].Trim().Split(' ')[0];
                        else if (line.Contains("Output True Peak:")) measuredTP = line.Split(':')[1].Trim().Split(' ')[0];
                    }
                    opAF += $",loudnorm=measured_I={measuredI}:measured_LRA={measuredLRA}:measured_TP={measuredTP}";
                    fileTagAF += "_loudnorm";
                }
                else
                {
                    var soundFxOptions = new Dictionary<string, (string, string)>
            {
                { "down_bass", (",highpass=f=200", "_basslow") },
                { "down_treble", (",lowpass=f=3000", "_treblelow") },
                { "speech_low", (",highpass=f=200,lowpass=f=2000", "_speechlow") },
                { "speech_wide", (",highpass=f=200,lowpass=f=5000", "_speechwide") },
                { "speech_high", (",highpass=f=800,lowpass=f=5000", "_speechhigh") }
            };
                    var (op, tag) = soundFxOptions[soundFx];
                    opAF += op;
                    fileTagAF += tag;
                }
            }
            if (!string.IsNullOrEmpty(opAF)) opAF = opAF.Substring(1);
            audioCodec = (volume == "copy" && Path.GetExtension(videoPath).ToLower() == ".mp4") ? "copy" : "aac";

            // Video settings
            string opVF = "";
            string fileTagVF = "";

            // Stabilization
            string smoothing = cb_stab_frames.SelectedItem?.ToString() ?? "none";
            string stabZoom = cb_stab_zoom.SelectedItem?.ToString() ?? "auto";
            if (smoothing != "none")
            {
                // Run vidstabdetect analysis pass
                string vidstabCmd = $"-y -i \"{videoPath}\" -vf \"vidstabdetect=result={vidstabDataFile}\" -f null -";
                var vidstabResult = await RunFFmpegAsync(ffmpegPath, vidstabCmd);
                if (!vidstabResult.Success) return $"Error: Vidstabdetect analysis failed for {Path.GetFileName(videoPath)}";

                string zoomOption = stabZoom == "auto" ? "" : $":zoom={stabZoom}";
                opVF += $",vidstabtransform=optzoom={(stabZoom == "auto" ? 1 : 0)}{zoomOption}:smoothing={smoothing}:crop=black:input={vidstabDataFile},unsharp=5:5:0.8:3:3:0.4";
                fileTagVF += $"_dsh{smoothing}z{(stabZoom == "auto" ? "auto" : stabZoom)}";
            }

            // Cropping and Scaling
            string cropSize = cb_size.SelectedItem?.ToString() ?? "keep";
            string cropOffset = cb_crop_offset.SelectedItem?.ToString() ?? "none";
            string cropAR = cb_crop_ar.SelectedItem?.ToString() ?? "keep";
            if (cropSize != "keep" || cropAR != "keep")
            {
                string scaleFilter = "";
                string cropFilter = "";
                if (cropSize != "keep")
                {
                    string height = cropSize.EndsWith("p") ? cropSize.Replace("p", "") : cropSize;
                    scaleFilter = $"-2:{height}"; // Just the dimensions, no "scale=" prefix here
                }

                // Handle aspect ratio and cropping
                if (cropSize != "keep" || cropAR != "keep")
                {
                    // If CropAR is specified, adjust dimensions to match the aspect ratio
                    if (cropAR != "keep")
                    {
                        string arFilter = "";
                        if (cropAR == "1:1")
                        {
                            arFilter = "crop='min(iw,ih)':'min(iw,ih)'";
                        }
                        else
                        {
                            var arParts = cropAR.Split(':');
                            double ar = double.Parse(arParts[0]) / double.Parse(arParts[1]);
                            arFilter = $"crop='if(gt(iw/ih,{ar}),ih*{ar},iw)':'if(gt(iw/ih,{ar}),ih,iw/{ar})'";
                        }
                        cropFilter = arFilter;
                    }

                    // Ensure even dimensions by trimming if necessary
                    if (!string.IsNullOrEmpty(cropFilter))
                    {
                        cropFilter += ",crop='floor(iw/2)*2':'floor(ih/2)*2'";
                    }
                    else
                    {
                        cropFilter = "crop='floor(iw/2)*2':'floor(ih/2)*2'";
                    }

                    // Apply cropping offset if specified
                    if (cropOffset != "none")
                    {
                        string offsetFilter = "";
                        switch (cropOffset)
                        {
                            case "start":
                                offsetFilter = ":0:0";
                                break;
                            case "end":
                                offsetFilter = ":(iw-ow):(ih-oh)";
                                break;
                            case "N":
                                offsetFilter = ":0:0";
                                break;
                            case "S":
                                offsetFilter = ":0:(ih-oh)";
                                break;
                            case "W":
                                offsetFilter = ":0:0";
                                break;
                            case "E":
                                offsetFilter = ":(iw-ow):0";
                                break;
                            case "NW":
                                offsetFilter = ":0:0";
                                break;
                            case "NE":
                                offsetFilter = ":(iw-ow):0";
                                break;
                            case "SW":
                                offsetFilter = ":0:(ih-oh)";
                                break;
                            case "SE":
                                offsetFilter = ":(iw-ow):(ih-oh)";
                                break;
                        }
                        cropFilter += offsetFilter;
                    }

                    // Combine scale and crop filters
                    if (!string.IsNullOrEmpty(scaleFilter))
                    {
                        opVF += $",scale={scaleFilter}";
                    }
                    if (!string.IsNullOrEmpty(cropFilter))
                    {
                        opVF += $",{cropFilter}";
                    }
                    fileTagVF += $"_crop{cropSize}{cropAR.Replace(":", "x")}";
                    if (cropOffset != "none") fileTagVF += cropOffset;
                }
            }
            // Rotation
            string rotate = cb_rotate.SelectedItem?.ToString() ?? "none";
            if (rotate != "none")
            {
                var rotateOptions = new Dictionary<string, (string, string)>
        {
            { "90", (",transpose=1", "_rot90") },
            { "180", (",transpose=2,transpose=2", "_rot180") },
            { "270", (",transpose=2", "_rot270") }
        };
                var (op, tag) = rotateOptions[rotate];
                opVF += op;
                fileTagVF += tag;
            }

            // Flipping
            string flip = cb_flip.SelectedItem?.ToString() ?? "none";
            if (flip != "none")
            {
                var flipOptions = new Dictionary<string, (string, string)>
        {
            { "horizontal", (",hflip", "_Hflip") },
            { "vertical", (",vflip", "_Vflip") }
        };
                var (op, tag) = flipOptions[flip];
                opVF += op;
                fileTagVF += tag;
            }

            // Construct output filename
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(videoPath);
            string outputFileName = $"{fileNameWithoutExt}{fileTagAF}{fileTagVF}{fileTagEnc}";
            string outputPath = Path.Combine(Path.GetDirectoryName(videoPath), outputFileName);

            // Build FFmpeg command
            string vfArg = string.IsNullOrEmpty(opVF) ? "" : $"-vf \"{opVF.Substring(1)}\"";
            string afArg = string.IsNullOrEmpty(opAF) ? "" : $"-af \"{opAF}\"";
            string cmd = $"-y -i \"{videoPath}\" {vfArg} {afArg} -c:a {audioCodec} -b:a 192k -map_metadata 0 -c:v {codec} -crf {crf} -pix_fmt yuv420p \"{outputPath}\"";

            return cmd;
        }


        private async Task<(bool Success, string Output)> RunFFmpegAsync(string executable, string arguments, string outputFile = null)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var output = new System.Text.StringBuilder();
            var error = new System.Text.StringBuilder();
            process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (!string.IsNullOrEmpty(outputFile) && error.Length > 0)
                File.WriteAllText(outputFile, error.ToString());

            return (process.ExitCode == 0, error.ToString());
        }

        // Helper method to check and log control initialization
        private void CheckControlInitialization(object control, string controlName)
        {
            if (control == null)
            {
                Debug.WriteLine($"{controlName} is null after InitializeComponent!");
            }
            else
            {
                Debug.WriteLine($"{controlName} is initialized successfully.");
            }
        }

        // Event handler for the "Start" button

        private async void Click_btn_Start(object sender, RoutedEventArgs e)
        {
            if (isProcessing || selectedVideoPaths.Count == 0)
            {
                tb_feedback.Text += "\nSelect video files or wait for current processing to finish.";
                return;
            }

            isProcessing = true;
            tb_feedback.Text += "\nStarted processing videos...";

            foreach (var videoPath in selectedVideoPaths)
            {
                string cmd = await GenerateFFmpegCommandAsync(videoPath);
                tb_feedback.Text += $"\n{cmd}";
                if (cmd.StartsWith("Error"))
                {
                    //tb_feedback.Text += $"\n{cmd}";
                    continue;
                }

                tb_feedback.Text += $"\nProcessing {Path.GetFileName(videoPath)}...";
                var result = await RunFFmpegAsync("ffmpeg", cmd);
                if (result.Success)
                    tb_feedback.Text += $"\n✓ Created: {Path.GetFileNameWithoutExtension(videoPath)}";
                else
                    tb_feedback.Text += $"\n⚠ Encoding Failed: {Path.GetFileName(videoPath)}\n{result.Output}";
            }

            isProcessing = false;
            tb_feedback.Text += "\nProcessing completed.";
        }

        // Event handler for the "Select Files" button
        private async void Click_btn_files_select(object sender, RoutedEventArgs e)
        {
            // Initialize the file picker
            var filePicker = new FileOpenPicker
            {
                // Start in the Videos folder
                SuggestedStartLocation = PickerLocationId.VideosLibrary,
                // Allow multiple file selection
                ViewMode = PickerViewMode.List
            };

            // Add common video file types
            filePicker.FileTypeFilter.Add(".mp4");
            filePicker.FileTypeFilter.Add(".mkv");
            filePicker.FileTypeFilter.Add(".avi");
            filePicker.FileTypeFilter.Add(".mov");
            filePicker.FileTypeFilter.Add(".wmv");
            filePicker.FileTypeFilter.Add(".flv");
            filePicker.FileTypeFilter.Add(".webm");
            filePicker.FileTypeFilter.Add(".mpeg");
            filePicker.FileTypeFilter.Add(".mpg");
            filePicker.FileTypeFilter.Add(".ts");

            // Initialize the picker with the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

            // Show the file picker and get the selected files
            var files = await filePicker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                // Ensure all files are from the same folder
                var firstFileFolder = files[0].Path.Substring(0, files[0].Path.LastIndexOf('\\'));
                bool allSameFolder = files.All(file => file.Path.Substring(0, file.Path.LastIndexOf('\\')) == firstFileFolder);

                if (!allSameFolder)
                {
                    tb_files_text.Text = "Please select files from the same folder.";
                    selectedVideoPaths.Clear();
                    return;
                }

                // Store the selected file paths
                selectedVideoPaths = files.Select(file => file.Path).ToList();
                tb_files_text.Text = $"Selected {selectedVideoPaths.Count} videos from {firstFileFolder}";
            }
            else
            {
                tb_files_text.Text = "No videos selected.";
                selectedVideoPaths.Clear();
            }
        }

        // Event handler for the "RESET" button
        private void Click_btn_ResetParams(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Click_btn_ResetParams triggered.");
            if (tb_feedback != null)
            {
                tb_feedback.Text += "\nResetting parameters...";
                Debug.WriteLine("tb_feedback updated successfully in Click_btn_ResetParams.");
            }
            else
            {
                Debug.WriteLine("tb_feedback is null in Click_btn_ResetParams.");
            }

            // Reset ComboBox selections to default values with null checks
            if (cb_encoding != null) cb_encoding.SelectedItem = "h264.mp4"; else Debug.WriteLine("cb_encoding is null in Click_btn_ResetParams.");
            if (cb_quality != null) cb_quality.SelectedItem = "VISUAL LL"; else Debug.WriteLine("cb_quality is null in Click_btn_ResetParams.");
            if (cb_volume != null) cb_volume.SelectedItem = "1"; else Debug.WriteLine("cb_volume is null in Click_btn_ResetParams.");
            if (cb_sound_fx != null) cb_sound_fx.SelectedItem = "none"; else Debug.WriteLine("cb_sound_fx is null in Click_btn_ResetParams.");
            if (cb_stab_frames != null) cb_stab_frames.SelectedItem = "10"; else Debug.WriteLine("cb_stab_frames is null in Click_btn_ResetParams.");
            if (cb_stab_zoom != null) cb_stab_zoom.SelectedItem = "auto"; else Debug.WriteLine("cb_stab_zoom is null in Click_btn_ResetParams.");
            if (cb_size != null) cb_size.SelectedItem = "none"; else Debug.WriteLine("cb_size is null in Click_btn_ResetParams.");
            if (rb_scale != null) rb_scale.IsChecked = true; else Debug.WriteLine("rb_scale is null in Click_btn_ResetParams."); // Default to Scale
            if (cb_crop_offset != null) cb_crop_offset.SelectedItem = "none"; else Debug.WriteLine("cb_crop_offset is null in Click_btn_ResetParams.");
            if (cb_crop_ar != null) cb_crop_ar.SelectedItem = "keep"; else Debug.WriteLine("cb_crop_ar is null in Click_btn_ResetParams.");
            if (cb_rotate != null) cb_rotate.SelectedItem = "none"; else Debug.WriteLine("cb_rotate is null in Click_btn_ResetParams.");
            if (cb_flip != null) cb_flip.SelectedItem = "none"; else Debug.WriteLine("cb_flip is null in Click_btn_ResetParams.");
        }

        // Event handler for the "STOP" button
        private void Click_btn_Stop(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Click_btn_Stop triggered.");
            if (tb_feedback != null)
            {
                //tb_feedback.Text += "\nStopped processing.";
                Debug.WriteLine("tb_feedback updated successfully in Click_btn_Stop.");
            }
            else
            {
                Debug.WriteLine("tb_feedback is null in Click_btn_Stop.");
            }
        }

        // Event handlers for ComboBox selection changes
        private void Changed_cb_encoding(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Changed_cb_encoding triggered.");
            if (cb_encoding != null && cb_encoding.SelectedItem != null && tb_feedback != null)
            {
                //tb_feedback.Text += $"\nCodec changed to: {cb_encoding.SelectedItem}";
                Debug.WriteLine("tb_feedback updated successfully in Changed_cb_encoding.");
            }
            else
            {
                Debug.WriteLine("cb_encoding or tb_feedback is null in Changed_cb_encoding.");
            }
        }

        private void Changed_cb_quality(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Changed_cb_quality triggered.");
            if (cb_quality != null && cb_quality.SelectedItem != null && tb_feedback != null)
            {
                //tb_feedback.Text += $"\nQuality changed to: {cb_quality.SelectedItem}";
                Debug.WriteLine("tb_feedback updated successfully in Changed_cb_quality.");
            }
            else
            {
                Debug.WriteLine("cb_quality or tb_feedback is null in Changed_cb_quality.");
            }
        }

        private void Changed_cb_volume(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Changed_cb_volume triggered.");
            if (cb_volume != null && cb_volume.SelectedItem != null && tb_feedback != null)
            {
                //tb_feedback.Text += $"\nVolume changed to: {cb_volume.SelectedItem}";
                Debug.WriteLine("tb_feedback updated successfully in Changed_cb_volume.");
            }
            else
            {
                Debug.WriteLine("cb_volume or tb_feedback is null in Changed_cb_volume.");
            }
        }

        private void Changed_cb_sound_fx(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Changed_cb_sound_fx triggered.");
            if (cb_sound_fx != null && cb_sound_fx.SelectedItem != null && tb_feedback != null)
            {
                //tb_feedback.Text += $"\nSoundFX changed to: {cb_sound_fx.SelectedItem}";
                Debug.WriteLine("tb_feedback updated successfully in Changed_cb_sound_fx.");
            }
            else
            {
                Debug.WriteLine("cb_sound_fx or tb_feedback is null in Changed_cb_sound_fx.");
            }
        }

        private void Changed_cb_stab_frames(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Changed_cb_stab_frames triggered.");
            if (cb_stab_frames != null && cb_stab_frames.SelectedItem != null && tb_feedback != null)
            {
                //tb_feedback.Text += $"\nStrength changed to: {cb_stab_frames.SelectedItem}";
                Debug.WriteLine("tb_feedback updated successfully in Changed_cb_stab_frames.");
            }
            else
            {
                Debug.WriteLine("cb_stab_frames or tb_feedback is null in Changed_cb_stab_frames.");
            }
        }

        private void Changed_cb_stab_zoom(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Changed_cb_stab_zoom triggered.");
            if (cb_stab_zoom != null && cb_stab_zoom.SelectedItem != null && tb_feedback != null)
            {
                //tb_feedback.Text += $"\nZoom % changed to: {cb_stab_zoom.SelectedItem}";
                Debug.WriteLine("tb_feedback updated successfully in Changed_cb_stab_zoom.");
            }
            else
            {
                Debug.WriteLine("cb_stab_zoom or tb_feedback is null in Changed_cb_stab_zoom.");
            }
        }

        private void Changed_cb_size(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Changed_cb_size triggered.");
            if (cb_size != null && cb_size.SelectedItem != null && tb_feedback != null)
            {
                //tb_feedback.Text += $"\nSize changed to: {cb_size.SelectedItem}";
                Debug.WriteLine("tb_feedback updated successfully in Changed_cb_size.");
            }
            else
            {
                Debug.WriteLine("cb_size or tb_feedback is null in Changed_cb_size.");
            }
        }

        private void Changed_cb_crop_offset(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Changed_cb_crop_offset triggered.");
            if (cb_crop_offset != null && cb_crop_offset.SelectedItem != null && tb_feedback != null)
            {
                //tb_feedback.Text += $"\nCropOffset changed to: {cb_crop_offset.SelectedItem}";
                Debug.WriteLine("tb_feedback updated successfully in Changed_cb_crop_offset.");
            }
            else
            {
                Debug.WriteLine("cb_crop_offset or tb_feedback is null in Changed_cb_crop_offset.");
            }
        }

        private void Changed_cb_crop_ar(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Changed_cb_crop_ar triggered.");
            if (cb_crop_ar != null && cb_crop_ar.SelectedItem != null && tb_feedback != null)
            {
                //tb_feedback.Text += $"\nCropAR changed to: {cb_crop_ar.SelectedItem}";
                Debug.WriteLine("tb_feedback updated successfully in Changed_cb_crop_ar.");
            }
            else
            {
                Debug.WriteLine("cb_crop_ar or tb_feedback is null in Changed_cb_crop_ar.");
            }
        }

        private void Changed_cb_rotate(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Changed_cb_rotate triggered.");
            if (cb_rotate != null && cb_rotate.SelectedItem != null && tb_feedback != null)
            {
                //tb_feedback.Text += $"\nRotate changed to: {cb_rotate.SelectedItem}";
                Debug.WriteLine("tb_feedback updated successfully in Changed_cb_rotate.");
            }
            else
            {
                Debug.WriteLine("cb_rotate or tb_feedback is null in Changed_cb_rotate.");
            }
        }

        private void Changed_cb_flip(object sender, SelectionChangedEventArgs e)
        {
            Debug.WriteLine("Changed_cb_flip triggered.");
            if (cb_flip != null && cb_flip.SelectedItem != null && tb_feedback != null)
            {
                //tb_feedback.Text += $"\nFlip changed to: {cb_flip.SelectedItem}";
                Debug.WriteLine("tb_feedback updated successfully in Changed_cb_flip.");
            }
            else
            {
                Debug.WriteLine("cb_flip or tb_feedback is null in Changed_cb_flip.");
            }
        }

        // Event handler for the Scale/Crop RadioButton
        private void Changed_rb_scale_crop(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Changed_rb_scale_crop triggered.");
            if (tb_feedback != null)
            {
                if (rb_scale != null && rb_scale.IsChecked == true)
                {
                    //tb_feedback.Text += "\nSelected: Scale";
                    Debug.WriteLine("tb_feedback updated successfully in Changed_rb_scale_crop: Scale selected.");
                }
                else if (rb_crop != null && rb_crop.IsChecked == true)
                {
                    //tb_feedback.Text += "\nSelected: Crop";
                    Debug.WriteLine("tb_feedback updated successfully in Changed_rb_scale_crop: Crop selected.");
                }
                else
                {
                    Debug.WriteLine("rb_scale or rb_crop is null in Changed_rb_scale_crop.");
                }
            }
            else
            {
                Debug.WriteLine("tb_feedback is null in Changed_rb_scale_crop.");
            }
        }
    }
}