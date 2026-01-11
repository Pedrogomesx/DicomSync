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
            public bool Success { get; set; }
            public string Message { get; set; }
        }

        // 1. TESTE DE CONEXÃO (TCP + C-ECHO)
        public static async Task<DicomOperationResult> TestConnectionAsync(string ip, int port, string localAet, string remoteAet)
        {
            try
            {
                // Nível 1: Teste de porta TCP básica
                using (var tcpClient = new TcpClient())
                {
                    var connectTask = tcpClient.ConnectAsync(ip, port);
                    if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                    {
                        return new DicomOperationResult { Success = false, Message = "Timeout: Porta ou IP inacessível." };
                    }
                }

                // Nível 2: Teste C-ECHO (Protocolo DICOM)
                var client = DicomClientFactory.Create(ip, port, false, localAet, remoteAet);
                var echoSuccess = false;

                var request = new DicomCEchoRequest();
                request.OnResponseReceived += (req, res) =>
                {
                    if (res.Status == DicomStatus.Success) echoSuccess = true;
                };

                await client.AddRequestAsync(request);
                await client.SendAsync();

                return new DicomOperationResult
                {
                    Success = echoSuccess,
                    Message = echoSuccess ? "Conexão DICOM OK (C-ECHO Sucedido)!" : "Servidor recusou a associação (Verifique AE Titles)."
                };
            }
            catch (Exception ex)
            {
                return new DicomOperationResult { Success = false, Message = $"Erro técnico: {ex.Message}" };
            }
        }

        // 2. ENVIO C-STORE COM SUPORTE A ABORTO IMEDIATO
        public static async Task ExecuteSendAsync(
            IEnumerable<DicomFile> files,
            string ip,
            int port,
            string localAet,
            string remoteAet,
            Action<DicomStatus, DicomFile> onResponse,
            CancellationToken token) // Token para cancelamento
        {
            var client = DicomClientFactory.Create(ip, port, false, localAet, remoteAet);
            client.ClientOptions.AssociationRequestTimeoutInMs = 60000;

            foreach (var file in files)
            {
                // Interrompe o enfileiramento se o usuário cancelar
                if (token.IsCancellationRequested) break;

                var request = new DicomCStoreRequest(file);
                request.OnResponseReceived += (req, res) => onResponse?.Invoke(res.Status, file);

                await client.AddRequestAsync(request);
            }

            // Só dispara o envio se não houver pedido de cancelamento pendente
            if (!token.IsCancellationRequested)
            {
                // Passamos o token para o motor do fo-dicom encerrar o tráfego de rede imediatamente se solicitado
                await client.SendAsync(token);
            }
        }

        // 3. CARREGAMENTO DE ARQUIVOS
        public static async Task<List<DicomFile>> LoadFilesAsync(string folderPath, Action<int, int> onProgress)
        {
            return await Task.Run(() =>
            {
                var validFiles = new List<DicomFile>();
                var filenames = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                int total = filenames.Length;
                int current = 0;

                foreach (var file in filenames)
                {
                    current++;
                    onProgress?.Invoke(current, total);

                    try
                    {
                        // FileReadOption.ReadAll garante que o arquivo não fique preso e possa ser editado/enviado
                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        validFiles.Add(dicomFile);
                    }
                    catch { /* Ignora arquivos não DICOM */ }
                }
                return validFiles;
            });
        }

        // 4. MAPEAMENTO PARA VIEWMODELS
        public static DicomItemViewModel CreateItemViewModel(DicomFile dicomFile)
        {
            return new DicomItemViewModel
            {
                IsSelected = true,
                FileName = Path.GetFileName(dicomFile.File.Name),
                FullPath = dicomFile.File.Name,
                SeriesUID = dicomFile.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, "Desconhecida")
            };
        }

        public static List<DicomSeriesViewModel> GroupIntoSeries(IEnumerable<DicomFile> allFiles)
        {
            return allFiles
                .GroupBy(d => d.Dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, "Desconhecida"))
                .Select(g => new DicomSeriesViewModel
                {
                    IsSelected = true,
                    SeriesUID = g.Key,
                    SeriesDescription = g.First().Dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, "Sem Descrição"),
                    ImageCount = g.Count()
                })
                .ToList();
        }

        // 5. TRADUTOR DE STATUS DICOM
        public static string GetFriendlyError(DicomStatus status)
        {
            ushort code = status.Code;
            // Erros de Dataset/Tags
            if (code == 0x0106 || code == 0x0120 || code == 0x0116) return "Erro de sintaxe: Tag obrigatória vazia ou inválida.";

            // Erros de Sintaxe de Transferência (Compressão)
            if (code == 0x0122) return "Transfer Syntax não suportada pelo PACS destino.";

            // Erros de Processamento no Servidor
            if (code >= 0xC000 && code <= 0xCFFF) return "Erro de Out of Resources ou falha no banco de dados do PACS.";
            if (code == 0xFE00 || code == 0x0110) return "Operação abortada pelo servidor remoto.";

            return $"Falha na operação (Status 0x{code:X4}H)";
        }
    }
}