using FellowOakDicom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Necessário para o .Where e .ToArray
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DicomSync.Helpers; // Garanta que o namespace do DicomFormatter está correto

namespace DicomSync
{
    public partial class EditDataWindow : Window
    {
        private List<DicomFile> _filesToEdit;

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

                // --- AJUSTE AQUI: Formatar data DICOM para Brasileira ao carregar ---
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

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            btnSave.IsEnabled = false;
            btnCancel.IsEnabled = false;
            pnlProgress.Visibility = Visibility.Visible;

            try
            {
                // Capturamos os valores da UI (Datas estão em dd/mm/yyyy)
                string pName = txtEditName.Text;
                string pID = txtEditID.Text;
                string pAcc = txtEditAccession.Text;
                string pDesc = txtEditDesc.Text;

                // --- AJUSTE AQUI: Converter datas brasileiras de volta para padrão DICOM (yyyyMMdd) ---
                string pBirthDicom = DicomFormatter.ToDicomDate(txtEditBirth.Text);
                string pDateDicom = DicomFormatter.ToDicomDate(txtEditDate.Text);

                await Task.Run(() =>
                {
                    string studyFolder = Path.GetDirectoryName(_filesToEdit[0].File.Name);
                    string backupFolder = Path.Combine(studyFolder, "BACKUP_ORIGINAL");

                    if (!Directory.Exists(backupFolder))
                        Directory.CreateDirectory(backupFolder);

                    Dispatcher.Invoke(() =>
                    {
                        pbBackup.Maximum = _filesToEdit.Count;
                        pbUpdate.Maximum = _filesToEdit.Count;
                    });

                    // FASE 1: BACKUP
                    foreach (var file in _filesToEdit)
                    {
                        string originalPath = file.File.Name;
                        string destPath = Path.Combine(backupFolder, Path.GetFileName(originalPath));
                        if (!File.Exists(destPath)) File.Copy(originalPath, destPath);
                        Dispatcher.Invoke(() => pbBackup.Value++);
                    }

                    // FASE 2: ATUALIZAÇÃO / ANONIMIZAÇÃO
                    bool isAnon = false;
                    Dispatcher.Invoke(() => isAnon = btnAnonimizar.IsChecked ?? false);

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
                            file.Dataset.AddOrUpdate(DicomTag.PatientBirthDate, pBirthDicom); // Grava yyyyMMdd
                            file.Dataset.AddOrUpdate(DicomTag.StudyDate, pDateDicom);       // Grava yyyyMMdd
                            file.Dataset.AddOrUpdate(DicomTag.StudyDescription, pDesc);
                        }

                        file.Save(file.File.Name);
                        count++;
                        Dispatcher.Invoke(() => pbUpdate.Value = count);
                    }
                });

                MessageBox.Show("Processo concluído com sucesso!", "DicomSync");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro: {ex.Message}");
                pnlProgress.Visibility = Visibility.Collapsed;
                btnSave.IsEnabled = true;
                btnCancel.IsEnabled = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DateTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // 1. Armazena a posição atual do cursor e o texto antes de limpar
                int selectionStart = textBox.SelectionStart;
                int oldLength = textBox.Text.Length;

                // 2. Remove tudo que não é número
                string digitsOnly = new string(textBox.Text.Where(char.IsDigit).ToArray());

                // Limita a 8 dígitos (DDMMYYYY)
                if (digitsOnly.Length > 8) digitsOnly = digitsOnly.Substring(0, 8);

                string formatted = "";
                // 3. Monta a string formatada dd/mm/yyyy
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

                // 4. Só atualiza se o texto formatado for diferente do atual
                if (textBox.Text != formatted)
                {
                    textBox.Text = formatted;

                    // 5. Cálculo inteligente da posição do cursor
                    // Se o usuário adicionou um caractere que resultou em uma barra, pula o cursor adiante
                    int newLength = formatted.Length;
                    int diff = newLength - oldLength;

                    int newSelectionStart = selectionStart + diff;

                    // Garante que o cursor não saia dos limites do texto
                    if (newSelectionStart < 0) newSelectionStart = 0;
                    if (newSelectionStart > newLength) newSelectionStart = newLength;

                    textBox.SelectionStart = newSelectionStart;
                }
            }
        }
    }
}