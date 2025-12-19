using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input; // IMPORTANTE: Adicione para o MouseButtonEventArgs
using FellowOakDicom;

namespace DicomSync
{
    public partial class EditDataWindow : Window
    {
        private List<DicomFile> _filesToEdit;

        public string PatientName => txtEditName.Text;
        public string PatientID => txtEditID.Text;
        public string AccessionNumber => txtEditAccession.Text;
        public string BirthDate => txtEditBirth.Text;
        public string StudyDate => txtEditDate.Text;
        public string StudyDescription => txtEditDesc.Text;

        public EditDataWindow(List<DicomFile> files)
        {
            InitializeComponent();
            _filesToEdit = files;

            // (O código de preenchimento dos campos continua igual...)
            if (_filesToEdit != null && _filesToEdit.Count > 0)
            {
                var dataset = _filesToEdit[0].Dataset;
                txtEditName.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientName, string.Empty);
                txtEditID.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientID, string.Empty);
                txtEditAccession.Text = dataset.GetSingleValueOrDefault(DicomTag.AccessionNumber, string.Empty);
                txtEditBirth.Text = dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, string.Empty);
                txtEditDate.Text = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, string.Empty);
                txtEditDesc.Text = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, string.Empty);
            }
        }

        // --- NOVO: Permite arrastar a janela clicando no topo ---
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // --- NOVO: Botão X do topo ---
        private void btnCloseWindow_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // (Seu método btnSave_Click continua aqui igual ao anterior...)
        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // ... (Copie a lógica do btnSave_Click da resposta anterior)
            btnSave.IsEnabled = false;
            btnCancel.IsEnabled = false;
            pnlProgress.Visibility = Visibility.Visible;

            try
            {
                await Task.Run(() =>
                {
                    // --- PREPARAÇÃO ---
                    string studyFolder = Path.GetDirectoryName(_filesToEdit[0].File.Name);
                    string backupFolder = Path.Combine(studyFolder, "BACKUP_ORIGINAL");

                    if (!Directory.Exists(backupFolder))
                        Directory.CreateDirectory(backupFolder);

                    // Configura os máximos das barras
                    Dispatcher.Invoke(() => {
                        pbBackup.Maximum = _filesToEdit.Count;
                        pbBackup.Value = 0;
                        pbUpdate.Maximum = _filesToEdit.Count;
                        pbUpdate.Value = 0;
                    });

                    // --- FASE 1: BACKUP ---
                    int backupCount = 0;
                    foreach (var file in _filesToEdit)
                    {
                        string originalPath = file.File.Name;
                        string destPath = Path.Combine(backupFolder, Path.GetFileName(originalPath));

                        if (!File.Exists(destPath)) File.Copy(originalPath, destPath);

                        backupCount++;
                        Dispatcher.Invoke(() => pbBackup.Value = backupCount);
                    }

                    // --- FASE 2: ATUALIZAÇÃO ---
                    string pName = "", pID = "", pAcc = "", pBirth = "", pDate = "", pDesc = "";
                    Dispatcher.Invoke(() => {
                        pName = txtEditName.Text; pID = txtEditID.Text; pAcc = txtEditAccession.Text;
                        pBirth = txtEditBirth.Text; pDate = txtEditDate.Text; pDesc = txtEditDesc.Text;
                    });

                    int updateCount = 0;
                    foreach (var file in _filesToEdit)
                    {
                        file.Dataset.AddOrUpdate(DicomTag.PatientName, pName);
                        file.Dataset.AddOrUpdate(DicomTag.PatientID, pID);
                        file.Dataset.AddOrUpdate(DicomTag.AccessionNumber, pAcc);
                        file.Dataset.AddOrUpdate(DicomTag.PatientBirthDate, pBirth);
                        file.Dataset.AddOrUpdate(DicomTag.StudyDate, pDate);
                        file.Dataset.AddOrUpdate(DicomTag.StudyDescription, pDesc);

                        file.Save(file.File.Name);

                        updateCount++;
                        Dispatcher.Invoke(() => pbUpdate.Value = updateCount);
                    }
                });

                await Task.Delay(500);

                MessageBox.Show("Estudo atualizado com sucesso!", "Datamaker");
                this.DialogResult = true;
                this.Close();
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
            this.DialogResult = false;
            this.Close();
        }
    }
}