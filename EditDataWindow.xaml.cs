using FellowOakDicom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Necessário para o .Where e .ToArray
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DicomSync.Helpers; // Garante que o namespace do DicomFormatter está correto

namespace DicomSync
{
    public partial class EditDataWindow : Window
    {
        private List<DicomFile> _filesToEdit;

        // PROPRIEDADE NOVA: Avisa a MainWindow se o usuário quer enviar automaticamente após salvar
        public bool AutoSendRequested { get; private set; } = false;

        public EditDataWindow(List<DicomFile> files)
        {
            InitializeComponent();
            _filesToEdit = files;

            if (_filesToEdit != null && _filesToEdit.Count > 0)
            {
                var dataset = _filesToEdit[0].Dataset;

                // Preenchimento de texto normal
                txtEditName.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty);
                txtEditID.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
                txtEditAccession.Text = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);
                txtEditDesc.Text = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, string.Empty);

                // Formatar data DICOM para Brasileira ao carregar
                string rawBirth = dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, string.Empty);
                string rawDate = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty);

                txtEditBirth.Text = DicomFormatter.FormatDicomDate(rawBirth);
                txtEditDate.Text = DicomFormatter.FormatDicomDate(rawDate);
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void btnCloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // =========================================================================================
        // AÇÕES DOS BOTÕES (SALVAR vs SALVAR E ENVIAR)
        // =========================================================================================

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // Apenas salva e fecha
            if (await SaveDataAsync())
            {
                DialogResult = true;
                Close();
            }
        }

        private async void btnSaveAndSend_Click(object sender, RoutedEventArgs e)
        {
            // Salva e avisa a MainWindow para enviar
            if (await SaveDataAsync())
            {
                AutoSendRequested = true; // Ativa a flag de envio automático
                DialogResult = true;
                Close();
            }
        }

        // =========================================================================================
        // LÓGICA DE SALVAMENTO (REUTILIZÁVEL)
        // =========================================================================================

        private async Task<bool> SaveDataAsync()
        {
            // Desabilita botões para evitar clique duplo
            btnSave.IsEnabled = false;
            btnSaveAndSend.IsEnabled = false;
            btnCancel.IsEnabled = false;
            pnlProgress.Visibility = Visibility.Visible;

            try
            {
                // Captura valores da UI
                string pName = txtEditName.Text;
                string pID = txtEditID.Text;
                string pAcc = txtEditAccession.Text;
                string pDesc = txtEditDesc.Text;

                // Converte datas brasileiras de volta para padrão DICOM (yyyyMMdd)
                string pBirthDicom = DicomFormatter.ToDicomDate(txtEditBirth.Text);
                string pDateDicom = DicomFormatter.ToDicomDate(txtEditDate.Text);

                bool isAnon = btnAnonimizar.IsChecked ?? false;

                await Task.Run(() =>
                {
                    string studyFolder = Path.GetDirectoryName(_filesToEdit[0].File.Name);

                    // Lógica de Backup (Cria pasta _bkp se não existir)
                    string parentFolder = Path.GetDirectoryName(studyFolder);
                    string folderName = Path.GetFileName(studyFolder) + "_bkp";
                    var backupFolderPath = Path.Combine(parentFolder, folderName);

                    if (!Directory.Exists(backupFolderPath))
                        Directory.CreateDirectory(backupFolderPath);

                    Dispatcher.Invoke(() =>
                    {
                        pbBackup.Maximum = _filesToEdit.Count;
                        pbUpdate.Maximum = _filesToEdit.Count;
                    });

                    // FASE 1: BACKUP
                    foreach (var file in _filesToEdit)
                    {
                        string originalPath = file.File.Name;
                        string destPath = Path.Combine(backupFolderPath, Path.GetFileName(originalPath));
                        if (!File.Exists(destPath)) File.Copy(originalPath, destPath);
                        Dispatcher.Invoke(() => pbBackup.Value++);
                    }

                    // FASE 2: ATUALIZAÇÃO DOS ARQUIVOS
                    int count = 0;
                    foreach (var file in _filesToEdit)
                    {
                        if (isAnon)
                        {
                            file.Dataset.AddOrUpdate(DicomTag.PatientName, "ANONIMO");
                            file.Dataset.AddOrUpdate(DicomTag.PatientID, string.IsNullOrWhiteSpace(pID) ? "anonimo123" : pID);
                            file.Dataset.AddOrUpdate(DicomTag.AccessionNumber, string.IsNullOrWhiteSpace(pAcc) ? "anonimo123" : pAcc);

                            file.Dataset.Remove(DicomTag.PatientBirthDate);
                            file.Dataset.Remove(DicomTag.PatientSex);
                            file.Dataset.Remove(DicomTag.InstitutionName);
                            file.Dataset.AddOrUpdate(DicomTag.StudyDescription, "ESTUDO_ANONIMIZADO");
                        }
                        else
                        {
                            file.Dataset.AddOrUpdate(DicomTag.PatientName, pName);
                            file.Dataset.AddOrUpdate(DicomTag.PatientID, pID);
                            file.Dataset.AddOrUpdate(DicomTag.AccessionNumber, pAcc);
                            file.Dataset.AddOrUpdate(DicomTag.PatientBirthDate, pBirthDicom);
                            file.Dataset.AddOrUpdate(DicomTag.StudyDate, pDateDicom);
                            file.Dataset.AddOrUpdate(DicomTag.StudyDescription, pDesc);
                        }

                        // Salva o arquivo DICOM no disco (sobrescreve o original)
                        file.Save(file.File.Name);
                        count++;
                        Dispatcher.Invoke(() => pbUpdate.Value = count);
                    }
                });

                return true; // Sucesso
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar: {ex.Message}");

                // Restaura UI em caso de erro
                pnlProgress.Visibility = Visibility.Collapsed;
                btnSave.IsEnabled = true;
                btnSaveAndSend.IsEnabled = true;
                btnCancel.IsEnabled = true;
                return false; // Falha
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Lógica de máscara de data (dd/MM/yyyy)
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
                        {
                            formatted += "/" + digitsOnly.Substring(4, Math.Min(digitsOnly.Length - 4, 4));
                        }
                    }
                }

                if (textBox.Text != formatted)
                {
                    textBox.Text = formatted;
                    int newLength = formatted.Length;
                    int diff = newLength - oldLength;
                    int newSelectionStart = selectionStart + diff;

                    if (newSelectionStart < 0) newSelectionStart = 0;
                    if (newSelectionStart > newLength) newSelectionStart = newLength;

                    textBox.SelectionStart = newSelectionStart;
                }
            }
        }
    }
}