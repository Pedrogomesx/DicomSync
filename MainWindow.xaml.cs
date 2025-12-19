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
using System.Windows.Input; // NECESSÁRIO

namespace DicomSync
{
    public partial class MainWindow : Window
    {
        private List<DicomFile> _allDicomFiles = new List<DicomFile>();

        public MainWindow()
        {
            InitializeComponent();
        }

        // --- LÓGICA DA JANELA PERSONALIZADA ---

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // Se clicar duas vezes na barra, maximiza
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
                // Remove bordas arredondadas e margem ao maximizar para preencher a tela
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.Margin = new Thickness(0);
                btnMaximize.Content = "❐"; // Ícone de Restaurar
            }
            else
            {
                this.WindowState = WindowState.Normal;
                // Restaura bordas arredondadas e sombra
                MainBorder.CornerRadius = new CornerRadius(8);
                MainBorder.Margin = new Thickness(10);
                btnMaximize.Content = "⬜"; // Ícone de Maximizar
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // --- (A PARTIR DAQUI, SEU CÓDIGO ORIGINAL SEGUE NORMAL) ---

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
            string path = txtFolderPath.Text;
            if (string.IsNullOrEmpty(path) || path.Contains("Nenhuma")) return;
            await LoadImagesAsync(path);
        }

        private async Task LoadImagesAsync(string folderPath)
        {
            lblStatusImages.Text = "A procurar ficheiros...";
            pbImagesLoading.Visibility = Visibility.Visible;
            pbImagesLoading.Value = 0; // Reset na barra
            pbImagesLoading.IsIndeterminate = false; // Desativa o modo "infinito"

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

                    // 1. Configura o máximo da barra com o total de ficheiros encontrados
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
                            // 2. Atualiza o progresso e o texto
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
                        catch
                        {
                            // Se não for um DICOM válido, apenas ignoramos e passamos ao próximo
                        }
                    }

                    // Agrupamento (Fase final)
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

            lstImages.ItemsSource = listImages;
            lstSeries.ItemsSource = listSeries;
            lblStatusImages.Text = $"{listImages.Count} imagens carregadas na memória.";
            pbImagesLoading.Visibility = Visibility.Collapsed;
        }

        private void btnEditData_Click(object sender, RoutedEventArgs e)
        {
            if (_allDicomFiles.Count == 0)
            {
                MessageBox.Show("Nenhum estudo carregado.");
                return;
            }

            EditDataWindow editWindow = new EditDataWindow(_allDicomFiles);

            if (editWindow.ShowDialog() == true)
            {
                if (_allDicomFiles.Count > 0)
                {
                    FillPatientData(_allDicomFiles[0].Dataset);
                }
            }
        }
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
                // Cria o cliente usando a Factory (Compatível com versões novas)
                var client = DicomClientFactory.Create(ip, porta, false, aeLocal, aeRemote);

                // Adiciona o pedido de ECHO
                var echoReq = new DicomCEchoRequest();

                // Evento para capturar a resposta
                bool success = false;
                echoReq.OnResponseReceived += (req, response) =>
                {
                    if (response.Status == DicomStatus.Success)
                        success = true;
                };

                await client.AddRequestAsync(echoReq);
                await client.SendAsync();

                if (success)
                    MessageBox.Show($"Conexão com {aeRemote} ({ip}:{porta}) estabelecida com SUCESSO!", "Teste de Conexão", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("O servidor respondeu, mas retornou um erro.", "Aviso");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha na conexão:\n{ex.Message}\n\nVerifique IP, Porta e se o PACS permite seu AE Title.", "Erro de Conexão", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Nenhum estudo carregado para enviar.");
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

            // 2. Filtra quais arquivos enviar
            List<DicomFile> filesToSend = new List<DicomFile>();
            var seriesSource = lstSeries.ItemsSource as List<DicomSeriesViewModel>;
            var imagesSource = lstImages.ItemsSource as List<DicomItemViewModel>;

            if (lstSeries.IsVisible && seriesSource != null && seriesSource.Any(s => s.IsSelected))
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
                MessageBox.Show("Selecione ao menos uma imagem ou série.");
                return;
            }

            // 3. Preparação Visual
            btnSend.IsEnabled = false;
            pbImagesLoading.Visibility = Visibility.Visible;
            pbImagesLoading.IsIndeterminate = false;
            pbImagesLoading.Minimum = 0;
            pbImagesLoading.Maximum = filesToSend.Count;
            pbImagesLoading.Value = 0;
            lblStatusImages.Text = $"Iniciando envio de {filesToSend.Count} imagens...";

            int successCount = 0;
            int errorCount = 0;

            try
            {
                // Cria o cliente DICOM usando a Factory
                var client = DicomClientFactory.Create(ip, porta, false, aeLocal, aeRemote);

                // --- CORREÇÃO: Usamos apenas o timeout de associação que ainda existe ---
                client.ClientOptions.AssociationRequestTimeoutInMs = 60000;
                // A linha 'DimseTimeoutInMs' foi removida pois não existe mais na versão 5.0+

                foreach (var dicomFile in filesToSend)
                {
                    var request = new DicomCStoreRequest(dicomFile);

                    request.OnResponseReceived += (req, response) =>
                    {
                        if (response.Status == DicomStatus.Success)
                            successCount++;
                        else
                            errorCount++;

                        Dispatcher.Invoke(() =>
                        {
                            pbImagesLoading.Value = successCount + errorCount;
                            lblStatusImages.Text = $"Enviando... {successCount + errorCount}/{filesToSend.Count}";
                        });
                    };

                    await client.AddRequestAsync(request);
                }

                await client.SendAsync();

                string msg = $"Envio Finalizado!\n\nSucesso: {successCount}\nFalhas: {errorCount}";
                MessageBoxImage icon = errorCount == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning;
                MessageBox.Show(msg, "Relatório", MessageBoxButton.OK, icon);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro crítico no envio: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnSend.IsEnabled = true;
                pbImagesLoading.Visibility = Visibility.Collapsed;
                lblStatusImages.Text = "Pronto.";
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
    }
}