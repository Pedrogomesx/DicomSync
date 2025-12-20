using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
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
        private List<DicomErrorLog> _errorLogs = new List<DicomErrorLog>();

        public MainWindow()
        {
            InitializeComponent();
        }

        // --- LÓGICA DA JANELA PERSONALIZADA ---

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    btnMaximize_Click(sender, e);
                }
                else
                {
                    this.DragMove();
                }
            }
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void btnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                this.WindowState = WindowState.Maximized;
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.Margin = new Thickness(0);
                btnMaximize.Content = "❐";
            }
            else
            {
                this.WindowState = WindowState.Normal;
                MainBorder.CornerRadius = new CornerRadius(8);
                MainBorder.Margin = new Thickness(10);
                btnMaximize.Content = "⬜";
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // --- VIEW MODELS ---

        public class DicomItemViewModel
        {
            public bool IsSelected { get; set; } = true;
            public string FileName { get; set; }
            public string FullPath { get; set; }
            public string SeriesUID { get; set; }
        }

        public class DicomSeriesViewModel
        {
            public bool IsSelected { get; set; } = true;
            public string SeriesUID { get; set; }
            public string SeriesDescription { get; set; }
            public int ImageCount { get; set; }
            public string ImageCountInfo => $"{ImageCount} imagens nesta série";
        }
        public class DicomErrorLog
        {
            public string InstanceUID { get; set; }
            public string ErrorCode { get; set; }     // Ex: 0110H
            public string Description { get; set; }   // Ex: Out of Resources
            public string ProbableCause { get; set; } // Ex: Disco do PACS cheio
            public string Time { get; set; }
        }

        // --- GERENCIAMENTO DE ARQUIVOS ---

        private void btnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "SELECIONE_A_PASTA_DO_ESTUDO",
                Title = "Selecione a pasta RAIZ do Estudo"
            };

            if (dialog.ShowDialog() == true)
            {
                string folderPath = System.IO.Path.GetDirectoryName(dialog.FileName);
                txtFolderPath.Text = folderPath;
                btnLoadImages.IsEnabled = true;
                lblStatusImages.Text = "Pasta selecionada.";
            }
        }

        private async void btnLoadImages_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtFolderPath.Text))
            {
                await LoadImagesAsync(txtFolderPath.Text);
            }
        }

        private async Task LoadImagesAsync(string folderPath)
        {
            lblStatusImages.Text = "A procurar ficheiros...";
            pbImagesLoading.Visibility = Visibility.Visible;
            pbImagesLoading.Value = 0;
            pbImagesLoading.IsIndeterminate = false;

            lstImages.ItemsSource = null;
            lstSeries.ItemsSource = null;
            _allDicomFiles.Clear();

            var listImages = new List<DicomItemViewModel>();
            var listSeries = new List<DicomSeriesViewModel>();

            await Task.Run(() =>
            {
                try
                {
                    string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);

                    Dispatcher.Invoke(() => {
                        pbImagesLoading.Maximum = files.Length;
                    });

                    bool firstFileProcessed = false;
                    int processedCount = 0;

                    foreach (var file in files)
                    {
                        processedCount++;
                        try
                        {
                            Dispatcher.Invoke(() => {
                                pbImagesLoading.Value = processedCount;
                                lblStatusImages.Text = $"A ler: {processedCount} de {files.Length} ficheiros...";
                            });

                            var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                            _allDicomFiles.Add(dicomFile);

                            string sUID = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, "Unknown");

                            listImages.Add(new DicomItemViewModel
                            {
                                IsSelected = true,
                                FileName = Path.GetFileName(file),
                                FullPath = file,
                                SeriesUID = sUID
                            });

                            if (!firstFileProcessed)
                            {
                                Dispatcher.Invoke(() => FillPatientData(dicomFile.Dataset));
                                firstFileProcessed = true;
                            }
                        }
                        catch { }
                    }

                    var grouped = _allDicomFiles
                        .GroupBy(d => d.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, "Unknown"))
                        .Select(g => new DicomSeriesViewModel
                        {
                            IsSelected = true,
                            SeriesUID = g.Key,
                            SeriesDescription = g.First().Dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, "Sem Descrição"),
                            ImageCount = g.Count()
                        })
                        .ToList();

                    listSeries.AddRange(grouped);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show("Erro: " + ex.Message));
                }
            });

            // --- ATUALIZAÇÃO DOS CONTADORES APÓS LOCALIZAR ---
            lstImages.ItemsSource = listImages;
            lstSeries.ItemsSource = listSeries;

            // Aqui está o segredo:
            lblTotalSucesso.Text = listImages.Count.ToString();
            lblTotalErro.Text = listImages.Count.ToString();
            lblSucessoCount.Text = "0";
            lblErroCount.Text = "0";

            lblStatusImages.Text = $"{listImages.Count} imagens carregadas na memória.";
            pbImagesLoading.Visibility = Visibility.Collapsed;
        }

        // --- OPERAÇÕES DICOM (ECHO E SEND) ---

        private async void btnEcho_Click(object sender, RoutedEventArgs e)
        {
            string ip = txtIpRemote.Text;
            string aeLocal = txtAeLocal.Text;
            string aeRemote = txtAeRemote.Text;

            if (!int.TryParse(txtPortRemote.Text, out int porta))
            {
                MessageBox.Show("Porta inválida.");
                return;
            }

            btnEcho.IsEnabled = false;
            this.Cursor = Cursors.Wait;

            try
            {
                var client = DicomClientFactory.Create(ip, porta, false, aeLocal, aeRemote);
                var echoReq = new DicomCEchoRequest();

                bool success = false;
                echoReq.OnResponseReceived += (req, response) =>
                {
                    if (response.Status == DicomStatus.Success) success = true;
                };

                await client.AddRequestAsync(echoReq);
                await client.SendAsync();

                if (success)
                    MessageBox.Show($"Conexão estabelecida com SUCESSO!", "Teste", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("O servidor respondeu, mas retornou um erro.", "Aviso");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnEcho.IsEnabled = true;
                this.Cursor = Cursors.Arrow;
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            // 1. Validações Iniciais
            if (_allDicomFiles.Count == 0)
            {
                MessageBox.Show("Nenhum estudo carregado.");
                return;
            }

            string ip = txtIpRemote.Text;
            string aeLocal = txtAeLocal.Text;
            string aeRemote = txtAeRemote.Text;

            if (!int.TryParse(txtPortRemote.Text, out int porta))
            {
                MessageBox.Show("Porta inválida.");
                return;
            }

            // 2. Identificar ficheiros selecionados
            List<DicomFile> filesToSend = new List<DicomFile>();
            var seriesSource = lstSeries.ItemsSource as List<DicomSeriesViewModel>;
            var imagesSource = lstImages.ItemsSource as List<DicomItemViewModel>;

            if (seriesSource != null && seriesSource.Any(s => s.IsSelected))
            {
                var selectedUIDs = seriesSource.Where(s => s.IsSelected).Select(s => s.SeriesUID).ToList();
                filesToSend = _allDicomFiles.Where(d => selectedUIDs.Contains(d.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, ""))).ToList();
            }
            else if (imagesSource != null && imagesSource.Any(i => i.IsSelected))
            {
                var selectedPaths = imagesSource.Where(i => i.IsSelected).Select(i => i.FullPath).ToList();
                filesToSend = _allDicomFiles.Where(d => selectedPaths.Contains(d.File.Name)).ToList();
            }

            if (filesToSend.Count == 0)
            {
                MessageBox.Show("Selecione ao menos uma imagem ou série para enviar.");
                return;
            }

            // --- PREPARAÇÃO PARA O ENVIO E LOGS ---
            btnSend.IsEnabled = false;
            _errorLogs.Clear();
            lstErrorLogs.ItemsSource = null;

            // Inicializa contadores visuais
            lblSucessoCount.Text = "0";
            lblErroCount.Text = "0";
            lblTotalSucesso.Text = filesToSend.Count.ToString();
            lblTotalErro.Text = filesToSend.Count.ToString();

            pbImagesLoading.Visibility = Visibility.Visible;
            pbImagesLoading.Maximum = filesToSend.Count;
            pbImagesLoading.Value = 0;

            int successCount = 0;
            int errorCount = 0;

            try
            {
                var client = DicomClientFactory.Create(ip, porta, false, aeLocal, aeRemote);
                client.ClientOptions.AssociationRequestTimeoutInMs = 60000;

                foreach (var dicomFile in filesToSend)
                {
                    var request = new DicomCStoreRequest(dicomFile);

                    // CAPTURA DE RESPOSTA (Sem MessageBox aqui dentro!)
                    request.OnResponseReceived += (req, response) =>
                    {
                        if (response.Status == DicomStatus.Success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            var status = response.Status;

                            var errorEntry = new DicomErrorLog
                            {
                                InstanceUID = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, "N/A"),
                                ErrorCode = $"0x{status.Code:X4}H",
                                Description = status.ToString(),
                                ProbableCause = GetFriendlyError(status, dicomFile.Dataset),
                                Time = DateTime.Now.ToString("HH:mm:ss")
                            };

                            Dispatcher.Invoke(() =>
                            {
                                _errorLogs.Insert(0, errorEntry);
                                lstErrorLogs.ItemsSource = null;
                                lstErrorLogs.ItemsSource = _errorLogs;
                            });
                        }

                        // Atualiza contadores na tela em tempo real
                        Dispatcher.Invoke(() =>
                        {
                            lblSucessoCount.Text = successCount.ToString();
                            lblErroCount.Text = errorCount.ToString();
                            pbImagesLoading.Value = successCount + errorCount;
                            lblStatusImages.Text = $"Enviando: {successCount + errorCount}/{filesToSend.Count}";
                        });
                    };

                    await client.AddRequestAsync(request);
                }

                // DISPARA O ENVIO DE TODOS OS ARQUIVOS
                await client.SendAsync();

                // APENAS UMA CAIXA DE MENSAGEM NO FINAL
                string resultadoFinal = $"Envio concluído!\n\nSucessos: {successCount}\nFalhas: {errorCount}";
                MessageBox.Show(resultadoFinal, "Relatório de Envio", MessageBoxButton.OK,
                                errorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro crítico na conexão: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSend.IsEnabled = true;
                pbImagesLoading.Visibility = Visibility.Collapsed;
                lblStatusImages.Text = "Pronto.";
            }
        }
        // --- AUXILIARES ---

        private void btnEditData_Click(object sender, RoutedEventArgs e)
        {
            if (_allDicomFiles.Count == 0) return;

            EditDataWindow editWindow = new EditDataWindow(_allDicomFiles);
            if (editWindow.ShowDialog() == true)
            {
                FillPatientData(_allDicomFiles[0].Dataset);
            }
        }

        private void FillPatientData(DicomDataset dataset)
        {
            txtPatientName.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty);
            txtPatientID.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
            txtAccessionNumber.Text = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);
            txtBirthDate.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, string.Empty);
            txtStudyDate.Text = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty);
            txtStudyDescription.Text = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, string.Empty);
        }
        private string GetFriendlyError(DicomStatus status, DicomDataset dataset = null)
        {
            ushort code = status.Code;

            // 1. TRATAMENTO DE TAG DICOM VAZIA / NÃO ENCONTRADA
            // Códigos 0106H (Invalid Attribute Value) ou 0120H (Missing Attribute)
            if (code == 0x0106 || code == 0x0120 || code == 0x0116)
            {
                // Tentamos identificar qual tag causou o problema se o PACS informar
                string tagInfo = !string.IsNullOrEmpty(status.ErrorComment) ? status.ErrorComment : "Tag obrigatória";
                return $"Erro de Dados: A {tagInfo} está vazia ou ausente no dataset do arquivo.";
            }

            // 2. TRANSFER SYNTAX NÃO SUPORTADA
            // Código 0122H (SOP Class Not Supported) ou CxxxH (Unable to Process)
            if (code == 0x0122 || (code >= 0xC000 && code <= 0xCFFF))
            {
                return "Incompatibilidade: A Transfer Syntax (compressão) deste arquivo não é suportada pelo PACS de destino.";
            }

            // 3. OPERAÇÃO ABORTADA
            // No fo-dicom, o status pode vir como cancelado ou erro de processamento específico
            if (code == 0xFE00 || code == 0x0110)
            {
                return "Operação Abortada: O processo foi interrompido pelo servidor ou pelo usuário.";
            }

            // Outros erros são simplificados para uma mensagem genérica ou ignorados conforme solicitado
            return "Falha na operação DICOM. Verifique os logs do servidor.";
        }
    }
}