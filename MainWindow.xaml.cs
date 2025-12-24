using DicomSync.Helpers;

using DicomSync.ViewModels;
using FellowOakDicom;
using FellowOakDicom.Network;
using Microsoft.Win32;


using System.Collections.ObjectModel;
using System.IO;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Windows.Media; // Adicione este using

namespace DicomSync
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _sendCts;
        private List<DicomFile> _allDicomFiles = [];
        private ObservableCollection<DicomErrorViewModel> _errorList = [];
        

    public MainWindow()
        {
            InitializeComponent();
            lstErrorLogs.ItemsSource = _errorList;
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
            // REGRA: Se houver um envio em curso, pergunta se deseja abortar
            if (_sendCts != null)
            {
                var confirm = MessageBox.Show("Um envio está em curso. Deseja abortar o envio atual para carregar novos arquivos?",
                    "Abortar Envio", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    _sendCts.Cancel();
                    await Task.Delay(500); // Aguarda o encerramento da rede
                }
                else return;
            }

            string path = txtFolderPath.Text;
            if (string.IsNullOrEmpty(path)) return;

            SetInterfaceElementState(false);
            pbImagesLoading.Visibility = Visibility.Visible;
            _allDicomFiles.Clear();

            try
            {
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
                    FillPatientData(_allDicomFiles[0].Dataset);
                    lstImages.ItemsSource = files.Select(f => Services.DicomService.CreateItemViewModel(f)).ToList();
                    lstSeries.ItemsSource = Services.DicomService.GroupIntoSeries(files);
                    lblTotalSucesso.Text = _allDicomFiles.Count.ToString();
                    lblStatusImages.Text = "Estudo carregado com sucesso.";
                }
                else
                {
                    MessageBox.Show("Nenhum arquivo DICOM válido encontrado.");
                }
            }
            finally
            {
                pbImagesLoading.Visibility = Visibility.Collapsed;
                SetInterfaceElementState(true);
            }
        }

        private void FillPatientData(DicomDataset dataset)
        {
            txtPatientName.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientName, "N/A");
            txtPatientID.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientID, "N/A");
            txtAccessionNumber.Text = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, "N/A");
            txtStudyDescription.Text = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, "N/A");

            txtBirthDate.Text = DicomFormatter.FormatDicomDate(dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, ""));
            txtStudyDate.Text = DicomFormatter.FormatDicomDate(dataset.GetSingleValueOrDefault(DicomTag.StudyDate, ""));
        }
        #endregion

        #region OPERAÇÕES DICOM (ECHO / SEND)
        private async void btnEcho_Click(object sender, RoutedEventArgs e)
        {
            // Reseta visualização inicial
            bdrStatus.Visibility = Visibility.Collapsed;

            // --- 1. Validação ---
            if (!int.TryParse(txtPortRemote.Text, out int port))
            {
                // Define o visual de ERRO DE VALIDAÇÃO
                txtLogTitle.Text = "Erro de Validação";
                txtLogTitle.Foreground = Brushes.DarkRed; // #8B0000

                txtLogMessage.Text = "A porta informada não é válida. Insira apenas números.";
                txtLogCode.Text = "VAL-01";
                txtLogUID.Text = "Local";

                bdrStatus.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                SetInterfaceElementState(false);

                // --- 2. Teste de Conexão ---
                var result = await Services.DicomService.TestConnectionAsync(
                    txtIpRemote.Text, port, txtAeLocal.Text, txtAeRemote.Text);

                // Preenche Título e Mensagem baseados no resultado
                txtLogTitle.Text = result.Sucess ? "Conexão Estabelecida" : "Falha na Conexão";
                txtLogMessage.Text = result.Message;

                // Define a cor do título e código
                if (result.Sucess)
                {
                    // Verde (#2E7D32)
                    txtLogTitle.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                    txtLogCode.Text = "OK-200";
                }
                else
                {
                    // Vermelho Escuro (#B71C1C)
                    txtLogTitle.Foreground = new SolidColorBrush(Color.FromRgb(183, 28, 28));
                    txtLogCode.Text = "ERR-NET";
                }

                // Mostra o destino no UID
                txtLogUID.Text = $"{txtIpRemote.Text}:{port}";

                bdrStatus.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                // --- 3. Erro Crítico (Crash) ---
                txtLogTitle.Text = "Erro Crítico do Sistema";
                txtLogTitle.Foreground = Brushes.Red;

                txtLogMessage.Text = ex.Message;
                txtLogCode.Text = "EX-500";
                txtLogUID.Text = "System Exception";

                bdrStatus.Visibility = Visibility.Visible;
            }
            finally
            {
                SetInterfaceElementState(true);
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (_allDicomFiles.Count == 0) return;
            if (!int.TryParse(txtPortRemote.Text, out int port)) return;

            // BLOQUEIO DA INTERFACE E RESET DE LOGS
            SetInterfaceElementState(false);
            _sendCts = new CancellationTokenSource();
            _errorList.Clear(); // Limpa logs de envios anteriores de forma segura

            int successCount = 0;
            int errorCount = 0;

            pbImagesLoading.Maximum = _allDicomFiles.Count;
            pbImagesLoading.Value = 0;
            pbImagesLoading.Visibility = Visibility.Visible;

            try
            {
                await Services.DicomService.ExecuteSendAsync(
                    _allDicomFiles,
                    txtIpRemote.Text,
                    port,
                    txtAeLocal.Text,
                    txtAeRemote.Text,
                    (status, file) =>
                    {
                        // VERIFICAÇÃO DE CANCELAMENTO
                        if (_sendCts != null && _sendCts.IsCancellationRequested) return;

                        Dispatcher.Invoke(() =>
                        {
                            if (_sendCts == null || _sendCts.IsCancellationRequested) return;

                            if (status == DicomStatus.Success)
                            {
                                successCount++;
                            }
                            else
                            {
                                errorCount++;
                                // Adiciona diretamente na coleção que a UI está observando
                                _errorList.Insert(0, new DicomErrorViewModel
                                {
                                    InstanceUID = file.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, "N/A"),
                                    ErrorCode = $"0x{status.Code:X4}H",
                                    Description = status.ToString(),
                                    ProbableCause = Services.DicomService.GetFriendlyError(status),
                                    Time = DateTime.Now.ToString("HH:mm:ss")
                                });
                            }

                            pbImagesLoading.Value++;
                            lblSucessoCount.Text = successCount.ToString();
                            lblErroCount.Text = errorCount.ToString();
                        });
                    },
                    _sendCts.Token);

                if (!_sendCts.IsCancellationRequested)
                {
                    MessageBox.Show($"Envio Finalizado!\nSucessos: {successCount}\nFalhas: {errorCount}", "DicomSync");
                }
                else
                {
                    lblStatusImages.Text = "Envio cancelado pelo usuário.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro crítico no envio: " + ex.Message);
            }
            finally
            {
                SetInterfaceElementState(true);
                _sendCts?.Dispose();
                _sendCts = null;
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

        private void SetInterfaceElementState(bool isEnabled)
        {
            btnSend.IsEnabled = isEnabled;
            btnEcho.IsEnabled = isEnabled;
            btnEditData.IsEnabled = isEnabled;
            btnLoadImages.IsEnabled = isEnabled;
            btnSelectFolder.IsEnabled = isEnabled;

            this.Cursor = isEnabled ? Cursors.Arrow : Cursors.Wait;
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

        private void lstErrorLogs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void txtFolderPath_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}