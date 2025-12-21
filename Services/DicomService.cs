using DicomSync.ViewModels;
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using System.IO;
using System.Net.Sockets;

namespace DicomSync.Services
{
    internal class DicomService
    {
        public class DicomOperationResult
        {
            public bool Sucess { get; set; }
            public string Message { get; set; }
        }

        public static async Task<DicomOperationResult> TestConnectionAsync(string ip, int port, string localAet, string remoteAet)
        {
            // 1. Teste de porta TCP (Nível de Rede)
            try
            {
                using var tcpClient = new TcpClient();
                // Timeout curto para conexão TCP (3 segundos)
                var connectTask = tcpClient.ConnectAsync(ip, port);

                if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                {
                    return new DicomOperationResult { Sucess = false, Message = "Porta inativa ou IP inacessível (Timeout)." };
                }
            }
            catch (Exception)
            {
                return new DicomOperationResult { Sucess = false, Message = $"Falha ao abrir conexão TCP com {ip}:{port}." };
            }

            return new DicomOperationResult { Sucess = true, Message = "Conexão TCP bem-sucedida." };
        }

        // Responsabilidade: Envio C-STORE (Enviar lista de imagens)
        public static async Task ExecuteSendAsync(IEnumerable<DicomFile> files, string ip, int port, string localAet, string remoteAet, Action<DicomStatus, DicomFile> onResponse)
        {
            var client = DicomClientFactory.Create(ip, port, false, localAet, remoteAet);

            // Aumentar o timeout para operações C-STORE
            client.ClientOptions.AssociationRequestTimeoutInMs = 60000;

            foreach (var file in files)
            {
                var request = new DicomCStoreRequest(file);

                // A cada resposta do servidor, avisamos quem chamou
                request.OnResponseReceived += (req, res) => onResponse?.Invoke(res.Status, file);

                await client.AddRequestAsync(request);
            }

            await client.SendAsync();
        }
        // Responsabilidade: Varrer pasta e carregar arquivos DICOM na memória
        public static async Task<List<DicomFile>> LoadFilesAsync(string folderPath, Action<int, int> onProgress)
        {
            return await Task.Run(() =>
            {
                var validFiles = new List<DicomFile>();

                // 1. Pega lista de todos os arquivos (rápido)
                var filenames = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                int total = filenames.Length;
                int current = 0;

                // 2. Abre um por um (lento)
                foreach (var file in filenames)
                {
                    current++;

                    // Avisa quem chamou (a UI) sobre o progresso
                    // Invoke garante que o código da UI rode na hora certa, mas aqui só disparamos o aviso
                    onProgress?.Invoke(current, total);

                    try
                    {
                        // Tenta abrir como DICOM. Se falhar, cai no catch e ignora o arquivo
                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        validFiles.Add(dicomFile);
                    }
                    catch
                    {
                        // Arquivo não é DICOM ou está corrompido. Apenas ignoramos.
                    }
                }

                return validFiles;
            });
        }
        // Responsabilidade: Transformar um DicomFile em um ViewModel para a Lista
        public static DicomItemViewModel CreateItemViewModel(DicomFile dicomFile)
        {
            return new DicomItemViewModel
            {
                IsSelected = true,
                FileName = Path.GetFileName(dicomFile.File.Name),
                FullPath = dicomFile.File.Name,
                SeriesUID = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, "Unknown")
            };
        }

        // Responsabilidade: Gerar a lista de Séries a partir dos arquivos carregados
        public static List<DicomSeriesViewModel> GroupIntoSeries(IEnumerable<DicomFile> allFiles)
        {
            return allFiles
                .GroupBy(d => d.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, "Unknown"))
                .Select(g => new DicomSeriesViewModel
                {
                    IsSelected = true,
                    SeriesUID = g.Key,
                    SeriesDescription = g.First().Dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, "Sem Descrição"),
                    ImageCount = g.Count()
                })
                .ToList();
        }

        // Responsabilidade: Tradutor de Erros centralizado
        public static string GetFriendlyError(DicomStatus status)
        {
            ushort code = status.Code;
            if (code == 0x0106 || code == 0x0120 || code == 0x0116) return "Tag obrigatória vazia ou ausente.";
            if (code == 0x0122 || (code >= 0xC000 && code <= 0xCFFF)) return "Compressão (Transfer Syntax) não suportada.";
            if (code == 0xFE00 || code == 0x0110) return "Operação Abortada pelo servidor.";
            return "Falha na operação DICOM.";
        }
    }
}