using DicomSync.Helpers;
using DicomSync.Services;
using DicomSync.ViewModels;
using FellowOakDicom;
using FellowOakDicom.Network;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DicomSync
{
    public partial class MainWindow : Window
    {
        private List<DicomFile> _allDicomFiles = new List<DicomFile>();

        public MainWindow()
        {
            InitializeComponent();
        }

        #region LÓGICA DA JANELA (UI)
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2) btnMaximize_Click(sender, e);
                else DragMove();
            }
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void btnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.Margin = new Thickness(0);
                btnMaximize.Content = "❐";
            }
            else
            {
                WindowState = WindowState.Normal;
                MainBorder.CornerRadius = new CornerRadius(8);
                MainBorder.Margin = new Thickness(10);
                btnMaximize.Content = "⬜";
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        #endregion

        #region GERENCIAMENTO DE ARQUIVOS
        private void btnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "SELECIONE_A_PASTA",
                Title = "Selecione a pasta do Estudo"
            };

            if (dialog.ShowDialog() == true)
            {
                txtFolderPath.Text = Path.GetDirectoryName(dialog.FileName);
                btnLoadImages.IsEnabled = true;
                lblStatusImages.Text = "Pasta selecionada.";
            }
        }

        private async void btnLoadImages_Click(object sender, RoutedEventArgs e)
        {
            string path = txtFolderPath.Text;
            if (string.IsNullOrEmpty(path)) return;

            pbImagesLoading.Visibility = Visibility.Visible;
            _allDicomFiles.Clear();

            // 1. Carrega Arquivos via Serviço
            var files = await Services.DicomService.LoadFilesAsync(path, (current, total) =>
            {
                Dispatcher.Invoke(() =>
                {
                    pbImagesLoading.Maximum = total;
                    pbImagesLoading.Value = current;
                    lblStatusImages.Text = $"Lendo: {current}/{total}";
                });
            });

            _allDicomFiles = files;

            if (_allDicomFiles.Count > 0)
            {
                // 2. Preenche dados do Paciente (Cabeçalho)
                FillPatientData(_allDicomFiles[0].Dataset);

                // 3. Alimenta as listas da UI usando as ViewModels
                lstImages.ItemsSource = files.Select(f => Services.DicomService.CreateItemViewModel(f)).ToList();
                lstSeries.ItemsSource = Services.DicomService.GroupIntoSeries(files);

                lblTotalSucesso.Text = _allDicomFiles.Count.ToString();
                lblStatusImages.Text = "Estudo carregado com sucesso.";
            }
            else
            {
                MessageBox.Show("Nenhum arquivo DICOM válido encontrado.");
            }
            pbImagesLoading.Visibility = Visibility.Collapsed;
        }

        private void FillPatientData(DicomDataset dataset)
        {
            txtPatientName.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientName, "N/A");
            txtPatientID.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientID, "N/A");
            txtAccessionNumber.Text = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "N/A");
            txtStudyDescription.Text = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, "N/A");

            // Formatação de data brasileira
            txtBirthDate.Text = DicomFormatter.FormatDicomDate(dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, ""));
            txtStudyDate.Text = DicomFormatter.FormatDicomDate(dataset.GetSingleValueOrDefault(DicomTag.StudyDate, ""));
        }
        #endregion

        #region OPERAÇÕES DICOM (ECHO / SEND)
        private async void btnEcho_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtPortRemote.Text, out int port)) return;

            Cursor = Cursors.Wait;
            btnEcho.IsEnabled = false;

            var result = await Services.DicomService.TestConnectionAsync(txtIpRemote.Text, port, txtAeLocal.Text, txtAeRemote.Text);

            MessageBox.Show(result.Message, "Teste de Conexão", MessageBoxButton.OK,
                            result.Sucess ? MessageBoxImage.Information : MessageBoxImage.Error);

            btnEcho.IsEnabled = true;
            Cursor = Cursors.Arrow;
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (_allDicomFiles.Count == 0) return;
            if (!int.TryParse(txtPortRemote.Text, out int port)) return;

            btnSend.IsEnabled = false;
            Cursor = Cursors.Wait;

            int successCount = 0;
            int errorCount = 0;
            var errorList = new List<DicomErrorViewModel>();

            pbImagesLoading.Maximum = _allDicomFiles.Count;
            pbImagesLoading.Value = 0;
            pbImagesLoading.Visibility = Visibility.Visible;

            try
            {
                await Services.DicomService.ExecuteSendAsync(_allDicomFiles, txtIpRemote.Text, port, txtAeLocal.Text, txtAeRemote.Text, (status, file) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (status == DicomStatus.Success) successCount++;
                        else
                        {
                            errorCount++;
                            errorList.Insert(0, new DicomErrorViewModel
                            {
                                InstanceUID = file.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, "N/A"),
                                ErrorCode = $"0x{status.Code:X4}H",
                                Description = status.ToString(),
                                ProbableCause = Services.DicomService.GetFriendlyError(status),
                                Time = DateTime.Now.ToString("HH:mm:ss")
                            });
                            lstErrorLogs.ItemsSource = null;
                            lstErrorLogs.ItemsSource = errorList;
                        }

                        pbImagesLoading.Value++;
                        lblSucessoCount.Text = successCount.ToString();
                        lblErroCount.Text = errorCount.ToString();
                    });
                });

                MessageBox.Show($"Envio Finalizado!\nSucessos: {successCount}\nFalhas: {errorCount}");
            }
            catch (Exception ex) { MessageBox.Show("Erro crítico: " + ex.Message); }
            finally
            {
                btnSend.IsEnabled = true;
                Cursor = Cursors.Arrow;
                pbImagesLoading.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region EVENTOS DE INTERFACE
        private void btnEditData_Click(object sender, RoutedEventArgs e)
        {
            if (_allDicomFiles.Count == 0) return;
            var editWindow = new EditDataWindow(_allDicomFiles);
            if (editWindow.ShowDialog() == true)
            {
                FillPatientData(_allDicomFiles[0].Dataset);
            }
        }

        private void DateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                int selectionStart = textBox.SelectionStart;
                int oldLength = textBox.Text.Length;

                string digitsOnly = new string(textBox.Text.Where(char.IsDigit).ToArray());
                if (digitsOnly.Length > 8) digitsOnly = digitsOnly.Substring(0, 8);

                string formatted = "";
                if (digitsOnly.Length > 0)
                {
                    formatted = digitsOnly.Substring(0, Math.Min(digitsOnly.Length, 2));
                    if (digitsOnly.Length > 2)
                    {
                        formatted += "/" + digitsOnly.Substring(2, Math.Min(digitsOnly.Length - 2, 2));
                        if (digitsOnly.Length > 4)
                            formatted += "/" + digitsOnly.Substring(4, Math.Min(digitsOnly.Length - 4, 4));
                    }
                }

                if (textBox.Text != formatted)
                {
                    textBox.Text = formatted;
                    int newSelectionStart = selectionStart + (formatted.Length - oldLength);
                    textBox.SelectionStart = Math.Max(0, Math.Min(formatted.Length, newSelectionStart));
                }
            }
        }
        #endregion
    }
}