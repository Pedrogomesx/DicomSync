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

        private void btnAnonymize_Click(object sender, RoutedEvent e)
        {

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
                if (btnAnonimizar.IsChecked == false)
                {
                    await Task.Run(() =>
                    {
                        // --- PREPARAÇÃO ---
                        string studyFolder = Path.GetDirectoryName(_filesToEdit[0].File.Name);
                        string backupFolder = Path.Combine(studyFolder, "BACKUP_ORIGINAL");

                        if (!Directory.Exists(backupFolder))
                            Directory.CreateDirectory(backupFolder);

                        // Configura os máximos das barras
                        Dispatcher.Invoke(() =>
                        {
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
                        Dispatcher.Invoke(() =>
                        {
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
                else // Modo Anonimizar Ativo (btnAnonimizar.IsChecked == true)
                {
                    await Task.Run(() =>
                    {
                        // --- PREPARAÇÃO E BACKUP ---
                        string studyFolder = Path.GetDirectoryName(_filesToEdit[0].File.Name);
                        string backupFolder = Path.Combine(studyFolder, "BACKUP_ORIGINAL");
                        if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);

                        Dispatcher.Invoke(() => {
                            pbBackup.Maximum = _filesToEdit.Count; pbBackup.Value = 0;
                            pbUpdate.Maximum = _filesToEdit.Count; pbUpdate.Value = 0;
                        });

                        foreach (var file in _filesToEdit)
                        {
                            string destPath = Path.Combine(backupFolder, Path.GetFileName(file.File.Name));
                            if (!File.Exists(destPath)) File.Copy(file.File.Name, destPath);
                            Dispatcher.Invoke(() => pbBackup.Value++);
                        }

                        // --- FASE 2: ANONIMIZAÇÃO TOTAL ---
                        string finalID = "";
                        string finalAcc = "";

                        Dispatcher.Invoke(() =>
                        {
                            // Regra: Se vazio, "anonimo123". Se preenchido, usa o que foi digitado.
                            finalID = string.IsNullOrWhiteSpace(txtEditID.Text) ? "anonimo123" : txtEditID.Text;
                            finalAcc = string.IsNullOrWhiteSpace(txtEditAccession.Text) ? "anonimo123" : txtEditAccession.Text;
                        });

                        int updateCount = 0;
                        foreach (var file in _filesToEdit)
                        {
                            var dataset = file.Dataset;

                            // 1. Identificação do Paciente (Atualizando com os novos valores ou limpando)
                            dataset.AddOrUpdate(DicomTag.PatientName, "ANONIMO");
                            dataset.AddOrUpdate(DicomTag.PatientID, finalID);
                            dataset.AddOrUpdate(DicomTag.AccessionNumber, finalAcc);

                            // Removendo as outras tags de identificação conforme a tua lista
                            dataset.Remove(DicomTag.PatientBirthDate);
                            dataset.Remove(DicomTag.PatientSex);
                            dataset.Remove(DicomTag.PatientAddress);
                            dataset.Remove(DicomTag.PatientTelephoneNumbers);
                            dataset.Remove(DicomTag.PatientBirthTime);
                            dataset.Remove(DicomTag.PatientAge);

                            // 2. Identificadores do Estudo (Removendo datas e horas)
                            //dataset.Remove(DicomTag.StudyDate);
                            //dataset.Remove(DicomTag.SeriesDate);
                            //dataset.Remove(DicomTag.AcquisitionDate);
                            //dataset.Remove(DicomTag.ContentDate);
                            //dataset.Remove(DicomTag.StudyTime);
                            //dataset.Remove(DicomTag.SeriesTime);

                            // 3. Documentos e Instituição
                            dataset.Remove(DicomTag.InstitutionName);
                            dataset.Remove(DicomTag.InstitutionAddress);
                            dataset.Remove(DicomTag.ReferringPhysicianName);
                            dataset.Remove(DicomTag.InstitutionalDepartmentName);
                            dataset.Remove(DicomTag.OperatorsName);
                            dataset.Remove(DicomTag.PhysiciansOfRecord);

                            // Tag de segurança para saber que foi processado
                            dataset.AddOrUpdate(DicomTag.StudyDescription, "ESTUDO_ANONIMIZADO");

                            // Guardar as alterações no ficheiro
                            file.Save(file.File.Name);

                            updateCount++;
                            Dispatcher.Invoke(() => pbUpdate.Value = updateCount);
                        }
                    });

                    MessageBox.Show("Anonimização concluída com sucesso!", "DicomSync");
                    this.DialogResult = true;
                    this.Close();
                }
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