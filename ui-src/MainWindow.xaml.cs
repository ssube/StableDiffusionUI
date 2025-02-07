﻿using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using SD_FXUI.Utils;
using SD_FXUI.Utils.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Windows.Devices.Usb;

namespace SD_FXUI
{
    public partial class MainWindow : HandyControl.Controls.BlurWindow
    {
        Config Data = null;
        ObservableCollection<ListViewItemsData> ListViewItemsCollections = new ObservableCollection<ListViewItemsData>();
        string currentImage = null;
        ModelCMD SafeCMD = null;

        public class ListViewItemsData
        {
            public string? GridViewColumnName_ImageSource { get; set; }
            public string? GridViewColumnName_LabelContent { get; set; }
        }

        public bool CPUUse = false;
        public MainWindow()
        {
            InitializeComponent();

            Install.SetupDirs();

            cbUpscaler.SelectedIndex = 0;
            cbModel.SelectedIndex = 0;

            cbSampler.SelectedIndex = 0;
            cbDevice.SelectedIndex = 0;

            Helper.Form = this;

            Helper.UIHost = new HostForm();
            Helper.UIHost.Hide();
            Host.Print("\n");

            Helper.GPUID = new GPUInfo();

            // Load App data
            Data = new Config();
            Load();
            ChangeTheme();

            Install.WrapPoserPath();
            Install.WrapHedPath();
            Install.WrapMlsdPath();

            gridImg.Visibility = Visibility.Collapsed;
            brImgPane.Visibility = Visibility.Collapsed;
            btnDDB.Visibility = Visibility.Collapsed;
            Helper.NoImageData = ViewImg.Source;
            Helper.SafeMaskFreeImg = imgMask.Source;

            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                Notification.ToastBtnClickManager(toastArgs);
            };

            cbTI.IsEnabled = false;
            btnApplyTI.IsEnabled = false;

            SafeCMD = new ModelCMD();
            cbExtractPoseSelector.SelectedIndex = 4;

            Helper.MakeInfo.LoRA = new System.Collections.Generic.List<Helper.LoRAData>();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(Helper.ImgPath);
            btnDDB.Visibility = Visibility.Collapsed;

            if (chRandom.IsChecked.Value)
            {
                var rand = new Random();
                tbSeed.Text = rand.Next().ToString();
            }

            ValidateSize();
            MakeCommandObject();

            string cmdline = "";
            bool SafeCPUFlag = CPUUse;

            InvokeProgressUpdate(3);

            switch (Helper.Mode)
            {
                case Helper.ImplementMode.Shark:
                    {
                        cmdline += GetCommandLineShark();
                        Task.Run(() => CMD.ProcessRunnerShark(cmdline, Helper.CurrentUpscaleSize));
                        break;
                    }
                case Helper.ImplementMode.ONNX:
                    {
                        if (tsCN.IsChecked.Value)
                        {
                            cmdline += GetCommandLineOnnx();
                            cmdline += HelperControlNet.Current.CommandLine();

                            Task.Run(() => CMD.ProcessRunnerDiffCN(cmdline, Helper.CurrentUpscaleSize, HelperControlNet.Current));
                            break;
                        }
                        else
                        {
                            Helper.MakeInfo.fp16 = false;
                            SafeCMD.PreStart(cbModel.Text, Helper.MakeInfo.Mode, cbNSFW.IsChecked.Value);
                            SafeCMD.Start();

                            //cmdline += GetCommandLineOnnx();
                            //Task.Run(() => CMD.ProcessRunnerOnnx(cmdline, Size));
                        }
                        break;
                    }
                case Helper.ImplementMode.DiffCPU:
                case Helper.ImplementMode.DiffCUDA:
                    {
                        if (tsCN.IsChecked.Value)
                        {
                            ControlNetBase CurrentCN = HelperControlNet.Current;

                            cmdline += GetCommandLineDiffCuda();
                            cmdline += CurrentCN.CommandLine();

                            Task.Run(() => CMD.ProcessRunnerDiffCN(cmdline, Helper.CurrentUpscaleSize, CurrentCN));
                            break;
                        }
                        else
                        {
                            Helper.MakeInfo.fp16 = cbFf16.IsChecked.Value;
                            SafeCMD.PreStart(cbModel.Text, Helper.MakeInfo.Mode, cbNSFW.IsChecked.Value, true);
                            SafeCMD.Start();
                            //cmdline += GetCommandLineDiffCuda();
                            //Task.Run(() => CMD.ProcessRunnerDiffCuda(cmdline, Size, SafeCPUFlag));
                            break;
                        }
                    }
            }

