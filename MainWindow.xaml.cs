using DicomSync.Helpers;
using DicomSync.ViewModels;
using FellowOakDicom;
using FellowOakDicom.Network;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation; // IMPORTANTE PARA O MENU RETRÁTIL

namespace DicomSync
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _sendCts;
        private List<DicomFile> _allDicomFiles = new List<DicomFile>();
        private ObservableCollection<DicomErrorViewModel> _errorList = new ObservableCollection<DicomErrorViewModel>();

        // Variável para controlar o estado do menu lateral
        private bool _isMenuExpanded = true;

        public MainWindow()
        {
            InitializeComponent();

            // Vincula a lista de erros à UI
            lstErrorLogs.ItemsSource = _errorList;

            // Define o tamanho inicial fixo conforme solicitado
            this.Width = 950;
            this.Height = 600;
        }

        #region MENU LATERAL RETRÁTIL
        private void btnToggleMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_isMenuExpanded)
            {
                // AJUSTE: Mudei de 68 para 60 para esconder totalmente o texto
                AnimateSidebar(200, 60);
            }
            else
            {
                // Expande de volta para 200
                AnimateSidebar(60, 200);
            }
            _isMenuExpanded = !_isMenuExpanded;
        }

        private void AnimateSidebar(double from, double to)
        {
            DoubleAnimation animation = new DoubleAnimation();
            animation.From = from;
            animation.To = to;
            animation.Duration = new Duration(TimeSpan.FromMilliseconds(300));
            animation.EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut };

            // SidebarBorder é o nome que demos ao Border do menu no XAML
            SidebarBorder.BeginAnimation(Border.WidthProperty, animation);
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                if (int.TryParse(rb.Tag.ToString(), out int index))
                {
                    MainTabs.SelectedIndex = index;
                    // Reseta a cor do texto caso estivesse vermelho por erro
                    if (index == 3) rb.Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99));
                }
            }
        }
        #endregion

        #region LÓGICA DA JANELA (Barra de Título)
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
                MainBorder.Margin = new Thickness(5);
                btnMaximize.Content = "⬜";
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        #endregion

        #region GERENCIAMENTO DE ARQUIVOS (Aba SEND)
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
                    lblTotalErro.Text = _allDicomFiles.Count.ToString();
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

        #region OPERAÇÕES DICOM (ECHO / SEND / EDIT)

        // Método Echo Atualizado (Visual Verde/Vermelho)
        private async void btnEcho_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtPortRemote.Text, out int port))
            {
                MessageBox.Show("Porta inválida.");
                return;
            }

            try
            {
                SetInterfaceElementState(false);

                // ESTADO: TESTANDO (Neutro)
                txtConnectionStatus.Text = "Testando...";
                txtPing.Text = "Enviando C-ECHO...";
                txtStatusIcon.Text = "⏳";
                bdrIconCircle.Background = new SolidColorBrush(Colors.White);
                txtStatusIcon.Foreground = new SolidColorBrush(Colors.Orange);
                bdrServerStatus.Background = new SolidColorBrush(Color.FromRgb(243, 244, 246));

                var result = await Services.DicomService.TestConnectionAsync(
                    txtIpRemote.Text, port, txtAeLocal.Text, txtAeRemote.Text);

                if (result.Sucess)
                {
                    // SUCESSO: Visual VERDE Degradê
                    var gradient = new LinearGradientBrush();
                    gradient.StartPoint = new Point(0, 0);
                    gradient.EndPoint = new Point(1, 1);
                    gradient.GradientStops.Add(new GradientStop(Color.FromRgb(34, 197, 94), 0.0));
                    gradient.GradientStops.Add(new GradientStop(Color.FromRgb(22, 163, 74), 1.0));
                    bdrServerStatus.Background = gradient;

                    txtConnectionStatus.Foreground = Brushes.White;
                    txtPing.Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));

                    txtStatusIcon.Text = "✔";
                    txtStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));

                    txtConnectionStatus.Text = "Online";
                    txtPing.Text = string.IsNullOrEmpty(result.Message) ? "Conexão estabelecida." : result.Message;
                }
                else
                {
                    // FALHA: Visual Vermelho Degradê
                    var gradient = new LinearGradientBrush();
                    gradient.StartPoint = new Point(0, 0);
                    gradient.EndPoint = new Point(1, 1);
                    gradient.GradientStops.Add(new GradientStop(Color.FromRgb(220, 38, 38), 0.0));
                    gradient.GradientStops.Add(new GradientStop(Color.FromRgb(185, 28, 28), 1.0));
                    bdrServerStatus.Background = gradient;

                    txtConnectionStatus.Foreground = Brushes.White;
                    txtPing.Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));

                    txtStatusIcon.Text = "✖";
                    txtStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));

                    txtConnectionStatus.Text = "Offline";
                    txtPing.Text = "O servidor não respondeu.";
                }

                txtLogMessage.Text = result.Sucess ? "Echo Success (0000)" : "Echo Failed";
            }
            catch (Exception ex)
            {
                txtConnectionStatus.Text = "Erro";
                txtLogMessage.Text = ex.Message;
                bdrServerStatus.Background = new SolidColorBrush(Color.FromRgb(185, 28, 28));
            }
            finally
            {
                SetInterfaceElementState(true);
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (_allDicomFiles.Count == 0 || !int.TryParse(txtPortRemote.Text, out int port)) return;

            SetInterfaceElementState(false);
            _sendCts = new CancellationTokenSource();
            _errorList.Clear();

            int successCount = 0;
            int errorCount = 0;

            lblTotalSucesso.Text = _allDicomFiles.Count.ToString();
            lblTotalErro.Text = _allDicomFiles.Count.ToString();

            try
            {
                await Services.DicomService.ExecuteSendAsync(
                    _allDicomFiles, txtIpRemote.Text, port, txtAeLocal.Text, txtAeRemote.Text,
                    (status, file) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (status == DicomStatus.Success) successCount++;
                            else
                            {
                                errorCount++;
                                _errorList.Insert(0, new DicomErrorViewModel
                                {
                                    Description = status.ToString(),
                                    ErrorCode = status.Code.ToString("X4"),
                                    InstanceUID = file.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, "N/A"),
                                    ProbableCause = Services.DicomService.GetFriendlyError(status),
                                    Time = DateTime.Now.ToString("HH:mm:ss")
                                });

                                // Notificação visual de erro na aba de logs
                                rbLogs.Foreground = Brushes.Red;
                            }
                            lblSucessoCount.Text = successCount.ToString();
                            lblErroCount.Text = errorCount.ToString();
                        });
                    }, _sendCts.Token);

                MessageBox.Show("Operação de envio finalizada.");
            }
            finally
            {
                SetInterfaceElementState(true);
            }
        }

        // Método para o botão "Editar Dados" na aba SEND
        private void btnGoToEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_allDicomFiles == null || _allDicomFiles.Count == 0)
            {
                MessageBox.Show("Importe um estudo primeiro.");
                return;
            }
            rbData.IsChecked = true;
            MainTabs.SelectedIndex = 2;
            btnEditData_Click(sender, e);
        }

        // Método para o botão roxo "DATAMAKER"
        private void btnEditData_Click(object sender, RoutedEventArgs e)
        {
            if (_allDicomFiles == null || _allDicomFiles.Count == 0) return;

            var editWindow = new EditDataWindow(_allDicomFiles);

            if (editWindow.ShowDialog() == true)
            {
                FillPatientData(_allDicomFiles[0].Dataset);

                // Lógica "Salvar e Enviar"
                if (editWindow.AutoSendRequested)
                {
                    rbSend.IsChecked = true;
                    MainTabs.SelectedIndex = 1;

                    Dispatcher.InvokeAsync(async () =>
                    {
                        await Task.Delay(300);
                        btnSend_Click(sender, e);
                    });
                }
            }
        }
        #endregion

        private void SetInterfaceElementState(bool isEnabled)
        {
            btnSend.IsEnabled = isEnabled;
            btnEcho.IsEnabled = isEnabled;
            btnLoadImages.IsEnabled = isEnabled;
            btnSelectFolder.IsEnabled = isEnabled;
            this.Cursor = isEnabled ? Cursors.Arrow : Cursors.Wait;
        }
    }
}