            string richText = new TextRange(tbPrompt.Document.ContentStart, tbPrompt.Document.ContentEnd).Text;
            Utils.HistoryList.ApplyPrompt(richText);

            currentImage = null;
            ClearImages();
        }

        private void Slider_Denoising(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbDenoising != null)
                tbDenoising.Text = slDenoising.Value.ToString();
        }
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbSteps != null)
                tbSteps.Text = slSteps.Value.ToString();
        }

        private void tbSteps2_TextChanged(object sender, TextChangedEventArgs e)
        {
            double Val = 0;
            double.TryParse(tbCFG.Text, out Val);
            slCFG.Value = Val;
        }

        private void Slider2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbCFG != null)
                tbCFG.Text = slCFG.Value.ToString();
        }

        private void slUpscale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lbUpscale != null)
                lbUpscale.Content = "x" + (slUpscale.Value + 1).ToString();
        }
        private void slDenoise_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lbDenoise != null)
                lbDenoise.Content = "x" + (slDenoise.Value).ToString();

            Helper.Denoise = (int)slDenoise.Value;
        }

        private void tbSteps_TextChanged(object sender, TextChangedEventArgs e)
        {
            double Val = 0;
            double.TryParse(tbSteps.Text, out Val);
            slSteps.Value = Val;
        }

        private void btFolder_ValueChanged(object sender, MouseButtonEventArgs e)
        {
            string argument = "/select, \"" + Helper.ImgPath + "\"";
            Host Explorer = new Host("", "explorer.exe");
            Explorer.Start(argument);
        }
        private void btCmd_ValueChanged(object sender, MouseButtonEventArgs e)
        {
            Helper.UIHost.Hide();
            Helper.UIHost.Show();
        }

        private void OnClose(object sender, EventArgs e)
        {
            Helper.UIHost.Close();
            Save();
        }

        private void Button_ClickBreak(object sender, RoutedEventArgs e)
        {
            SafeCMD.Exit(true);

            foreach (var Proc in Helper.SecondaryProcessList)
            {
                Proc.Kill();
            }

            Host.Print("\n All task aborted (」°ロ°)」");
            Helper.SecondaryProcessList.Clear();
            InvokeProgressUpdate(0);
        }

        private void Button_Click_Import_Model(object sender, RoutedEventArgs e)
        {
            Utils.SharkModelImporter Importer = new Utils.SharkModelImporter();
            Importer.Show();
        }

        private void chRandom_Checked(object sender, RoutedEventArgs e)
        {
            if (tbSeed != null)
                tbSeed.IsEnabled = false;
        }
        private void chRandom_Unchecked(object sender, RoutedEventArgs e)
        {
            tbSeed.IsEnabled = true;
        }

        private void cbDevice_TextChanged(object sender, RoutedEventArgs e)
        {

        }

        private void btnONNX_Click(object sender, RoutedEventArgs e)
        {
            if (Helper.Mode != Helper.ImplementMode.ONNX)
            {
                tsCN.IsChecked = false;

                Helper.Mode = Helper.ImplementMode.ONNX;
                Install.CheckAndInstallONNX();

                var Safe = btnONNX.Background;
                btnONNX.Background = new SolidColorBrush(Colors.DarkOrchid);
                btnShark.Background = Safe;
                btnDiffCuda.Background = Safe;
                btnDiffCpu.Background = Safe;

                UpdateModelsList();
                UpdateModelsTIList();

                cbDevice.Items.Clear();

                foreach (var item in Helper.GPUID.GPUs)
                {
                    cbDevice.Items.Add(item);
                }

                btnImg.Visibility = Visibility.Visible;
                cbFf16.Visibility = Visibility.Hidden;

                grLoRA.Visibility = Visibility.Visible;
                brLoRA.Visibility = Visibility.Visible;

                grCN.Visibility = Visibility.Visible;

                grDevice.Visibility = Visibility.Visible;
                brDevice.Visibility = Visibility.Visible;

                grVAE.Visibility = Visibility.Visible;

                string SafeSampler = cbSampler.Text;

                foreach (string Name in Schedulers.Diffusers)
                {
                    cbSampler.Items.Add(Name);
                }

                cbSampler.Text = Data.Get("sampler", "UniPCMultistep");
                cbDevice.Text = Data.Get("device");

                Title = "Stable Diffusion XUI : ONNX venv";
            }
        }
        private void btnDiffCuda_Click(object sender, RoutedEventArgs e)
        {
            if (Helper.Mode != Helper.ImplementMode.DiffCUDA)
            {
                Helper.Mode = Helper.ImplementMode.DiffCUDA;

                Install.CheckAndInstallCUDA();

                var Safe = btnDiffCuda.Background;
                btnDiffCuda.Background = new SolidColorBrush(Colors.DarkCyan);
                btnONNX.Background = Safe;
                btnShark.Background = Safe;
                btnDiffCpu.Background = Safe;

                UpdateModelsList();
                UpdateModelsTIList();

                grDevice.Visibility = Visibility.Collapsed;
                brDevice.Visibility = Visibility.Collapsed;

                grVAE.Visibility = Visibility.Visible;

                grLoRA.Visibility = Visibility.Visible;
                brLoRA.Visibility = Visibility.Visible;

                grCN.Visibility = Visibility.Visible;

                btnImg.Visibility = Visibility.Visible;
                cbFf16.Visibility = Visibility.Visible;
                CPUUse = false;

                cbSampler.Items.Clear();
                foreach (string Name in Schedulers.Diffusers)
                {
                    cbSampler.Items.Add(Name);
                }

                cbSampler.Text = Data.Get("sampler", "UniPCMultistep");

                foreach (var item in Helper.GPUID.GPUs)
                {
                    if (item.Contains("nvidia"))
                    {
                        cbDevice.Items.Add(item);
                    }
                }

                if (cbDevice.Items.Count == 0)
                {
                    cbDevice.Items.Add("None");
                }

                cbDevice.Text = Data.Get("device");

                if (cbDevice.Text.Length == 0)
                    cbDevice.SelectedItem = 0;

                Title = "Stable Diffusion XUI : CUDA venv";
            }
        }

        private void btnShark_Click(object sender, RoutedEventArgs e)
        {
            if (Helper.Mode != Helper.ImplementMode.Shark)
            {
                Helper.Mode = Helper.ImplementMode.Shark;
                Install.CheckAndInstallShark();

                var Safe = btnShark.Background;
                btnShark.Background = new SolidColorBrush(Colors.DarkSlateBlue);
                btnONNX.Background = Safe;
                btnDiffCuda.Background = Safe;
                btnDiffCpu.Background = Safe;

                UpdateModelsList();
                UpdateModelsTIList();

                cbDevice.Items.Clear();
                cbDevice.Items.Add("vulkan");
                cbDevice.Items.Add("CUDA");

                btnImg.Visibility = Visibility.Hidden;
                cbFf16.Visibility = Visibility.Visible;

                grDevice.Visibility = Visibility.Visible;
                brDevice.Visibility = Visibility.Visible;

                grVAE.Visibility = Visibility.Collapsed;
                grCN.Visibility = Visibility.Collapsed;

                grLoRA.Visibility = Visibility.Collapsed;
                brLoRA.Visibility = Visibility.Collapsed;

                cbSampler.Items.Clear();
                foreach (string Name in Schedulers.Shark)
                {
                    cbSampler.Items.Add(Name);
                }

                cbSampler.Text = Data.Get("sampler", "DDIM");
                cbDevice.Text = Data.Get("device");

                Title = "Stable Diffusion XUI : Shark venv";
            }
        }
        private void btnDiffCpu_Click(object sender, RoutedEventArgs e)
        {
            if (Helper.Mode != Helper.ImplementMode.DiffCPU)
            {
                Helper.Mode = Helper.ImplementMode.DiffCPU;
                Install.CheckAndInstallONNX();

                var Safe = btnDiffCpu.Background;
                btnDiffCpu.Background = new SolidColorBrush(Colors.DarkSalmon);
                btnONNX.Background = Safe;
                btnShark.Background = Safe;
                btnDiffCuda.Background = Safe;

                UpdateModelsList();
                UpdateModelsTIList();

                grDevice.Visibility = Visibility.Collapsed;
                brDevice.Visibility = Visibility.Collapsed;

                btnImg.Visibility = Visibility.Visible;
                cbFf16.Visibility = Visibility.Visible;
                grVAE.Visibility = Visibility.Visible;

                grLoRA.Visibility = Visibility.Visible;
                brLoRA.Visibility = Visibility.Visible;

                grCN.Visibility = Visibility.Visible;
                CPUUse = true;

                cbSampler.Items.Clear();
                foreach (string Name in Schedulers.Diffusers)
                {
                    cbSampler.Items.Add(Name);
                }

                cbSampler.Text = Data.Get("sampler", "UniPCMultistep");
                cbDevice.Text = Data.Get("device");

                Title = "Stable Diffusion XUI : CPU venv";
            }
        }
        private void lvImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Helper.ImgList.Count > 0)
            {
                currentImage = (Helper.ImgList[lvImages.SelectedIndex]);

                ViewImg.Source = CodeUtils.BitmapFromUri(new Uri(currentImage));
                string NewCurrentImage = currentImage.Replace("_upscale.", ".");

                if (File.Exists(NewCurrentImage))
                {
                    currentImage = NewCurrentImage;
                }

                string Name = FS.GetImagesDir() + "best\\" + Path.GetFileName(Helper.ImgList[lvImages.SelectedIndex]);

                if (File.Exists(Name))
                {
                    Helper.ActiveImageState = Helper.ImageState.Favor;
                    btnFavor.Source = imgFavor.Source;
                }
                else
                {
                    Helper.ActiveImageState = Helper.ImageState.Free;
                    btnFavor.Source = imgNotFavor.Source;
                }
            }
        }
        private void cbDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDevice.SelectedItem == null)
                return;

            if (Helper.Mode == Helper.ImplementMode.ONNX)
            {
                Install.WrapONNXGPU(cbDevice.SelectedIndex > 0);
            }
        }

        private void btnImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog OpenDlg = new OpenFileDialog();
            OpenDlg.Filter = "Image Files|*.jpg;*.jpeg;*.png| PNG (*.png)|*.png|JPG (*.jpg)|*.jpg|All files (*.*)|*.*";
            OpenDlg.Multiselect = false;

            bool? IsOpened = OpenDlg.ShowDialog();
            if (IsOpened.Value)
            {
                Helper.InputImagePath = OpenDlg.FileName;
                gridImg.Visibility = Visibility.Visible;
                brImgPane.Visibility = Visibility.Visible;
                imgLoaded.Source = CodeUtils.BitmapFromUri(new Uri(Helper.InputImagePath));
                Helper.DrawMode = Helper.DrawingMode.Img2Img;

                tbMeta.Text = CodeUtils.MetaData(Helper.InputImagePath);
            }
        }

        private void btnZoom_Click(object sender, MouseButtonEventArgs e)
        {
            if (currentImage == null || currentImage.Length < 5)
                return;

            Utils.ImageView ImgViewWnd = new Utils.ImageView();
            ImgViewWnd.SetImage(currentImage);
            ImgViewWnd.Show();
        }

        private void btnToImg_Click(object sender, MouseButtonEventArgs e)
        {
            if (currentImage == null && Helper.ImgList.Count <= 0)
            {
                return;
            }
            else if (currentImage != null)
            {
                Helper.InputImagePath = currentImage;
                imgLoaded.Source = CodeUtils.BitmapFromUri(new Uri(currentImage));
            }
            else
            {
                int Idx = lvImages.SelectedIndex;
                if (lvImages.SelectedIndex == -1)
                {
                    Idx = lvImages.Items.Count - 1;
                }

                Helper.InputImagePath = Helper.ImgList[Idx];
                imgLoaded.Source = CodeUtils.BitmapFromUri(new Uri(Helper.InputImagePath));
            }

            tbMeta.Text = CodeUtils.MetaData(Helper.InputImagePath);

            gridImg.Visibility = Visibility.Visible;
            brImgPane.Visibility = Visibility.Visible;
            Helper.DrawMode = Helper.DrawingMode.Img2Img;
        }

        private void tbDenoising_TextChanged(object sender, TextChangedEventArgs e)
        {
            slDenoising.Value = float.Parse(tbDenoising.Text.Replace('.', ','));
        }

        private void btnImageClear_Click(object sender, RoutedEventArgs e)
        {
            gridImg.Visibility = Visibility.Collapsed;
            brImgPane.Visibility = Visibility.Collapsed;

            Helper.DrawMode = Helper.DrawingMode.Text2Img;
            imgLoaded.Source = Helper.NoImageData;

            // Mask clear
            imgMask.Source = Helper.SafeMaskFreeImg;
            Helper.ImgMaskPath = string.Empty;
            imgMask.Visibility = Visibility.Collapsed;
        }

        private void btnHistory_Click(object sender, MouseButtonEventArgs e)
        {
            Utils.HistoryList HistoryWnd = new Utils.HistoryList();
            HistoryWnd.ShowDialog();
        }

        private void BlurWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Install.Check();
        }

        private void btnFavorClick(object sender, MouseButtonEventArgs e)
        {
            if (lvImages.Items.Count == 0)
            {
                return;
            }

            int CurrentSel = lvImages.SelectedItem != null ? lvImages.SelectedIndex : 0;

            if (Helper.ImageState.Favor == Helper.ActiveImageState)
            {
                string Name = Path.GetFileName(Helper.ImgList[CurrentSel]);
                File.Delete(FS.GetImagesDir() + "best\\" + Name);
                Helper.ActiveImageState = Helper.ImageState.Free;

                btnFavor.Source = imgNotFavor.Source;
            }
            else
            {
                string Name = Path.GetFileName(Helper.ImgList[CurrentSel]);
                File.Copy(Helper.ImgList[CurrentSel], FS.GetImagesDir() + "best\\" + Name);
                Helper.ActiveImageState = Helper.ImageState.Favor;

                btnFavor.Source = imgFavor.Source;
            }
        }

        private void cbUpscaler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Helper.CurrentUpscalerType = (Helper.UpscalerType)cbUpscaler.SelectedIndex;

            // Waifu and SRMD
            if (cbUpscaler.SelectedIndex > 3 && cbUpscaler.SelectedIndex < 7)
            {
                lbDenoiseName.IsEnabled = true;
                lbDenoise.IsEnabled = true;
                slDenoise.IsEnabled = true;
                slDenoise.Maximum = 3;
            }
            else if (cbUpscaler.SelectedIndex == 8)
            {
                lbDenoiseName.IsEnabled = true;
                lbDenoise.IsEnabled = true;
                slDenoise.IsEnabled = true;
                slDenoise.Maximum = 10;
            }
            else
            {
                lbDenoiseName.IsEnabled = false;
                lbDenoise.IsEnabled = false;
                slDenoise.IsEnabled = false;
            }
        }

        private void cbGfpgan_SelectionChanged(object sender, RoutedEventArgs e)
        {
            Helper.EnableGFPGAN = cbGfpgan.IsChecked.Value;
        }

        private void btnDeepDanbooru_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => CMD.DeepDanbooruProcess(Helper.InputImagePath));
        }

        private void Button_Click_DeepDanbooru(object sender, RoutedEventArgs e)
        {
            if (currentImage != null && currentImage != "")
            {
                Task.Run(() => CMD.DeepDanbooruProcess(currentImage));
            }
        }

        private void gridDrop(object sender, DragEventArgs e)
        {
            if (null != e.Data && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                imgView_Drop(sender, e);
            }
        }

        private void imgView_Drop(object sender, DragEventArgs e)
        {
            // Note that you can have more than one file.
            string dropedFile = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];

            if (dropedFile.ToLower().EndsWith(".png") || dropedFile.ToLower().EndsWith(".jpg") || dropedFile.ToLower().EndsWith(".jpeg"))
            {
                currentImage = dropedFile;
                ViewImg.Source = CodeUtils.BitmapFromUri(new Uri(dropedFile));
                btnDDB.Visibility = Visibility.Visible;
            }
        }

        private void btnBestOpen_Click(object sender, RoutedEventArgs e)
        {
            string Path = FS.GetImagesDir() + "\\best";

            if (!Directory.Exists(Path))
                return;

            currentImage = null;
            ClearImages();

            var Files = FS.GetFilesFrom(Path, new string[] { "png", "jpg" }, false);
            foreach (string file in Files)
            {
                SetImg(file);
            }
        }

        private void slEMA_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbETA != null)
                tbETA.Text = slETA.Value.ToString();
        }

        private void tbEMA_TextChanged(object sender, TextChangedEventArgs e)
        {
            slETA.Value = float.Parse(tbETA.Text.Replace('.', ','));
        }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void FloatNumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (e.Text.Length > 1)
            {
                float SkipFlt = 0;
                e.Handled = !float.TryParse(e.Text.Replace('.', ','), out SkipFlt);
            }
            else if (e.Text == ",")
            {
                e.Handled = false;
                return;
            }

            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            Utils.HuggDownload DownloadWnd = new Utils.HuggDownload();
            DownloadWnd.Show();
        }

        private void btnImageClearMask_Click(object sender, RoutedEventArgs e)
        {
            btnImageClearMask.Visibility = Visibility.Collapsed;

            imgMask.Source = Helper.SafeMaskFreeImg;
            Helper.ImgMaskPath = string.Empty;
        }

        private void btnSettingsClick(object sender, RoutedEventArgs e)
        {
            Utils.Settings SettingsWnd = new Utils.Settings();
            SettingsWnd.Show();
        }

        private void gridImg_Drop(object sender, DragEventArgs e)
        {
            string dropedFile = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];

            if (dropedFile.ToLower().EndsWith(".png") || dropedFile.ToLower().EndsWith(".jpg") || dropedFile.ToLower().EndsWith(".jpeg"))
            {
                Helper.ImgMaskPath = dropedFile;
                imgMask.Source = CodeUtils.BitmapFromUri(new Uri(dropedFile));
                btnImageClearMask.Visibility = Visibility.Visible;
            }
        }

        private void slW_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbW != null)
            {
                tbW.Text = slW.Value.ToString();

                if (lbRatio != null)
                    lbRatio.Content = GetRatio(slW.Value, slH.Value);
            }
        }

        private void slH_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbH != null)
            {
                tbH.Text = slH.Value.ToString();

                if (lbRatio != null)
                    lbRatio.Content = GetRatio(slW.Value, slH.Value);
            }
        }

        private void tbW_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tbW.Text.Length == 0)
                return;

            slW.Value = float.Parse(tbW.Text.Replace('.', ','));

            if (lbRatio != null)
                lbRatio.Content = GetRatio(slW.Value, slH.Value);
        }

        private void tbH_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tbH.Text.Length == 0)
                return;

            float NewValue = float.Parse(tbH.Text);
            slH.Value = NewValue;

            if (lbRatio != null)
                lbRatio.Content = GetRatio(slW.Value, slH.Value);
        }

        private void btnApplyTI_Click(object sender, RoutedEventArgs e)
        {
            string Path = FS.GetModelDir(FS.ModelDirs.Diffusers) + cbModel.Text;

            if (Helper.Mode == Helper.ImplementMode.ONNX)
            {
                string CPath = FS.GetModelDir(FS.ModelDirs.ONNX) + cbModel.Text;

                if (!Directory.Exists(Path))
                {
                    Notification.MsgBox("Error! Need base diffuser model for apply!");
                    return;
                }

                TIApply HelpWnd = new TIApply();
                HelpWnd.ShowDialog();

                Directory.CreateDirectory(CPath + "\\textual_inversion_merges\\");

                if (Helper.CurrentTI != null)
                    Task.Run(() => CMD.ApplyTextInv(Path, CPath, Helper.CurrentTI));
            }
            else
            {
                if (!Directory.Exists(Path))
                {
                    Notification.MsgBox("Error! Need base diffuser model for apply!");
                    return;
                }

                TIApply HelpWnd = new TIApply();
                HelpWnd.ShowDialog();

                Directory.CreateDirectory(Path + "\\textual_inversion_merges\\");

                if (Helper.CurrentTI != null)
                    Task.Run(() => CMD.ApplyTextInvDiff(Path, Helper.CurrentTI));
            }
        }

        private void cbModel_Copy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void cbModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            string PickPathName = FS.GetModelDir() + (Helper.Mode != Helper.ImplementMode.ONNX ? "Diffusers\\" : "onnx\\") + e.AddedItems[0] + "\\logo.";

            if (System.IO.File.Exists(PickPathName + "png"))
            {
                imgModelPrivew.Source = CodeUtils.BitmapFromUri(new Uri(PickPathName + "png"));
            }
            else if (System.IO.File.Exists(PickPathName + "jpg"))
            {
                imgModelPrivew.Source = CodeUtils.BitmapFromUri(new Uri(PickPathName + "jpg"));
            }
            else
            {
                imgModelPrivew.Source = Helper.NoImageData;
            }

            if (Helper.Mode != Helper.ImplementMode.ONNX || cbTI == null || e.AddedItems.Count == 0)
                return;

            cbTI.Items.Clear();
            cbTI.Items.Add("None");

            string Mode = "onnx/";

            string ModelPath = FS.GetModelDir() + Mode + e.AddedItems[0] + "/textual_inversion_merges/";

            if (!Directory.Exists(ModelPath))
                return;

            foreach (string File in Directory.GetDirectories(ModelPath))
            {
                cbTI.Items.Add(Path.GetFileNameWithoutExtension(File));
            }
        }

        private void btnMerge_Click(object sender, MouseButtonEventArgs e)
        {
            Utils.Merge MergeWnd = new Utils.Merge();
            MergeWnd.ShowDialog();
        }

        private void pbGen_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (pbGen.Value == 100 || pbGen.Value == 0)
                pbGen.Visibility = Visibility.Collapsed;
            else
                pbGen.Visibility = Visibility.Visible;
        }

        private void btnInImgPose_Click(object sender, RoutedEventArgs e)
        {
            string CurrentImg = Helper.InputImagePath;

            ControlNetBase CN = GetCNType(cbExtractPoseSelector.Text);
            Task.Run(() => CMD.PoserProcess(CurrentImg, CN));

            cbControlNetMode.SelectedIndex = cbExtractPoseSelector.SelectedIndex;

            UpdateModelsListControlNet();
        }

        private void cbPose_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HelperControlNet.Current == null)
                return;

            if (e.AddedItems.Count == 0)
            {
                Helper.CurrentPose = null;
                return;
            }

            string ImgPath = HelperControlNet.Current.Outdir();
            ImgPath += e.AddedItems[0];

            if (!ImgPath.EndsWith(".jpg"))
                ImgPath += ".png";

            if (File.Exists(ImgPath))
                imgPose.Source = CodeUtils.BitmapFromUri(new Uri(ImgPath));

            Helper.CurrentPose = ImgPath;
        }

        private void tsCN_Checked(object sender, RoutedEventArgs e)
        {
            cbPose.IsEnabled = tsCN.IsChecked.Value;
            imgPose.IsEnabled = tsCN.IsChecked.Value;
            cbSampler.IsEnabled = !tsCN.IsChecked.Value;

            if (tsCN.IsChecked.Value)
            {
                cbSampler.Text = "UniPCMultistep";
            }

            if (Helper.Mode != Helper.ImplementMode.ONNX)
                return;

            if (tsCN.IsChecked == true)
            {
                brLoRA.Visibility = Visibility.Collapsed;
                grLoRA.Visibility = Visibility.Collapsed;
            }
            else
            {
                brLoRA.Visibility = Visibility.Visible;
                grLoRA.Visibility = Visibility.Visible;
            }
        }

        private void tsTTA_Checked(object sender, RoutedEventArgs e)
        {
            Helper.TTA = tsTTA.IsChecked.Value;
        }

        private void cbPix2Pix_Checked(object sender, RoutedEventArgs e)
        {
            if (cbSampler == null)
                return;

            if (cbPix2Pix.IsChecked.Value)
            {
                imgMask.Visibility = Visibility.Collapsed;
                cbSampler.Text = "EulerAncestralDiscrete";
                cbSampler.IsEnabled = false;
                cbModel.IsEnabled = false;
            }
            else
            {
                imgMask.Visibility = Visibility.Visible;
                cbSampler.IsEnabled = true;
                cbModel.IsEnabled = true;
            }
        }

        private void cbLoRA_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            string LoRAName = e.AddedItems[0].ToString();
            LoRAName = LoRAName.Replace(".safetensors", string.Empty);

            string TokenFilePath = FS.GetModelDir(FS.ModelDirs.LoRA) + LoRAName + ".txt";
            if (File.Exists(TokenFilePath))
            {
                string Contents = File.ReadAllText(TokenFilePath);
                tbLoRAUserTokens.Text = Contents;
                tbLoRAUserTokens.IsEnabled = true;
            }
            else
            {
                tbLoRAUserTokens.IsEnabled = false;
                tbLoRAUserTokens.Text = "";
            }
        }

        private void cbPreprocess_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void cbControlNetMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            string NewMode = ((ComboBoxItem)e.AddedItems[0]).Content.ToString();

            HelperControlNet.Current = GetCNType(NewMode);

            UpdateModelsListControlNet();
            cbPose.IsEnabled = true;
        }

        private void btnMore(object sender, RoutedEventArgs e)
        {
            ModelSelector form  = new ModelSelector();
            form.ShowDialog();

        }

        private void Button_ClickTest(object sender, RoutedEventArgs e)
        {
         
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (cbLoRA.Text.Length == 0)
                return;

            string Temporary = $"<{cbLoRA.Text}:{tbLorastrength.Text}>, ";
            Temporary += CodeUtils.GetRichText(tbPrompt);

            CodeUtils.SetRichText(tbPrompt, Temporary);
        }
    }
}